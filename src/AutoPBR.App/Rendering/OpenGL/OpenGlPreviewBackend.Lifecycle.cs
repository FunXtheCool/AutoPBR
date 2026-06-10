using System.Diagnostics;
using System.Runtime.InteropServices;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Avalonia.OpenGL;
using Avalonia.Platform;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>
public sealed partial class OpenGlPreviewBackend
{
    private bool TryUploadGrassGroundTexture(GL gl)
    {
        _ = gl;
        try
        {
            var uri = new Uri("avares://AutoPBR.App/Assets/Preview/grass_block_top.png");
            if (!AssetLoader.Exists(uri))
            {
                EmitDiagnostic("[3D preview] Grass ground texture asset missing.");
                return false;
            }

            using var stream = AssetLoader.Open(uri);
            if (!PreviewGrassTextureLoader.TryDecodeTinted(stream, out var rgba, out var w, out var h) || w < 1 ||
                h < 1)
            {
                EmitDiagnostic("[3D preview] Grass ground texture decode failed.");
                return false;
            }

            _grassGroundAlbedo!.UploadRgba(w, h, rgba, nearestFilter: true);
            return true;
        }
        catch (Exception ex)
        {
            EmitDiagnostic("[3D preview] Grass ground texture load failed: " + ex.Message);
            return false;
        }
    }

    private void UploadMaterial(GL gl, PreviewMaterial? material, bool nearest)
    {
        Debug.Assert(_albedo is not null && _normal is not null && _spec is not null && _height is not null);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        try
        {
            if (material is null || material.AlbedoRgba.Length < 4)
            {
                _albedo.UploadRgba(1, 1, [180, 180, 190, 255], nearest);
                _normal.UploadRgba(1, 1, [128, 128, 255, 255], nearest);
                _spec.UploadRgba(1, 1, [120, 60, 40, 255], nearest);
                _height.UploadRgba(1, 1, [128, 128, 128, 255], nearest);
                return;
            }

            var albMem = material.AlbedoRgba;
            var (albW, albH) = ResolveRgbaDimensions(material.Width, material.Height, albMem.Length);
            var alb = albMem.Span;
            var albPx = albW * albH * 4;
            if (alb.Length < albPx)
            {
                _albedo.UploadRgba(1, 1, [180, 180, 190, 255], nearest);
            }
            else
            {
                _albedo.UploadRgba(albW, albH, alb[..albPx], nearest);
            }

            if (material.NormalRgba is { Length: >= 4 } nr)
            {
                var (nw, nh) = ResolveRgbaDimensions(material.Width, material.Height, nr.Length);
                var nPx = nw * nh * 4;
                if (nr.Length >= nPx)
                {
                    _normal.UploadRgba(nw, nh, nr[..nPx].Span, nearest);
                }
                else
                {
                    _normal.UploadRgba(1, 1, [128, 128, 255, 255], nearest);
                }
            }
            else
            {
                _normal.UploadRgba(1, 1, [128, 128, 255, 255], nearest);
            }

            if (material.SpecularRgba is { Length: >= 4 } sr)
            {
                var (sw, sh) = ResolveRgbaDimensions(material.Width, material.Height, sr.Length);
                var sPx = sw * sh * 4;
                if (sr.Length >= sPx)
                {
                    _spec.UploadRgba(sw, sh, sr[..sPx].Span, nearest);
                }
                else
                {
                    _spec.UploadRgba(1, 1, [120, 60, 40, 255], nearest);
                }
            }
            else
            {
                _spec.UploadRgba(1, 1, [120, 60, 40, 255], nearest);
            }

            if (material.HeightRgba is { Length: >= 4 } hr)
            {
                var (hw, hh) = ResolveRgbaDimensions(material.Width, material.Height, hr.Length);
                var hPx = hw * hh * 4;
                if (hr.Length >= hPx)
                {
                    _height.UploadRgba(hw, hh, hr[..hPx].Span, nearest);
                }
                else
                {
                    _height.UploadRgba(1, 1, [128, 128, 128, 255], nearest);
                }
            }
            else
            {
                _height.UploadRgba(1, 1, [128, 128, 128, 255], nearest);
            }
        }
        finally
        {
            gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }
    }

    private static (int W, int H) ResolveRgbaDimensions(int declaredW, int declaredH, int byteLength)
    {
        if (byteLength < 4)
        {
            return (1, 1);
        }

        var texels = byteLength / 4;
        var w = declaredW;
        var h = declaredH;
        if (w > 0 && h > 0 && w * h == texels)
        {
            return (w, h);
        }

        var side = (int)Math.Sqrt(texels);
        if (side > 0 && side * side == texels)
        {
            return (side, side);
        }

        if (w > 0 && texels % w == 0)
        {
            var inferredH = texels / w;
            if (inferredH <= w * 4)
            {
                return (w, inferredH);
            }
        }

        if (h > 0 && texels % h == 0)
        {
            var inferredW = texels / h;
            if (inferredW <= h * 4)
            {
                return (inferredW, h);
            }
        }

        var s = side > 0 ? side : (int)Math.Sqrt(texels);
        while (s > 1 && texels % s != 0)
        {
            s--;
        }

        if (s < 1)
        {
            s = 1;
        }

        return (s, Math.Max(1, texels / s));
    }

    private void InitEntitySkinningBoneUbo(GL gl)
    {
        if (_program is not { IsValid: true })
        {
            return;
        }

        const string blockName = "EntitySkinningBones";
        _entityBoneUbo = gl.GenBuffer();
        Array.Clear(_entitySkinningUboScratch);
        gl.BindBuffer(BufferTargetARB.UniformBuffer, _entityBoneUbo);
        gl.BufferData<byte>(BufferTargetARB.UniformBuffer, _entitySkinningUboScratch.AsSpan(), BufferUsageARB.DynamicDraw);
        gl.BindBufferBase(BufferTargetARB.UniformBuffer, EntitySkinningUboBindingPoint, _entityBoneUbo);

        var mainProg = _program.Program;
        var mainBlock = gl.GetUniformBlockIndex(mainProg, blockName);
        _entityBoneUboBlockBoundMain = mainBlock != uint.MaxValue;
        if (_entityBoneUboBlockBoundMain)
        {
            gl.UniformBlockBinding(mainProg, mainBlock, EntitySkinningUboBindingPoint);
        }

        if (_shadowProgram is { IsValid: true })
        {
            var shadowProg = _shadowProgram.Program;
            var shadowBlock = gl.GetUniformBlockIndex(shadowProg, blockName);
            _entityBoneUboBlockBoundShadow = shadowBlock != uint.MaxValue;
            if (_entityBoneUboBlockBoundShadow)
            {
                gl.UniformBlockBinding(shadowProg, shadowBlock, EntitySkinningUboBindingPoint);
            }
        }

        gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
    }

    private static EntitySkinningUniformLocs ResolveEntitySkinningUniformLocs(GlShaderProgram program) =>
        new(
            program.GetUniformLocation("uEntityPreviewSpaceVerts"),
            program.GetUniformLocation("uEntityBindMesh"),
            program.GetUniformLocation("uEntityGpuSkinning"),
            program.GetUniformLocation("uEntityBoneCount"),
            program.GetUniformLocation("uEntityMeshLiftY"));

    private void LogEntityShaderInitDiagnosticsOnce()
    {
        if (_loggedEntityShaderInit)
        {
            return;
        }

        _loggedEntityShaderInit = true;
        EmitDiagnostic(EntityGpuShaderDiagnostics.FormatEntityShaderInitLine(
            "main",
            _mainEntityUniformLocs.PreviewSpaceVerts,
            _mainEntityUniformLocs.BindMesh,
            _mainEntityUniformLocs.GpuSkinning,
            _mainEntityUniformLocs.BoneCount,
            _mainEntityUniformLocs.MeshLiftY,
            _entityBoneUboBlockBoundMain));
        EmitDiagnostic(EntityGpuShaderDiagnostics.FormatEntityShaderInitLine(
            "shadow",
            _shadowEntityUniformLocs.PreviewSpaceVerts,
            _shadowEntityUniformLocs.BindMesh,
            _shadowEntityUniformLocs.GpuSkinning,
            _shadowEntityUniformLocs.BoneCount,
            _shadowEntityUniformLocs.MeshLiftY,
            _entityBoneUboBlockBoundShadow));
        if (!_mainEntityUniformLocs.IsComplete || !_shadowEntityUniformLocs.IsComplete)
        {
            EmitDiagnostic(
                "[3D preview] GPU WARN: entity scalar uniform locations incomplete at init; 13-float entity meshes may render exploded (anim-on and anim-off) until shaders reload.");
        }

        if (!_entityBoneUboBlockBoundMain || (_shadowProgram is { IsValid: true } && !_entityBoneUboBlockBoundShadow))
        {
            EmitDiagnostic(
                "[3D preview] GPU WARN: EntitySkinningBones UBO block not bound on one or more programs; anim-on bone multiply will not run.");
        }
    }

    private void ApplyEntityBoneSkinningUniformsBeforeDraw(
        GlShaderProgram? program,
        EntitySkinningUniformLocs locs,
        PreviewModelSubject? model,
        float meshSpaceLiftY,
        bool boneSnapshotValid,
        int boneSnapshotCount,
        bool setupAnimMotion,
        bool bonePaletteUploaded,
        string passLabel)
    {
        if (bonePaletteUploaded && _entityBoneUbo != 0)
        {
            _gl!.BindBufferBase(BufferTargetARB.UniformBuffer, EntitySkinningUboBindingPoint, _entityBoneUbo);
        }

        var resolveOk = TryApplyEntityBoneSkinningUniforms(
            program,
            locs,
            model,
            meshSpaceLiftY,
            boneSnapshotValid,
            boneSnapshotCount,
            setupAnimMotion);
        EmitEntityDrawContractDiagnostic(
            passLabel,
            locs,
            model,
            setupAnimMotion,
            boneSnapshotValid,
            boneSnapshotCount,
            bonePaletteUploaded,
            resolveOk);
    }

    private bool TryApplyEntityBoneSkinningUniforms(
        GlShaderProgram? program,
        EntitySkinningUniformLocs locs,
        PreviewModelSubject? model,
        float meshSpaceLiftY,
        bool boneSnapshotValid,
        int boneSnapshotCount,
        bool setupAnimMotion)
    {
        if (TryResolveEntitySkinningDrawState(
                model,
                meshSpaceLiftY,
                boneSnapshotValid,
                boneSnapshotCount,
                setupAnimMotion,
                out var previewSpaceVerts,
                out var bindMesh,
                out var gpuSkinning,
                out var boneCount,
                out var liftY))
        {
            ApplyEntitySkinningUniforms(program, locs, previewSpaceVerts, bindMesh, gpuSkinning, boneCount, liftY);
            _lastUploadedPreviewSpaceVerts = previewSpaceVerts > 0.5f ? 1 : 0;
            _lastUploadedBindMesh = bindMesh > 0.5f ? 1 : 0;
            _lastUploadedGpuSkinning = gpuSkinning;
            _lastUploadedBoneCount = boneCount;
            _lastUploadedLiftY = liftY;
            return true;
        }

        ApplyEntitySkinningUniforms(program, locs, 0f, 0f, 0f, 0f, 0f);
        _lastUploadedPreviewSpaceVerts = 0;
        _lastUploadedBindMesh = 0;
        _lastUploadedGpuSkinning = 0;
        _lastUploadedBoneCount = 0;
        _lastUploadedLiftY = 0f;
        return false;
    }

    private void ApplyEntitySkinningUniforms(
        GlShaderProgram? program,
        EntitySkinningUniformLocs locs,
        float previewSpaceVerts,
        float bindMesh,
        float gpuSkinning,
        float boneCount,
        float meshLiftY)
    {
        if (program is not { IsValid: true })
        {
            return;
        }

        if (locs.PreviewSpaceVerts >= 0)
        {
            _gl!.Uniform1(locs.PreviewSpaceVerts, previewSpaceVerts);
        }

        if (locs.BindMesh >= 0)
        {
            _gl!.Uniform1(locs.BindMesh, bindMesh);
        }

        if (locs.GpuSkinning >= 0)
        {
            _gl!.Uniform1(locs.GpuSkinning, gpuSkinning);
        }

        if (locs.BoneCount >= 0)
        {
            _gl!.Uniform1(locs.BoneCount, boneCount);
        }

        if (locs.MeshLiftY >= 0)
        {
            _gl!.Uniform1(locs.MeshLiftY, meshLiftY);
        }
    }

    // ReSharper disable once UnusedMember.Local -- called from PassScene/PassShadow partials
    private void ApplyEntitySkinningUniforms(GlShaderProgram? program, int gpuSkinning, int boneCount, float meshLiftY) =>
        ApplyEntitySkinningUniforms(
            program,
            program == _shadowProgram ? _shadowEntityUniformLocs : _mainEntityUniformLocs,
            0f,
            0f,
            gpuSkinning,
            boneCount,
            meshLiftY);

    private static bool TryResolveEntitySkinningDrawState(
        PreviewModelSubject? model,
        float meshSpaceLiftY,
        bool boneSnapshotValid,
        int boneSnapshotCount,
        bool setupAnimMotion,
        out float previewSpaceVerts,
        out float bindMesh,
        out int gpuSkinning,
        out int boneCount,
        out float liftY)
    {
        previewSpaceVerts = 0f;
        bindMesh = 0f;
        gpuSkinning = 0;
        boneCount = 0;
        liftY = 0f;
        if (model is not { GpuEntityBoneSkinning: true, EmulatedRebake.GpuPreparedBoneCount: > 0 and var preparedCount })
        {
            return false;
        }

        if (model.EntityGpuVerticesInPreviewSpace)
        {
            previewSpaceVerts = 1f;
            gpuSkinning = 0;
            boneCount = 0;
            liftY = 0f;
            return true;
        }

        var paletteReady = boneSnapshotValid && boneSnapshotCount > 0;
        previewSpaceVerts = 0f;
        bindMesh = 1f;
        gpuSkinning = paletteReady && setupAnimMotion ? 1 : 0;
        boneCount = paletteReady ? boneSnapshotCount : preparedCount;
        liftY = meshSpaceLiftY;

        return true;
    }

    private void UploadEntitySkinningBoneMatrices(GL gl, int boneSnapshotCount)
    {
        if (_entityBoneUbo == 0)
        {
            return;
        }

        var matrixFloats = MemoryMarshal.Cast<byte, float>(_entitySkinningUboScratch.AsSpan(0, EntitySkinningUboMatrixBytes));
        var n = Math.Clamp(boneSnapshotCount, 0, EntityGpuSkinningLimits.MaxBones);
        for (var i = 0; i < n; i++)
        {
            // Numerics row-major storage; GLSL std140 mat4 is column-major. Copying M11..M44 in order matches
            // glUniformMatrix4(..., transpose:false) so mat4 * vec4 agrees with Vector3.Transform (see MatrixTransformGlColumnParityTests).
            var m = _entityBoneScratch[i];
            MemoryMarshal.CreateReadOnlySpan(ref m.M11, 16).CopyTo(matrixFloats.Slice(i * 16, 16));
        }

        if (n < EntityGpuSkinningLimits.MaxBones)
        {
            matrixFloats.Slice(n * 16, (EntityGpuSkinningLimits.MaxBones - n) * 16).Clear();
        }

        gl.BindBuffer(BufferTargetARB.UniformBuffer, _entityBoneUbo);
        gl.BufferSubData<byte>(BufferTargetARB.UniformBuffer, 0, _entitySkinningUboScratch.AsSpan(0, EntitySkinningUboMatrixBytes));
        gl.BindBufferBase(BufferTargetARB.UniformBuffer, EntitySkinningUboBindingPoint, _entityBoneUbo);
        gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
    }

    /// <summary>Called from <see cref="AutoPBR.App.Controls.GlPbrPreviewControl.OnOpenGlInit"/> only.</summary>
    internal void GlInit(GlInterface glInterface)
    {
        lock (_sync)
        {
            _lastError = null;
            try
            {
                _gl = GL.GetApi(glInterface.GetProcAddress);
            }
            catch (Exception ex)
            {
                _lastError = ex.ToString();
                EmitDiagnostic("[3D preview] " + _lastError);
                return;
            }

            var gl = _gl;
            string versionStr;
            unsafe
            {
                var p = gl.GetString(StringName.Version);
                versionStr = p is null ? "(unknown)" : Marshal.PtrToStringUTF8((nint)p) ?? "(unknown)";
            }

            var useOpenGlEs = versionStr.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
            _useOpenGlEs = useOpenGlEs;
            _program = new GlShaderProgram(gl, "genesis.vert", "genesis.frag", useOpenGlEs, out var err);
            if (!_program.IsValid)
            {
                _lastError = err ?? "Shader link failed.";
                EmitDiagnostic("[3D preview] " + _lastError);
                _program.Dispose();
                _program = null;
                return;
            }

            EmitDiagnostic(useOpenGlEs
                ? $"[3D preview] Context: {versionStr} (Genesis shader path, GLSL ES 3.0)."
                : $"[3D preview] Context: {versionStr} (Genesis shader path, GLSL 330 core).");
            _mainEntityUniformLocs = ResolveEntitySkinningUniformLocs(_program);

            // Genesis Shadows Phase 2: depth-only program + FBO shadow target. If either fails we keep
            // the main path running and just disable shadow sampling at draw time.
            // PHASE3-CSM hook: when cascades arrive, allocate N targets here (or one array texture).
            _shadowProgram = new GlShaderProgram(gl, "genesis_shadow.vert", "genesis_shadow.frag", useOpenGlEs,
                out var shadowErr);
            if (!_shadowProgram.IsValid)
            {
                EmitDiagnostic("[3D preview] Shadow program: " + (shadowErr ?? "link failed"));
                _shadowProgram.Dispose();
                _shadowProgram = null;
            }
            else
            {
                _shadowEntityUniformLocs = ResolveEntitySkinningUniformLocs(_shadowProgram);
            }

            InitEntitySkinningBoneUbo(gl);
            LogEntityShaderInitDiagnosticsOnce();

            var shadowResolution = Math.Clamp(_settings.ShadowMapResolution, 256, 4096);
            try
            {
                _shadowTarget = new GlShadowMapTarget(gl, shadowResolution, useOpenGlEs);
                _shadowTargetCascadeNear = new GlShadowMapTarget(gl, shadowResolution, useOpenGlEs);
                EmitDiagnostic(
                    $"[3D preview] Shadow map: {shadowResolution}x{shadowResolution} (near cascade ready)");
            }
            catch (Exception ex)
            {
                _shadowTarget = null;
                _shadowTargetCascadeNear = null;
                EmitDiagnostic("[3D preview] Shadow target init failed: " + ex.Message);
            }

            var nearest = true;
            _albedo = new GlTexture2D(gl, nearest);
            _normal = new GlTexture2D(gl, nearest);
            _spec = new GlTexture2D(gl, nearest);
            _height = new GlTexture2D(gl, nearest);
            _mesh = new GlMeshBuffer(gl);
            _groundMesh = new GlMeshBuffer(gl);
            var groundGeom = PreviewMeshFactory.CreatePreviewGroundPlane();
            _groundMesh.Upload(groundGeom.InterleavedVertices, groundGeom.Indices);

            _neutralNormal = new GlTexture2D(gl, nearest);
            _neutralNormal.UploadRgba(1, 1, [128, 128, 255, 255], nearest);
            _neutralSpec = new GlTexture2D(gl, nearest);
            _neutralSpec.UploadRgba(1, 1, [120, 60, 40, 255], nearest);
            _neutralHeight = new GlTexture2D(gl, nearest);
            _neutralHeight.UploadRgba(1, 1, [128, 128, 128, 255], nearest);

            _grassGroundAlbedo = new GlTexture2D(gl, nearest);
            _grassGroundReady = TryUploadGrassGroundTexture(gl);

            TryInitLineOverlay(gl, useOpenGlEs);
            TryInitMoonBillboard(gl, useOpenGlEs);
            TryInitAtmosphere(gl, useOpenGlEs);
            TryInitGodRays(gl, useOpenGlEs);
            TryInitVolume(gl, useOpenGlEs);
            TryInitVolumetricClouds(gl, useOpenGlEs);
            EmitDiagnostic(
                "[3D preview] Feature init: " +
                $"sky={(_atmoSkyProgram is { IsValid: true } ? "lut" : _proceduralSkyProgram is { IsValid: true } ? "procedural" : "off")}, " +
                $"atmoLut={(_atmoTransProgram is { IsValid: true } && _atmoSkyViewProgram is { IsValid: true } ? "yes" : "no")}, " +
                $"volume={(_volumeInjectProgram is { IsValid: true } && _volumeIntegrateProgram is { IsValid: true } ? (_volumeUseLiteShaders ? "lite" : "full") : "off")}, " +
                $"godRays={(_godRayCompositeProgram is { IsValid: true } ? "yes" : "no")}.");
            _gpuAlive = true;
            _materialDirty = true;
            _meshDirty = true;

            // Context init can complete after SetScene; re-derive orbit from the current scene so updated
            // PreviewCamera defaults (or first scene push) always match the GPU path.
            if (_scene is not null)
            {
                SyncOrbitFromSceneLocked(_scene);
                _orbitSyncedKey = ResolveOrbitSyncKey(_scene, _blockModelSubject);
            }

            _loggedMeshReady = false;
            _loggedZeroIndex = false;
        }
    }

    /// <summary>Called from <see cref="AutoPBR.App.Controls.GlPbrPreviewControl.OnOpenGlDeinit"/> only.</summary>
    internal void GlDeinit(GlInterface glInterface)
    {
        _ = glInterface;
        lock (_sync)
        {
            _gpuAlive = false;
            _mesh?.Dispose();
            _mesh = null;
            _groundMesh?.Dispose();
            _groundMesh = null;
            _grassGroundAlbedo?.Dispose();
            _grassGroundAlbedo = null;
            _neutralNormal?.Dispose();
            _neutralNormal = null;
            _neutralSpec?.Dispose();
            _neutralSpec = null;
            _neutralHeight?.Dispose();
            _neutralHeight = null;
            _grassGroundReady = false;
            _albedo?.Dispose();
            _albedo = null;
            _normal?.Dispose();
            _normal = null;
            _spec?.Dispose();
            _spec = null;
            _height?.Dispose();
            _height = null;
            _program?.Dispose();
            _program = null;
            _shadowProgram?.Dispose();
            _shadowProgram = null;
            _shadowTarget?.Dispose();
            _shadowTarget = null;
            _shadowTargetCascadeNear?.Dispose();
            _shadowTargetCascadeNear = null;
            DestroyAtmosphereResources();
            DestroyGodRayResources();
            DestroyVolumeResources();
            DestroyVolumetricCloudResources();
            DestroyMoonBillboard();
            DestroyLineOverlay();
            DestroySunDebugOverlay();
            if (_entityBoneUbo != 0)
            {
                _gl?.DeleteBuffer(_entityBoneUbo);
                _entityBoneUbo = 0;
            }

            _gl = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            _gpuAlive = false;
        }
    }
}
