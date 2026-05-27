using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
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

            if (material.NormalRgba is { } nr && nr.Length >= 4)
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

            if (material.SpecularRgba is { } sr && sr.Length >= 4)
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

            if (material.HeightRgba is { } hr && hr.Length >= 4)
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
        if (mainBlock != uint.MaxValue)
        {
            gl.UniformBlockBinding(mainProg, mainBlock, EntitySkinningUboBindingPoint);
        }

        if (_shadowProgram is { IsValid: true })
        {
            var shadowProg = _shadowProgram.Program;
            var shadowBlock = gl.GetUniformBlockIndex(shadowProg, blockName);
            if (shadowBlock != uint.MaxValue)
            {
                gl.UniformBlockBinding(shadowProg, shadowBlock, EntitySkinningUboBindingPoint);
            }
        }

        gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
    }

    private static void WriteEntitySkinningUboTailScalars(Span<byte> tail16, int gpuSkinning, int boneCount, float meshLiftY)
    {
        BinaryPrimitives.WriteInt32LittleEndian(tail16, gpuSkinning);
        BinaryPrimitives.WriteInt32LittleEndian(tail16.Slice(4), boneCount);
        BinaryPrimitives.WriteSingleLittleEndian(tail16.Slice(8), meshLiftY);
        BinaryPrimitives.WriteInt32LittleEndian(tail16.Slice(12), 0);
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

    /// <summary>Updates only the tail of the entity skinning UBO (GPU skinning flag, bone count, mesh lift).</summary>
    private void UploadEntitySkinningUboTail(GL gl, int gpuSkinning, int boneCount, float meshLiftY)
    {
        if (_entityBoneUbo == 0)
        {
            return;
        }

        WriteEntitySkinningUboTailScalars(
            _entitySkinningUboScratch.AsSpan(EntitySkinningUboMatrixBytes, 16),
            gpuSkinning,
            boneCount,
            meshLiftY);
        gl.BindBuffer(BufferTargetARB.UniformBuffer, _entityBoneUbo);
        gl.BufferSubData<byte>(
            BufferTargetARB.UniformBuffer,
            EntitySkinningUboMatrixBytes,
            _entitySkinningUboScratch.AsSpan(EntitySkinningUboMatrixBytes, 16));
        gl.BindBufferBase(BufferTargetARB.UniformBuffer, EntitySkinningUboBindingPoint, _entityBoneUbo);
        gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
    }

    private void ApplyEntityBoneSkinningUboTail(
        GL gl,
        PreviewModelSubject? model,
        float meshSpaceLiftY,
        bool boneSnapshotValid,
        int boneSnapshotCount)
    {
        if (_entityBoneUbo == 0)
        {
            return;
        }

        if (!boneSnapshotValid || model?.GpuEntityBoneSkinning != true || boneSnapshotCount <= 0)
        {
            UploadEntitySkinningUboTail(gl, 0, 0, 0f);
            return;
        }

        UploadEntitySkinningUboTail(gl, 1, boneSnapshotCount, meshSpaceLiftY);
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

            var shadowResolution = Math.Clamp(_settings.ShadowMapResolution, 256, 4096);
            try
            {
                _shadowTarget = new GlShadowMapTarget(gl, shadowResolution, useOpenGlEs);
                EmitDiagnostic(
                    $"[3D preview] Shadow map: {shadowResolution}x{shadowResolution}");
            }
            catch (Exception ex)
            {
                _shadowTarget = null;
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
            TryInitSunBillboard(gl, useOpenGlEs);
            TryInitAtmosphere(gl, useOpenGlEs);
            InitEntitySkinningBoneUbo(gl);
            _gpuAlive = true;
            _materialDirty = true;
            _meshDirty = true;

            // Context init can complete after SetScene; re-derive orbit from the current scene so updated
            // PreviewCamera defaults (or first scene push) always match the GPU path.
            if (_scene is not null)
            {
                SyncOrbitFromSceneLocked(_scene);
                _orbitSyncedKind = _scene.SceneKind == PreviewSceneKind.ItemPlane
                    ? PreviewSceneKind.ItemPlane
                    : PreviewSceneKind.BlockCube;
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
            DestroyAtmosphereResources();
            DestroySunBillboard();
            DestroyLineOverlay();
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
