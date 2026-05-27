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
    /// <summary>Called from <see cref="AutoPBR.App.Controls.GlPbrPreviewControl.OnOpenGlRender"/> only.</summary>
    internal void GlRender(GlInterface glInterface, int framebuffer, int pixelWidth, int pixelHeight)
    {
        _ = glInterface;
        PreviewRenderSettings settings;
        IRenderPreviewScene? scene;
        PreviewMaterial? material;
        PreviewModelSubject? blockModel;
        PreviewMaterial[]? blockSlots;
        double rotation;
        double renderTime;
        Vector3 orbitBaseTarget;
        Vector3 orbitPan;
        Vector3 debugFlyWorldOffset;
        float orbitYaw;
        float orbitPitch;
        float orbitDistance;
        lock (_sync)
        {
            if (!_gpuAlive || _gl is null || _program is null || !_program.IsValid || _albedo is null ||
                _normal is null || _spec is null || _height is null || _mesh is null || _groundMesh is null ||
                _neutralNormal is null || _neutralSpec is null || _neutralHeight is null)
            {
                return;
            }

            settings = CloneSettings(_settings);
            scene = _scene;
            material = _material;
            blockModel = _blockModelSubject;
            blockSlots = _blockModelSlots;
            rotation = _rotationAccum;
            renderTime = _renderTimeAccum;
            orbitBaseTarget = _orbitBaseTarget;
            orbitPan = _orbitPan;
            debugFlyWorldOffset = _debugFlyWorldOffset;
            orbitYaw = _orbitYaw;
            orbitPitch = _orbitPitch;
            orbitDistance = _orbitDistance;
        }

        var gl = _gl!;
        var defaultFbo = framebuffer;
        // Always render to the full control backing surface; relying on GL_VIEWPORT from BeginDraw can become stale
        // during splitter drags, leaving unrendered bands when the preview column is resized.
        var vpX = 0;
        var vpY = 0;
        var vw = Math.Max(1, pixelWidth);
        var vh = Math.Max(1, pixelHeight);

        // Bind backbuffer first; if there's no scene we still want a clean clear so the host control is not stale.
        if (defaultFbo != 0)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)defaultFbo);
        }
        else
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        gl.Viewport(vpX, vpY, (uint)vw, (uint)vh);
        gl.Disable(EnableCap.ScissorTest);

        if (scene is null)
        {
            gl.ClearColor(0.12f, 0.12f, 0.14f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            return;
        }

        bool meshDirty;
        bool materialDirty;
        lock (_sync)
        {
            meshDirty = _meshDirty;
            materialDirty = _materialDirty;
        }

        // When idle animation is off, clear spacing so the next enable always rebakes immediately (avoids one throttled skip).
        if (!settings.EnableEntityAnimation)
        {
            _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
        }

        var entityEmulatedPreview = IsEntityEmulatedPreview(blockModel);
        var entityRebakeCtx = blockModel?.EmulatedRebake;
        var entityEmulatedMaterialsOk = entityEmulatedPreview &&
            entityRebakeCtx is not null &&
            blockSlots is { Length: > 0 } &&
            blockModel!.Materials.Length == entityRebakeCtx.OrderedTextureZipPaths.Length;

        float entityEmulatedAnimClock = 0f;
        var entityEmulatedPauseEdge = false;
        // Keep in sync with TryBuildStaticMesh / GPU bone fill: renderTime * speed * amp (see TryRebakeMesh / TryFillEmulatedEntityBoneMatrices).
        // Must not depend on materials being ready; otherwise modelMatrix wobble uses a different phase than bones when amp != 1.
        if (entityEmulatedPreview && blockModel is not null && entityRebakeCtx is not null)
        {
            var speed = Math.Clamp(settings.EntityAnimationSpeed, 0f, 4f);
            var amp = Math.Clamp(settings.EntityAnimationAmplitude, 0f, 2f);
            var paused = settings.PauseEntityIdleAnimation;
            if (paused)
            {
                if (!_prevPauseEntityIdleAnimation)
                {
                    _frozenEntityIdleAnimClock = (float)(renderTime * speed * amp);
                }

                entityEmulatedAnimClock = _frozenEntityIdleAnimClock;
            }
            else
            {
                entityEmulatedAnimClock = (float)(renderTime * speed * amp);
            }

            entityEmulatedPauseEdge = paused != _prevPauseEntityIdleAnimation;
            _prevPauseEntityIdleAnimation = paused;
        }

        var uploadedLiveEntityAnim = false;
        // When entity animation is off, rebake to lifted IR bind pose (no setupAnim overlay). Initial pack
        // conversion bakes with setupAnim enabled; this path restores static pose until animation is re-enabled.
        if (entityEmulatedMaterialsOk &&
            blockModel is not null &&
            entityRebakeCtx is not null &&
            !settings.EnableEntityAnimation &&
            blockModel.GpuEntityBoneSkinning &&
            EntityEmulatedPreviewRebaker.TryRebakeMesh(
                entityRebakeCtx,
                blockModel.Materials,
                entityEmulatedAnimClock,
                out var revertVerts,
                out var revertIdx,
                out var revertBatches,
                applyGeometryIrSetupAnimMotion: false) &&
            revertVerts is not null &&
            revertIdx is not null &&
            revertBatches is not null)
        {
            entityRebakeCtx.GpuPreparedBoneCount = null;
            entityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
            var lifted = PreviewSubjectPlacement.LiftSubjectIfClipping(new PreviewModelSubject
            {
                InterleavedVertices = revertVerts,
                Indices = revertIdx,
                DrawBatches = revertBatches,
                Materials = blockModel.Materials,
                PrimaryMaterialIndex = blockModel.PrimaryMaterialIndex,
                Sprite2DFoliageTarget = blockModel.Sprite2DFoliageTarget,
                EnableRenderTimeAnimation = blockModel.EnableRenderTimeAnimation,
                AnimationPreset = blockModel.AnimationPreset,
                EmulatedRebake = blockModel.EmulatedRebake,
                GpuEntityBoneSkinning = false,
                EntityGpuMeshSpaceLiftY = 0f,
            });
            blockModel = lifted;
            _mesh!.Upload(lifted.InterleavedVertices, lifted.Indices);
            uploadedLiveEntityAnim = true;
            lock (_sync)
            {
                _blockModelSubject = lifted;
                if (meshDirty)
                {
                    _meshDirty = false;
                }
            }

            EmitDiagnostic(
                $"[3D preview] Emulated entity CPU mesh (animation off): verts={lifted.InterleavedVertices.Length / PreviewMesh.FloatsPerVertex}, indices={lifted.Indices.Length}.");
        }
        else if (entityEmulatedMaterialsOk &&
                 settings.EnableEntityAnimation &&
                 blockModel is not null &&
                 entityRebakeCtx is not null)
        {
            var rebakeKey = $"{entityRebakeCtx.PackZipPath}\u001f{entityRebakeCtx.AssetArchivePath}";
            if (!string.Equals(rebakeKey, _emulatedRebakeSubjectKey, StringComparison.Ordinal))
            {
                _emulatedRebakeSubjectKey = rebakeKey;
                _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
            }

            if (meshDirty)
            {
                _emulatedGpuSkinPrepFailedKey = null;
            }

            var shouldTryGpuLayout = meshDirty ||
                (!blockModel.GpuEntityBoneSkinning &&
                 !string.Equals(rebakeKey, _emulatedGpuSkinPrepFailedKey, StringComparison.Ordinal));

            if (shouldTryGpuLayout &&
                EntityEmulatedPreviewRebaker.TryPrepareGpuSkinnedEmulatedMesh(
                    entityRebakeCtx,
                    blockModel.Materials,
                    PreviewStageConstants.GridWorldY,
                    0.002f,
                    out var gpuVerts,
                    out var gpuIdx,
                    out var gpuBatches,
                    out var gpuBoneCount,
                    out var gpuLift))
            {
                _emulatedGpuSkinPrepFailedKey = null;
                entityRebakeCtx.GpuPreparedBoneCount = gpuBoneCount;
                var liftedGpu = new PreviewModelSubject
                {
                    InterleavedVertices = gpuVerts!,
                    Indices = gpuIdx!,
                    DrawBatches = gpuBatches!,
                    Materials = blockModel.Materials,
                    PrimaryMaterialIndex = blockModel.PrimaryMaterialIndex,
                    Sprite2DFoliageTarget = blockModel.Sprite2DFoliageTarget,
                    EnableRenderTimeAnimation = blockModel.EnableRenderTimeAnimation,
                    AnimationPreset = blockModel.AnimationPreset,
                    EmulatedRebake = blockModel.EmulatedRebake,
                    GpuEntityBoneSkinning = true,
                    VertexStrideFloats = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex,
                    EntityGpuMeshSpaceLiftY = gpuLift,
                };
                blockModel = liftedGpu;
                _mesh!.Upload(gpuVerts!, gpuIdx!, EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex);
                uploadedLiveEntityAnim = true;
                lock (_sync)
                {
                    _blockModelSubject = liftedGpu;
                    if (meshDirty)
                    {
                        _meshDirty = false;
                    }
                }

                EmitDiagnostic(
                    $"[3D preview] Emulated entity GPU skinned mesh: bones={gpuBoneCount}, verts={gpuVerts!.Length / EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex}, indices={gpuIdx!.Length}.");
            }
            else
            {
                if (shouldTryGpuLayout)
                {
                    _emulatedGpuSkinPrepFailedKey = rebakeKey;
                    entityRebakeCtx.GpuPreparedBoneCount = null;
                    entityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
                }

                var needsCpuRebake = meshDirty ||
                    entityEmulatedPauseEdge ||
                    (renderTime - _lastEmulatedEntityRebakeRenderTime >= MinEmulatedEntityRebakeIntervalSeconds);
                if (needsCpuRebake &&
                    !blockModel.GpuEntityBoneSkinning &&
                    EntityEmulatedPreviewRebaker.TryRebakeMesh(
                        entityRebakeCtx,
                        blockModel.Materials,
                        entityEmulatedAnimClock,
                        out var rbVerts,
                        out var rbIdx,
                        out var rbBatches) &&
                    rbVerts is not null &&
                    rbIdx is not null &&
                    rbBatches is not null)
                {
                    var lifted = PreviewSubjectPlacement.LiftSubjectIfClipping(new PreviewModelSubject
                    {
                        InterleavedVertices = rbVerts,
                        Indices = rbIdx,
                        DrawBatches = rbBatches,
                        Materials = blockModel.Materials,
                        PrimaryMaterialIndex = blockModel.PrimaryMaterialIndex,
                        Sprite2DFoliageTarget = blockModel.Sprite2DFoliageTarget,
                        EnableRenderTimeAnimation = blockModel.EnableRenderTimeAnimation,
                        AnimationPreset = blockModel.AnimationPreset,
                        EmulatedRebake = blockModel.EmulatedRebake,
                        GpuEntityBoneSkinning = false,
                        EntityGpuMeshSpaceLiftY = 0f,
                    });
                    blockModel = lifted;
                    _mesh!.Upload(lifted.InterleavedVertices, lifted.Indices);
                    uploadedLiveEntityAnim = true;
                    _lastEmulatedEntityRebakeRenderTime = renderTime;
                    lock (_sync)
                    {
                        _blockModelSubject = lifted;
                        if (meshDirty)
                        {
                            _meshDirty = false;
                        }
                    }

                    EmitDiagnostic(
                        $"[3D preview] Emulated entity mesh rebake: verts={lifted.InterleavedVertices.Length / PreviewMesh.FloatsPerVertex}, indices={lifted.Indices.Length}.");
                }
            }
        }

        // Mesh upload must happen before either pass so depth-only and main pass share the same VBO.
        if (meshDirty && !uploadedLiveEntityAnim)
        {
            // Keep mesh dirty until we actually have geometry to upload; render can start a few frames
            // before scene meshes are ready, and clearing this flag too early leaves the GPU buffer empty.
            if (!settings.DrawPreviewSubject)
            {
                var empty = PreviewMeshFactory.CreateEmptySubjectPlaceholder();
                _mesh!.Upload(empty.InterleavedVertices, empty.Indices);
            }
            else if (blockModel is
                     {
                         GpuEntityBoneSkinning: true,
                         VertexStrideFloats: > 0,
                         InterleavedVertices.Length: > 0,
                         Indices.Length: > 0
                     } gpuSkinned
                     && gpuSkinned.InterleavedVertices.Length % gpuSkinned.VertexStrideFloats == 0)
            {
                // Scene meshes are always 12-float PreviewMesh; GPU-skinned entities use a wider stride (bone index).
                // Never upload the scene copy here or the VAO stride would desync from genesis.vert skinning.
                _mesh!.Upload(
                    gpuSkinned.InterleavedVertices,
                    gpuSkinned.Indices,
                    gpuSkinned.VertexStrideFloats);
                EmitDiagnostic(
                    $"[3D preview] Mesh upload: scene={scene.SceneKind}, sourceCount={scene.Meshes.Count}, verts={gpuSkinned.InterleavedVertices.Length / gpuSkinned.VertexStrideFloats}, indices={gpuSkinned.Indices.Length}, strideFloats={gpuSkinned.VertexStrideFloats} (GPU-skinned subject).");
            }
            else if (scene.Meshes.Count > 0)
            {
                var uploadMesh = scene.Meshes[0];
                _mesh!.Upload(uploadMesh.InterleavedVertices, uploadMesh.Indices);
                EmitDiagnostic(
                    $"[3D preview] Mesh upload: scene={scene.SceneKind}, sourceCount={scene.Meshes.Count}, verts={uploadMesh.VertexCount}, indices={uploadMesh.Indices.Length}.");
            }
            else
            {
                // Defensive fallback: if scene mesh population races with the first render frame,
                // synthesize a canonical mesh so preview never goes blank.
                var uploadMesh = scene.SceneKind == PreviewSceneKind.ItemPlane
                    ? (settings.SpritePlaneCount <= 1
                        ? PreviewMeshFactory.CreateItemPlane()
                        : PreviewMeshFactory.CreateSpritePlanes(
                            planeCount: Math.Clamp(settings.SpritePlaneCount, 1, 8)))
                    : PreviewMeshFactory.CreateUnitCube();
                EmitDiagnostic($"[3D preview] Fallback mesh upload used ({scene.SceneKind}).");
                _mesh!.Upload(uploadMesh.InterleavedVertices, uploadMesh.Indices);
            }

            lock (_sync)
            {
                _meshDirty = false;
            }
        }

        if (materialDirty)
        {
            if (blockModel is null || blockSlots is null)
            {
                UploadMaterial(gl, material, settings.NearestTextureFilter);
            }

            lock (_sync)
            {
                _materialDirty = false;
            }
        }

        var entityBoneSnapshotValid = false;
        var entityBoneSnapshotCount = 0;
        if (blockModel is { GpuEntityBoneSkinning: true, EmulatedRebake: { } ebBone } &&
            entityEmulatedMaterialsOk &&
            EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(
                ebBone,
                entityEmulatedAnimClock,
                _entityBoneScratch.AsSpan(),
                out entityBoneSnapshotCount))
        {
            entityBoneSnapshotValid = entityBoneSnapshotCount > 0;
        }

        if (entityBoneSnapshotValid &&
            blockModel?.GpuEntityBoneSkinning == true &&
            _entityBoneUbo != 0)
        {
            UploadEntitySkinningBoneMatrices(gl, entityBoneSnapshotCount);
        }

        // Compute the world-space light direction once; both the shadow ortho and the main pass use it.
        var worldLightDir = PreviewLightMath.LightDirectionFromYawPitch(
            settings.LightYawDegrees, settings.LightPitchDegrees);
        if (settings.EnableAtmosphericSky)
        {
            EnsureAtmosphereLuts(gl, worldLightDir, settings);
        }

        // Build orthographic light view-projection (covers the unit cube + max POM displacement).
        // Half-extent 1.5 covers a unit cube's diagonal; near/far chosen so the boom (scene-extent + margin)
        // sits centered. Boom is intentionally larger than 2.5 so near > 0 (depth precision).
        const float shadowOrthoHalfExtent = 1.5f;
        const float shadowBoom = 4.0f;
        const float shadowNear = shadowBoom - 2.5f;
        const float shadowFar = shadowBoom + 2.5f;
        var shadowTargetPos = Vector3.Zero;
        var shadowEye = shadowTargetPos - worldLightDir * shadowBoom;
        var shadowUp = PreviewLightMath.PickShadowViewUp(worldLightDir);
        var shadowView = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(shadowEye, shadowTargetPos, shadowUp);
        var shadowProj = PreviewGlMatrices.CreateOrthographicOpenGlRowStorage(
            -shadowOrthoHalfExtent, shadowOrthoHalfExtent,
            -shadowOrthoHalfExtent, shadowOrthoHalfExtent,
            shadowNear, shadowFar);
        var shadowVp = shadowProj * shadowView;

        var entityAlphaModeUniform = entityEmulatedPreview ? (int)settings.EntityAlphaMode : 0;
        var entityBlendDraw =
            entityEmulatedPreview &&
            scene.SceneKind == PreviewSceneKind.BlockModel &&
            settings.EntityAlphaMode == PreviewEntityAlphaMode.Blend;
        var enableParallaxEff = PreviewEntityEmulatedShaderGating.EffectiveParallax(
            settings.EnableParallax, entityEmulatedPreview, settings.EnableEntityParallax);
        var enableParallaxAoEff = PreviewEntityEmulatedShaderGating.EffectiveParallaxAo(
            settings.EnableParallaxAo, entityEmulatedPreview, settings.EnableEntityParallax);
        var enableNormalMapEff = PreviewEntityEmulatedShaderGating.EffectiveNormalMap(
            settings.EnableNormalMap, entityEmulatedPreview, settings.EnableEntityLabPbrShading);
        var enableSpecularMapEff = PreviewEntityEmulatedShaderGating.EffectiveSpecularMap(
            settings.EnableSpecularMap, entityEmulatedPreview, settings.EnableEntityLabPbrShading);
        var enableParallaxShadowEff = PreviewEntityEmulatedShaderGating.EffectiveParallaxShadow(
            settings.EnableParallaxShadow, entityEmulatedPreview, settings.EnableEntityParallax);

        var modelMatrix = Matrix4x4.CreateRotationY((float)rotation);
        if (scene.SceneKind == PreviewSceneKind.ItemPlane)
        {
            modelMatrix = Matrix4x4.Identity;
        }
        // Legacy whole-mesh wobble (pre–setupAnim IR); opt-in via Render settings.
        else if (blockModel?.EnableRenderTimeAnimation == true &&
                 settings.EnableEntityAnimation &&
                 settings.EnableLegacyEntityWobble &&
                 string.Equals(blockModel.AnimationPreset, "entity_emulated", StringComparison.Ordinal))
        {
            var animT = entityEmulatedAnimClock;
            var amp = Math.Clamp(settings.EntityAnimationAmplitude, 0f, 2f);
            var bob = Matrix4x4.CreateTranslation(0f, MathF.Sin(animT * 2.2f) * (0.035f * amp), 0f);
            var yaw = Matrix4x4.CreateRotationY(MathF.Sin(animT * 0.9f) * (0.22f * amp));
            var roll = Matrix4x4.CreateRotationZ(MathF.Sin(animT * 1.6f) * (0.03f * amp));
            modelMatrix = roll * yaw * bob * modelMatrix;
        }

        // Shadow depth pre-pass (Phase 2). Skips line overlays so debug grid/axes never cast shadows.
        var shadowAvailable = settings.EnableShadows && _shadowProgram?.IsValid == true && _shadowTarget is not null;
        if (shadowAvailable)
        {
            _shadowTarget!.BeginShadowPass();
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(GLEnum.Lequal);
            gl.DepthMask(true);
            // Cull front faces during the depth pass to reduce self-shadow acne on solid casters; for
            // alpha-cut planes (sprite mode) we leave culling off so both sides cast.
            if (ShouldCullSolidBackFaces(scene.SceneKind, blockModel))
            {
                gl.Enable(EnableCap.CullFace);
                gl.CullFace(GLEnum.Front);
                gl.FrontFace(GLEnum.Ccw);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
            }

            _shadowProgram!.Use();
            SetMatrixOnProgram(_shadowProgram, "uLightViewProj", shadowVp);
            SetMatrixOnProgram(_shadowProgram, "uModel", Matrix4x4.Identity);
            SetIntOnProgram(_shadowProgram, "uSceneKind", 0);
            SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
            UploadEntitySkinningUboTail(gl, 0, 0, 0f);
            _groundMesh!.Draw();
            if (settings.DrawPreviewSubject && _mesh.IndexCount > 0)
            {
                SetMatrixOnProgram(_shadowProgram, "uModel", modelMatrix);
                if (scene.SceneKind == PreviewSceneKind.ItemPlane)
                {
                    UploadEntitySkinningUboTail(gl, 0, 0, 0f);
                    SetIntOnProgram(_shadowProgram, "uSceneKind", 1);
                    SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
                    SetFloatOnProgram(_shadowProgram, "uAlphaCutoff", settings.AlphaCutoff);
                    SetIntOnProgram(_shadowProgram, "uItemAlphaBlend", settings.ItemUseAlphaBlend ? 1 : 0);
                    gl.ActiveTexture(TextureUnit.Texture0);
                    _albedo!.Bind(0);
                    SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                }
                else
                {
                    SetIntOnProgram(_shadowProgram, "uSceneKind", 0);
                }

                if (blockModel is not null && blockSlots is { Length: > 0 })
                {
                    if (entityAlphaModeUniform != 0)
                    {
                        SetFloatOnProgram(_shadowProgram, "uAlphaCutoff", settings.AlphaCutoff);
                    }

                    SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", entityAlphaModeUniform);
                    ApplyEntityBoneSkinningUboTail(
                        gl,
                        blockModel,
                        blockModel.EntityGpuMeshSpaceLiftY,
                        entityBoneSnapshotValid,
                        entityBoneSnapshotCount);
                    foreach (var batch in blockModel.DrawBatches)
                    {
                        if ((uint)batch.MaterialIndex >= (uint)blockSlots.Length)
                        {
                            continue;
                        }

                        UploadMaterial(gl, blockSlots[batch.MaterialIndex], settings.NearestTextureFilter);
                        gl.ActiveTexture(TextureUnit.Texture0);
                        _albedo!.Bind(0);
                        SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                        _mesh.DrawRange(batch.FirstIndex, batch.IndexCount);
                    }
                }
                else
                {
                    SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
                    UploadEntitySkinningUboTail(gl, 0, 0, 0f);
                    _mesh.Draw();
                }
            }

            _shadowTarget.EndShadowPass();
        }

        // Restore main-pass framebuffer + viewport (BeginShadowPass snapshots & EndShadowPass restores
        // the GL viewport, but binding our actual default FBO again is cheap and explicit).
        if (defaultFbo != 0)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)defaultFbo);
        }
        else
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        gl.Viewport(vpX, vpY, (uint)vw, (uint)vh);
        gl.Disable(EnableCap.ScissorTest);
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(GLEnum.Lequal);
        gl.DepthMask(true);
        if (ShouldCullSolidBackFaces(scene.SceneKind, blockModel))
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(GLEnum.Back);
            gl.FrontFace(GLEnum.Ccw);
        }
        else
        {
            gl.Disable(EnableCap.CullFace);
        }

        var drewAtmosphereSky = false;
        if (settings.EnableAtmosphericSky && _atmoLutsValid && _atmoSkyProgram?.IsValid == true && _atmoSkyViewTex != 0)
        {
            gl.Disable(EnableCap.DepthTest);
            gl.DepthMask(false);
            DrawAtmosphereSky(gl, worldLightDir, settings);
            gl.DepthMask(true);
            drewAtmosphereSky = true;
        }

        if (drewAtmosphereSky)
        {
            gl.Clear(ClearBufferMask.DepthBufferBit);
        }
        else
        {
            gl.ClearColor(0.12f, 0.12f, 0.14f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        var cam = scene.Camera;
        ComposeOrbitEye(orbitBaseTarget, orbitPan, debugFlyWorldOffset, orbitYaw, orbitPitch, orbitDistance,
            out var eye, out var lookTarget);
        var aspect = vw / (float)vh;
        var proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
            cam.FieldOfViewDegrees * (MathF.PI / 180f),
            aspect,
            cam.NearPlane,
            cam.FarPlane);
        var view = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(eye, lookTarget, Vector3.UnitY);

        // Genesis Shadows Phase 2 routes the user-controlled yaw/pitch through PreviewLightMath so the
        // shadow ortho frustum and the shaded direct lighting agree on direction. The scene.Light.Direction
        // is kept as a fallback when the helper produces a degenerate vector.
        var lightDir = worldLightDir;
        if (lightDir.LengthSquared() < 1e-8f)
        {
            lightDir = scene.Light.Direction.LengthSquared() < 1e-8f
                ? new Vector3(-0.35f, -0.85f, -0.4f)
                : Vector3.Normalize(scene.Light.Direction);
        }

        // Sun billboard: draw before opaque geometry so depth testing hides it behind the cube/grid while
        // the atmosphere sky (drawn earlier without depth) stays behind the sun.
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(GLEnum.Lequal);
        DrawSunBillboard(gl, proj, view, eye, lightDir, ShouldCullSolidBackFaces(scene.SceneKind, blockModel));

        _program.Use();
        SetMatrix("uView", view);
        SetMatrix("uProj", proj);
        SetMatrix("uLightViewProj", shadowVp);

        SetVec3("uCameraPos", eye);
        SetVec3("uLightDir", lightDir);
        SetVec3("uLightColor", scene.Light.Color);
        SetFloat("uAmbient", settings.AmbientStrength);
        SetFloat("uNormalStrength", settings.NormalStrength);
        SetFloat("uHeightStrength", settings.HeightStrength);
        SetFloat("uSpecularStrength", settings.SpecularStrength);
        SetFloat("uRoughnessScale", settings.RoughnessScale);
        SetFloat("uExposure", settings.Exposure);
        SetFloat("uParallaxAoStrength", settings.ParallaxAoStrength);
        SetInt("uEnableParallax", enableParallaxEff ? 1 : 0);
        SetInt("uEnableParallaxAo", enableParallaxAoEff ? 1 : 0);
        SetInt("uEnableNormalMap", enableNormalMapEff ? 1 : 0);
        SetInt("uEnableSpecularMap", enableSpecularMapEff ? 1 : 0);
        SetInt("uSceneKind", scene.SceneKind == PreviewSceneKind.ItemPlane ? 1 : 0);
        SetFloat("uAlphaCutoff", settings.AlphaCutoff);
        SetInt("uItemAlphaBlend", settings.ItemUseAlphaBlend ? 1 : 0);
        SetInt("uEntityAlphaMode", 0);

        // Genesis-specific uniforms.
        SetInt("uEnableSss", settings.EnableSss ? 1 : 0);
        SetInt("uEnableParallaxShadow", enableParallaxShadowEff ? 1 : 0);
        SetInt("uEnableIbl", settings.EnableIbl ? 1 : 0);
        SetFloat("uSssStrength", settings.SssStrength);
        SetFloat("uIblStrength", settings.IblStrength);
        SetFloat("uEmissionStrength", settings.EmissionStrength);
        SetInt("uEnableAtmosphericSky", settings.EnableAtmosphericSky ? 1 : 0);
        SetFloat("uAtmosphereTurbidity", settings.AtmosphereTurbidity);
        SetFloat("uAtmosphereSunIntensity", settings.AtmosphereSunIntensity);
        SetFloat("uAtmosphereHorizonFalloff", settings.AtmosphereHorizonFalloff);
        // Soft neutral sky/ground tints; future plan can expose these as user settings.
        SetVec3("uSkyTint", new Vector3(0.55f, 0.62f, 0.74f));
        SetVec3("uGroundTint", new Vector3(0.22f, 0.20f, 0.18f));

        // Directional shadow map (Genesis Shadows Phase 2). Bound to texture unit 4.
        var shadowEnabledForShader = shadowAvailable;
        SetInt("uEnableShadowMap", shadowEnabledForShader ? 1 : 0);
        SetFloat("uShadowMinBias", settings.ShadowMinBias);
        SetFloat("uShadowMaxBias", settings.ShadowMaxBias);
        var shadowRes = _shadowTarget?.Resolution ?? Math.Clamp(settings.ShadowMapResolution, 256, 4096);
        SetVec2("uShadowTexelSize", new Vector2(1f / shadowRes, 1f / shadowRes));
        if (_shadowTarget is not null)
        {
            gl.ActiveTexture(TextureUnit.Texture4);
            gl.BindTexture(TextureTarget.Texture2D, _shadowTarget.DepthTextureHandle);
            SetInt("uShadowMap", 4);
        }

        // Tinted vanilla grass plane sits under the grid; one texture tile per world unit (nearest + repeat).
        if (_grassGroundReady && _grassGroundAlbedo is not null && _groundMesh!.IndexCount > 0)
        {
            var restoreCull = gl.IsEnabled(EnableCap.CullFace);
            gl.Disable(EnableCap.CullFace);
            SetMatrix("uModel", Matrix4x4.Identity);
            SetInt("uEnableParallax", 0);
            SetInt("uEnableNormalMap", 0);
            SetInt("uEnableSpecularMap", 0);
            SetInt("uSceneKind", 0);
            SetInt("uEntityAlphaMode", 0);
            UploadEntitySkinningUboTail(gl, 0, 0, 0f);
            SetInt("uHasNormal", 0);
            SetInt("uHasSpecular", 0);
            SetInt("uHasHeight", 0);
            _grassGroundAlbedo.Bind(0);
            _neutralNormal!.Bind(1);
            _neutralSpec!.Bind(2);
            _neutralHeight!.Bind(3);
            SetInt("uAlbedo", 0);
            SetInt("uNormal", 1);
            SetInt("uSpecular", 2);
            SetInt("uHeight", 3);
            _groundMesh.Draw();
            if (restoreCull)
            {
                gl.Enable(EnableCap.CullFace);
            }
        }

        if (settings.ShowBackgroundGrid && _lineProgram?.IsValid == true &&
            _gridVertexCount > 0)
        {
            DrawBackgroundGrid(gl, proj, view);
            // DrawBackgroundGrid binds the line program; restore main material program before mesh uniforms.
            _program.Use();
        }

        SetMatrix("uModel", modelMatrix);
        SetInt("uEnableParallax", enableParallaxEff ? 1 : 0);
        SetInt("uEnableNormalMap", enableNormalMapEff ? 1 : 0);
        SetInt("uEnableSpecularMap", enableSpecularMapEff ? 1 : 0);
        SetInt("uSceneKind", scene.SceneKind == PreviewSceneKind.ItemPlane ? 1 : 0);
        SetInt("uEntityAlphaMode", 0);
        if (_atmoSkyViewTex != 0)
        {
            gl.ActiveTexture(TextureUnit.Texture5);
            gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
        }

        SetInt("uAtmoSkyViewLut", 5);

        if (!settings.DrawPreviewSubject || _mesh.IndexCount <= 0)
        {
            if (!_loggedZeroIndex && settings.DrawPreviewSubject && _mesh.IndexCount <= 0)
            {
                EmitDiagnostic(
                    $"[3D preview] Draw skipped: index buffer empty (scene={scene.SceneKind}, sceneMeshCount={scene.Meshes.Count}, meshDirty={meshDirty}).");
                _loggedZeroIndex = true;
            }
        }
        else if (blockModel is not null && blockSlots is { Length: > 0 })
        {
            if (!_loggedMeshReady)
            {
                EmitDiagnostic(
                    $"[3D preview] Draw ready: indexCount={_mesh.IndexCount}, scene={scene.SceneKind}, lightYaw={settings.LightYawDegrees:F1}, lightPitch={settings.LightPitchDegrees:F1}.");
                _loggedMeshReady = true;
            }

            SetInt("uEntityAlphaMode", entityAlphaModeUniform);
            var blendWasEnabled = false;
            if (entityBlendDraw)
            {
                blendWasEnabled = gl.IsEnabled(EnableCap.Blend);
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }

            ApplyEntityBoneSkinningUboTail(
                gl,
                blockModel,
                blockModel.EntityGpuMeshSpaceLiftY,
                entityBoneSnapshotValid,
                entityBoneSnapshotCount);
            foreach (var batch in blockModel.DrawBatches)
            {
                if ((uint)batch.MaterialIndex >= (uint)blockSlots.Length)
                {
                    continue;
                }

                var slot = blockSlots[batch.MaterialIndex];
                UploadMaterial(gl, slot, settings.NearestTextureFilter);
                var bHasN = slot.NormalRgba is { Length: > 0 };
                var bHasS = slot.SpecularRgba is { Length: > 0 };
                var bHasH = slot.HeightRgba is { Length: > 0 };
                SetInt("uHasNormal", bHasN ? 1 : 0);
                SetInt("uHasSpecular", bHasS ? 1 : 0);
                SetInt("uHasHeight", bHasH ? 1 : 0);
                _albedo.Bind(0);
                _normal.Bind(1);
                _spec.Bind(2);
                _height.Bind(3);
                SetInt("uAlbedo", 0);
                SetInt("uNormal", 1);
                SetInt("uSpecular", 2);
                SetInt("uHeight", 3);
                _mesh.DrawRange(batch.FirstIndex, batch.IndexCount);
            }

            if (entityBlendDraw && !blendWasEnabled)
            {
                gl.Disable(EnableCap.Blend);
            }
        }
        else
        {
            var hasN = material?.NormalRgba is { Length: > 0 };
            var hasS = material?.SpecularRgba is { Length: > 0 };
            var hasH = material?.HeightRgba is { Length: > 0 };
            SetInt("uHasNormal", hasN ? 1 : 0);
            SetInt("uHasSpecular", hasS ? 1 : 0);
            SetInt("uHasHeight", hasH ? 1 : 0);
            _albedo.Bind(0);
            _normal.Bind(1);
            _spec.Bind(2);
            _height.Bind(3);
            SetInt("uAlbedo", 0);
            SetInt("uNormal", 1);
            SetInt("uSpecular", 2);
            SetInt("uHeight", 3);
            if (!_loggedMeshReady)
            {
                EmitDiagnostic(
                    $"[3D preview] Draw ready: indexCount={_mesh.IndexCount}, scene={scene.SceneKind}, lightYaw={settings.LightYawDegrees:F1}, lightPitch={settings.LightPitchDegrees:F1}.");
                _loggedMeshReady = true;
            }

            UploadEntitySkinningUboTail(gl, 0, 0, 0f);
            _mesh.Draw();
        }

        if (settings.ShowCornerAxes && _lineProgram?.IsValid == true)
        {
            DrawCornerAxes(gl, vpX, vpY, vw, vh, proj, view);
        }
    }
}