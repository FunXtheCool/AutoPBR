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
    private void GlRenderPassSetup(ref GlRenderFrame frame)
    {
                SyncEntityPreviewDebugRevision(frame.BlockModel);

                // When idle animation is off, clear spacing so the next enable always rebakes immediately (avoids one throttled skip).
                if (!frame.Settings.EnableEntityAnimation)
                {
                    _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
                }

                frame.EntityEmulatedPreview = IsEntityEmulatedPreview(frame.BlockModel);
                frame.EntityRebakeCtx = frame.BlockModel?.EmulatedRebake;
                frame.EntityEmulatedMaterialsOk = frame.EntityEmulatedPreview &&
                    frame.EntityRebakeCtx is not null &&
                    frame.BlockSlots is { Length: > 0 } &&
                    frame.BlockModel!.Materials.Length == frame.EntityRebakeCtx.OrderedTextureZipPaths.Length;

                frame.EntityEmulatedAnimClock = 0f;
                frame.EntityEmulatedPauseEdge = false;
                // Keep in sync with TryBuildStaticMesh / GPU bone fill: frame.RenderTime * speed * amp (see TryRebakeMesh / TryFillEmulatedEntityBoneMatrices).
                // Must not depend on materials being ready; otherwise frame.ModelMatrix wobble uses a different phase than bones when amp != 1.
                if (frame.EntityEmulatedPreview && frame.BlockModel is not null && frame.EntityRebakeCtx is not null)
                {
                    var speed = Math.Clamp(frame.Settings.EntityAnimationSpeed, 0f, 4f);
                    var amp = Math.Clamp(frame.Settings.EntityAnimationAmplitude, 0f, 2f);
                    var paused = frame.Settings.PauseEntityIdleAnimation;
                    if (paused)
                    {
                        if (!_prevPauseEntityIdleAnimation)
                        {
                            _frozenEntityIdleAnimClock = (float)(frame.RenderTime * speed * amp);
                        }

                        frame.EntityEmulatedAnimClock = _frozenEntityIdleAnimClock;
                    }
                    else
                    {
                        frame.EntityEmulatedAnimClock = (float)(frame.RenderTime * speed * amp);
                    }

                    frame.EntityEmulatedPauseEdge = paused != _prevPauseEntityIdleAnimation;
                    _prevPauseEntityIdleAnimation = paused;
                }

                frame.UploadedLiveEntityAnim = false;
                var setupAnimMotion = frame.Settings.EnableEntityAnimation;
                if (!setupAnimMotion)
                {
                    frame.EntityEmulatedAnimClock = 0f;
                }

                var bindPoseRebakeKey = frame.EntityRebakeCtx is not null
                    ? $"{frame.EntityRebakeCtx.PackZipPath}\u001f{frame.EntityRebakeCtx.AssetArchivePath}"
                    : null;
                var bindPoseCommitted = bindPoseRebakeKey is not null &&
                    string.Equals(bindPoseRebakeKey, _entityBindPoseCommittedKey, StringComparison.Ordinal);
                // Animation-off: 13-float bind VBO + entity shader W()+lift (see entity-preview-gpu-cpu-parity.md).
                var needsBindPoseMesh = frame.MeshDirty ||
                    !bindPoseCommitted ||
                    frame.BlockModel is not
                    {
                        GpuEntityBoneSkinning: true,
                        VertexStrideFloats: EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex
                    };
                if (frame.EntityEmulatedMaterialsOk &&
                    frame.BlockModel is not null &&
                    frame.EntityRebakeCtx is not null &&
                    !frame.Settings.EnableEntityAnimation &&
                    needsBindPoseMesh &&
                    EntityEmulatedPreviewRebaker.TryPrepareGpuSkinnedEmulatedMesh(
                        frame.EntityRebakeCtx,
                        frame.BlockModel.Materials,
                        PreviewStageConstants.GridWorldY,
                        EntityPreviewGrounding.DefaultClearance,
                        out var bindGpuVerts,
                        out var bindGpuIdx,
                        out var bindGpuBatches,
                        out var bindGpuBoneCount,
                        out var bindGpuLift,
                        applyGeometryIrSetupAnimMotion: false) &&
                    bindGpuVerts is not null &&
                    bindGpuIdx is not null &&
                    bindGpuBatches is not null)
                {
                    _emulatedGpuSkinPrepFailedKey = null;
                    frame.EntityRebakeCtx.GpuPreparedBoneCount = bindGpuBoneCount;
                    var bindGpuSubject = new PreviewModelSubject
                    {
                        InterleavedVertices = bindGpuVerts,
                        Indices = bindGpuIdx,
                        DrawBatches = bindGpuBatches,
                        Materials = frame.BlockModel.Materials,
                        PrimaryMaterialIndex = frame.BlockModel.PrimaryMaterialIndex,
                        Sprite2DFoliageTarget = frame.BlockModel.Sprite2DFoliageTarget,
                        EnableRenderTimeAnimation = frame.BlockModel.EnableRenderTimeAnimation,
                        AnimationPreset = frame.BlockModel.AnimationPreset,
                        EmulatedRebake = frame.BlockModel.EmulatedRebake,
                        GpuEntityBoneSkinning = true,
                        VertexStrideFloats = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex,
                        EntityGpuMeshSpaceLiftY = bindGpuLift,
                        EntityGpuVerticesInPreviewSpace = false,
                        EntityPreviewAnchorOffset = frame.BlockModel.EntityPreviewAnchorOffset,
                        EntityPreviewPlacementApplied = true,
                        MeshProvenance = frame.BlockModel.MeshProvenance
                    };
                    frame.BlockModel = bindGpuSubject;
                    UploadPreviewMesh(
                        bindGpuVerts,
                        bindGpuIdx,
                        EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex);
                    frame.UploadedLiveEntityAnim = true;
                    if (bindPoseRebakeKey is not null)
                    {
                        _entityBindPoseCommittedKey = bindPoseRebakeKey;
                    }

                    lock (_sync)
                    {
                        _blockModelSubject = bindGpuSubject;
                        if (frame.MeshDirty)
                        {
                            _meshDirty = false;
                        }
                    }

                    EmitEntityBindPosePrepDiagnostic(
                        bindPoseRebakeKey,
                        bindGpuBoneCount,
                        bindGpuVerts.Length / EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex,
                        bindGpuIdx.Length,
                        bindGpuLift,
                        setupAnimMotion: false);
                    EmitParityCatalogPlacementDiagnostic(
                        frame.BlockModel,
                        frame.EntityRebakeCtx,
                        setupAnimMotion,
                        gpuSkinning: true,
                        frame.EntityEmulatedAnimClock);
                }
                else if (frame.EntityEmulatedMaterialsOk &&
                    frame.BlockModel is not null &&
                    frame.EntityRebakeCtx is not null &&
                    !frame.Settings.EnableEntityAnimation &&
                    needsBindPoseMesh &&
                    EntityEmulatedPreviewRebaker.TryRebakeMesh(
                        frame.EntityRebakeCtx,
                        frame.BlockModel.Materials,
                        animationTimeSeconds: 0f,
                        out var bindCpuVerts,
                        out var bindCpuIdx,
                        out var bindCpuBatches,
                        applyGeometryIrSetupAnimMotion: false) &&
                    bindCpuVerts is not null &&
                    bindCpuIdx is not null &&
                    bindCpuBatches is not null)
                {
                    _emulatedGpuSkinPrepFailedKey = null;
                    frame.EntityRebakeCtx.GpuPreparedBoneCount = null;
                    frame.EntityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
                    frame.EntityRebakeCtx.GpuBindPoseBonePalette = null;
                    frame.EntityRebakeCtx.GpuBindPoseInterleavedVertices = null;
                    var bindCpuSubject = new PreviewModelSubject
                    {
                        InterleavedVertices = bindCpuVerts,
                        Indices = bindCpuIdx,
                        DrawBatches = bindCpuBatches,
                        Materials = frame.BlockModel.Materials,
                        PrimaryMaterialIndex = frame.BlockModel.PrimaryMaterialIndex,
                        Sprite2DFoliageTarget = frame.BlockModel.Sprite2DFoliageTarget,
                        EnableRenderTimeAnimation = frame.BlockModel.EnableRenderTimeAnimation,
                        AnimationPreset = frame.BlockModel.AnimationPreset,
                        EmulatedRebake = frame.BlockModel.EmulatedRebake,
                        GpuEntityBoneSkinning = false,
                        VertexStrideFloats = 0,
                        EntityGpuMeshSpaceLiftY = 0f,
                        EntityPreviewAnchorOffset = frame.BlockModel.EntityPreviewAnchorOffset,
                        EntityPreviewPlacementApplied = true,
                        MeshProvenance = frame.BlockModel.MeshProvenance
                    };
                    frame.BlockModel = bindCpuSubject;
                    UploadPreviewMesh(bindCpuVerts, bindCpuIdx);
                    frame.UploadedLiveEntityAnim = true;
                    if (bindPoseRebakeKey is not null)
                    {
                        _entityBindPoseCommittedKey = bindPoseRebakeKey;
                    }

                    lock (_sync)
                    {
                        _blockModelSubject = bindCpuSubject;
                        if (frame.MeshDirty)
                        {
                            _meshDirty = false;
                        }
                    }

                    EmitDiagnostic(
                        $"[3D preview] Emulated entity CPU bind-pose mesh (GPU prep failed, animation off): verts={bindCpuVerts.Length / PreviewMesh.FloatsPerVertex}, indices={bindCpuIdx.Length}.");
                    EmitParityCatalogPlacementDiagnostic(
                        frame.BlockModel,
                        frame.EntityRebakeCtx,
                        setupAnimMotion,
                        gpuSkinning: false,
                        frame.EntityEmulatedAnimClock);
                }
                else if (frame.EntityEmulatedMaterialsOk &&
                         frame.Settings.EnableEntityAnimation &&
                         frame.BlockModel is not null &&
                         frame.EntityRebakeCtx is not null)
                {
                    var rebakeKey = $"{frame.EntityRebakeCtx.PackZipPath}\u001f{frame.EntityRebakeCtx.AssetArchivePath}";
                    if (!string.Equals(rebakeKey, _emulatedRebakeSubjectKey, StringComparison.Ordinal))
                    {
                        _emulatedRebakeSubjectKey = rebakeKey;
                        _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
                    }

                    if (frame.MeshDirty)
                    {
                        _emulatedGpuSkinPrepFailedKey = null;
                    }

                    var shouldTryGpuLayout = frame.MeshDirty ||
                        (!frame.BlockModel.GpuEntityBoneSkinning &&
                         !string.Equals(rebakeKey, _emulatedGpuSkinPrepFailedKey, StringComparison.Ordinal));

                    if (shouldTryGpuLayout &&
                        EntityEmulatedPreviewRebaker.TryPrepareGpuSkinnedEmulatedMesh(
                            frame.EntityRebakeCtx,
                            frame.BlockModel.Materials,
                            PreviewStageConstants.GridWorldY,
                            EntityPreviewGrounding.DefaultClearance,
                            out var gpuVerts,
                            out var gpuIdx,
                            out var gpuBatches,
                            out var gpuBoneCount,
                            out var gpuLift,
                            setupAnimMotion))
                    {
                        _emulatedGpuSkinPrepFailedKey = null;
                        frame.EntityRebakeCtx.GpuPreparedBoneCount = gpuBoneCount;
                        var liftedGpu = new PreviewModelSubject
                        {
                            InterleavedVertices = gpuVerts!,
                            Indices = gpuIdx!,
                            DrawBatches = gpuBatches!,
                            Materials = frame.BlockModel.Materials,
                            PrimaryMaterialIndex = frame.BlockModel.PrimaryMaterialIndex,
                            Sprite2DFoliageTarget = frame.BlockModel.Sprite2DFoliageTarget,
                            EnableRenderTimeAnimation = frame.BlockModel.EnableRenderTimeAnimation,
                            AnimationPreset = frame.BlockModel.AnimationPreset,
                            EmulatedRebake = frame.BlockModel.EmulatedRebake,
                            GpuEntityBoneSkinning = true,
                            VertexStrideFloats = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex,
                            EntityGpuMeshSpaceLiftY = gpuLift,
                            EntityGpuVerticesInPreviewSpace = false,
                            EntityPreviewAnchorOffset = frame.BlockModel.EntityPreviewAnchorOffset,
                            EntityPreviewPlacementApplied = true,
                            MeshProvenance = frame.BlockModel.MeshProvenance
                        };
                        frame.BlockModel = liftedGpu;
                        UploadPreviewMesh(gpuVerts!, gpuIdx!, EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex);
                        frame.UploadedLiveEntityAnim = true;
                        lock (_sync)
                        {
                            _blockModelSubject = liftedGpu;
                            if (frame.MeshDirty)
                            {
                                _meshDirty = false;
                            }
                        }

                        EmitDiagnostic(
                            $"[3D preview] Emulated entity GPU skinned mesh: bones={gpuBoneCount}, verts={gpuVerts!.Length / EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex}, indices={gpuIdx!.Length}.");
                        EmitParityCatalogPlacementDiagnostic(
                            frame.BlockModel,
                            frame.EntityRebakeCtx,
                            setupAnimMotion,
                            gpuSkinning: true,
                            frame.EntityEmulatedAnimClock);
                    }
                    else
                    {
                        if (shouldTryGpuLayout)
                        {
                            _emulatedGpuSkinPrepFailedKey = rebakeKey;
                            frame.EntityRebakeCtx.GpuPreparedBoneCount = null;
                            frame.EntityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
                            frame.EntityRebakeCtx.GpuBindPoseBonePalette = null;
                            frame.EntityRebakeCtx.GpuBindPoseInterleavedVertices = null;
                        }

                        var needsCpuRebake = frame.MeshDirty ||
                            frame.EntityEmulatedPauseEdge ||
                            (frame.RenderTime - _lastEmulatedEntityRebakeRenderTime >= MinEmulatedEntityRebakeIntervalSeconds);
                        if (needsCpuRebake &&
                            !frame.BlockModel.GpuEntityBoneSkinning &&
                            EntityEmulatedPreviewRebaker.TryRebakeMesh(
                                frame.EntityRebakeCtx,
                                frame.BlockModel.Materials,
                                frame.EntityEmulatedAnimClock,
                                out var rbVerts,
                                out var rbIdx,
                                out var rbBatches,
                                applyGeometryIrSetupAnimMotion: setupAnimMotion) &&
                            rbVerts is not null &&
                            rbIdx is not null &&
                            rbBatches is not null)
                        {
                            var rebaked = new PreviewModelSubject
                            {
                                InterleavedVertices = rbVerts,
                                Indices = rbIdx,
                                DrawBatches = rbBatches,
                                Materials = frame.BlockModel.Materials,
                                PrimaryMaterialIndex = frame.BlockModel.PrimaryMaterialIndex,
                                Sprite2DFoliageTarget = frame.BlockModel.Sprite2DFoliageTarget,
                                EnableRenderTimeAnimation = frame.BlockModel.EnableRenderTimeAnimation,
                                AnimationPreset = frame.BlockModel.AnimationPreset,
                                EmulatedRebake = frame.BlockModel.EmulatedRebake,
                                GpuEntityBoneSkinning = false,
                                EntityGpuMeshSpaceLiftY = 0f,
                                EntityPreviewAnchorOffset = frame.BlockModel.EntityPreviewAnchorOffset,
                                EntityPreviewPlacementApplied = true,
                                MeshProvenance = frame.BlockModel.MeshProvenance
                            };
                            frame.BlockModel = rebaked;
                            UploadPreviewMesh(rebaked.InterleavedVertices, rebaked.Indices);
                            frame.UploadedLiveEntityAnim = true;
                            _lastEmulatedEntityRebakeRenderTime = frame.RenderTime;
                            lock (_sync)
                            {
                                _blockModelSubject = rebaked;
                                if (frame.MeshDirty)
                                {
                                    _meshDirty = false;
                                }
                            }

                            var rebakeDiagKey =
                                $"{rebakeKey}|verts={rebaked.InterleavedVertices.Length / PreviewMesh.FloatsPerVertex}|idx={rebaked.Indices.Length}";
                            if (!string.Equals(rebakeDiagKey, _entityCpuRebakeDiagKey, StringComparison.Ordinal))
                            {
                                _entityCpuRebakeDiagKey = rebakeDiagKey;
                                EmitDiagnostic(
                                    $"[3D preview] Emulated entity mesh rebake: verts={rebaked.InterleavedVertices.Length / PreviewMesh.FloatsPerVertex}, indices={rebaked.Indices.Length}.");
                            }

                            EmitParityCatalogPlacementDiagnostic(
                                frame.BlockModel,
                                frame.EntityRebakeCtx,
                                setupAnimMotion,
                                gpuSkinning: false,
                                frame.EntityEmulatedAnimClock);
                        }
                    }
                }

                // Mesh upload must happen before either pass so depth-only and main pass share the same VBO.
                if (frame.MeshDirty && !frame.UploadedLiveEntityAnim)
                {
                    // Keep mesh dirty until we actually have geometry to upload; render can start a few frames
                    // before frame.Scene meshes are ready, and clearing this flag too early leaves the GPU buffer empty.
                    if (!frame.Settings.DrawPreviewSubject)
                    {
                        var empty = PreviewMeshFactory.CreateEmptySubjectPlaceholder();
                        UploadPreviewMesh(empty.InterleavedVertices, empty.Indices);
                    }
                    else if (frame.BlockModel is
                             {
                                 GpuEntityBoneSkinning: true,
                                 VertexStrideFloats: > 0,
                                 InterleavedVertices.Length: > 0,
                                 Indices.Length: > 0
                             } gpuSkinned
                             && gpuSkinned.InterleavedVertices.Length % gpuSkinned.VertexStrideFloats == 0)
                    {
                        // Scene meshes are always 12-float PreviewMesh; GPU-skinned entities use a wider stride (bone index).
                        // Never upload the frame.Scene copy here or the VAO stride would desync from genesis.vert skinning.
                        UploadPreviewMesh(
                            gpuSkinned.InterleavedVertices,
                            gpuSkinned.Indices,
                            gpuSkinned.VertexStrideFloats);
                        EmitDiagnostic(
                            $"[3D preview] Mesh upload: frame.Scene={frame.Scene.SceneKind}, sourceCount={frame.Scene.Meshes.Count}, verts={gpuSkinned.InterleavedVertices.Length / gpuSkinned.VertexStrideFloats}, indices={gpuSkinned.Indices.Length}, strideFloats={gpuSkinned.VertexStrideFloats} (GPU-skinned subject).");
                    }
                    else if (frame.Scene.Meshes.Count > 0 &&
                             !(frame.EntityEmulatedPreview && frame.EntityRebakeCtx is not null))
                    {
                        var uploadMesh = frame.Scene.Meshes[0];
                        UploadPreviewMesh(uploadMesh.InterleavedVertices, uploadMesh.Indices);
                        EmitDiagnostic(
                            $"[3D preview] Mesh upload: frame.Scene={frame.Scene.SceneKind}, sourceCount={frame.Scene.Meshes.Count}, verts={uploadMesh.VertexCount}, indices={uploadMesh.Indices.Length}.");
                    }
                    else
                    {
                        // Defensive fallback: if frame.Scene mesh population races with the first render frame,
                        // synthesize a canonical mesh so preview never goes blank.
                        var uploadMesh = frame.Scene.SceneKind == PreviewSceneKind.ItemPlane
                            ? (frame.Settings.SpritePlaneCount <= 1
                                ? PreviewMeshFactory.CreateItemPlane()
                                : PreviewMeshFactory.CreateSpritePlanes(
                                    planeCount: Math.Clamp(frame.Settings.SpritePlaneCount, 1, 8)))
                            : PreviewMeshFactory.CreateUnitCube();
                        EmitDiagnostic($"[3D preview] Fallback mesh upload used ({frame.Scene.SceneKind}).");
                        UploadPreviewMesh(uploadMesh.InterleavedVertices, uploadMesh.Indices);
                    }

                    lock (_sync)
                    {
                        _meshDirty = false;
                    }
                }

                if (frame.MaterialDirty)
                {
                    if (frame.BlockModel is null || frame.BlockSlots is null)
                    {
                        UploadMaterial(frame.Gl, frame.Material, frame.Settings.NearestTextureFilter);
                    }

                    lock (_sync)
                    {
                        _materialDirty = false;
                    }
                }

                frame.EntityBoneSnapshotValid = false;
                frame.EntityBoneSnapshotCount = 0;
                frame.EntityBonePaletteUploaded = false;
                _lastEntityBoneSnapshotCount = 0;
                string? boneFillHint = null;
                var boneFillOk = false;
                if (frame.BlockModel is { GpuEntityBoneSkinning: true, EmulatedRebake: { } ebBone })
                {
                    if (setupAnimMotion)
                    {
                        boneFillOk = EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(
                            ebBone,
                            frame.EntityEmulatedAnimClock,
                            _entityBoneScratch.AsSpan(),
                            out frame.EntityBoneSnapshotCount,
                            applyGeometryIrSetupAnimMotion: true);
                        if (!boneFillOk)
                        {
                            boneFillHint = "anim_bone_fill_failed";
                        }
                    }
                    else if (ebBone.GpuBindPoseBonePalette is { Length: > 0 } cachedBind)
                    {
                        var copyCount = Math.Min(cachedBind.Length, _entityBoneScratch.Length);
                        cachedBind.AsSpan(0, copyCount).CopyTo(_entityBoneScratch);
                        frame.EntityBoneSnapshotCount = copyCount;
                        boneFillOk = copyCount > 0;
                        if (!boneFillOk)
                        {
                            boneFillHint = "cached_bind_palette_empty";
                        }
                    }
                    else
                    {
                        boneFillOk = EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(
                            ebBone,
                            0f,
                            _entityBoneScratch.AsSpan(),
                            out frame.EntityBoneSnapshotCount,
                            applyGeometryIrSetupAnimMotion: false);
                        if (!boneFillOk)
                        {
                            boneFillHint = "bind_bone_fill_failed";
                        }
                    }

                    frame.EntityBoneSnapshotValid = boneFillOk && frame.EntityBoneSnapshotCount > 0;
                    _lastEntityBoneSnapshotCount = frame.EntityBoneSnapshotCount;
                    if (setupAnimMotion && frame.EntityBoneSnapshotValid)
                    {
                        var liveLift = EntityPreviewPlacement.ComputeLiveGpuLiftY(
                            frame.BlockModel.InterleavedVertices,
                            _entityBoneScratch.AsSpan(0, frame.EntityBoneSnapshotCount),
                            frame.EntityBoneSnapshotCount,
                            PreviewStageConstants.GridWorldY);
                        if (MathF.Abs(liveLift - frame.BlockModel.EntityGpuMeshSpaceLiftY) > 1e-5f)
                        {
                            frame.BlockModel = PreviewSubjectPlacement.CopySubjectWithVertices(
                                frame.BlockModel,
                                frame.BlockModel.InterleavedVertices,
                                liveLift);
                            lock (_sync)
                            {
                                _blockModelSubject = frame.BlockModel;
                            }
                        }
                    }
                }
                else if (!setupAnimMotion &&
                         frame.BlockModel is { GpuEntityBoneSkinning: true, EntityGpuVerticesInPreviewSpace: false } &&
                         frame.EntityRebakeCtx is not null &&
                         frame.BlockModel.EntityGpuMeshSpaceLiftY != frame.EntityRebakeCtx.LastGroundLiftY)
                {
                    frame.BlockModel = PreviewSubjectPlacement.CopySubjectWithVertices(
                        frame.BlockModel,
                        frame.BlockModel.InterleavedVertices,
                        frame.EntityRebakeCtx.LastGroundLiftY);
                    lock (_sync)
                    {
                        _blockModelSubject = frame.BlockModel;
                    }
                }

                var bonePaletteUploaded = false;
                if (frame.BlockModel is { GpuEntityBoneSkinning: true } blockModel && _entityBoneUbo != 0)
                {
                    if (frame.EntityBoneSnapshotValid &&
                        frame.EntityBoneSnapshotCount > 0)
                    {
                        UploadEntitySkinningBoneMatrices(frame.Gl, frame.EntityBoneSnapshotCount);
                        bonePaletteUploaded = true;
                    }

                    TryApplyForceEntityCpuSkinningUpload(ref frame, setupAnimMotion);
                    if (frame.Settings.ForceEntityCpuSkinning)
                    {
                        bonePaletteUploaded = false;
                    }

                    if (TryResolveEntitySkinningDrawState(
                            blockModel,
                            blockModel.EntityGpuMeshSpaceLiftY,
                            frame.EntityBoneSnapshotValid,
                            frame.EntityBoneSnapshotCount,
                            setupAnimMotion,
                            out var previewSpaceVerts,
                            out var bindMesh,
                            out var gpuSkinning,
                            out var boneCount,
                            out var liftY))
                    {
                        _lastUploadedPreviewSpaceVerts = previewSpaceVerts > 0.5f ? 1 : 0;
                        _lastUploadedBindMesh = bindMesh > 0.5f ? 1 : 0;
                        _lastUploadedGpuSkinning = gpuSkinning;
                        _lastUploadedBoneCount = boneCount;
                        _lastUploadedLiftY = liftY;
                        // Uniforms are applied in PassScene/PassShadow after program.Use().
                    }
                    else
                    {
                        _lastUploadedPreviewSpaceVerts = 0;
                        _lastUploadedBindMesh = 0;
                        _lastUploadedGpuSkinning = 0;
                        _lastUploadedBoneCount = 0;
                        _lastUploadedLiftY = 0f;
                    }

                    EmitEntityGpuRuntimeDiagnostic(
                        frame.BlockModel,
                        frame.EntityRebakeCtx,
                        setupAnimMotion,
                        frame.EntityEmulatedAnimClock,
                        boneFillOk,
                        bonePaletteUploaded,
                        boneFillHint);
                    frame.EntityBonePaletteUploaded = bonePaletteUploaded;
                }
    }

    private void TryApplyForceEntityCpuSkinningUpload(ref GlRenderFrame frame, bool setupAnimMotion)
    {
        if (!frame.Settings.ForceEntityCpuSkinning ||
            frame.BlockModel is not { GpuEntityBoneSkinning: true, EmulatedRebake: { } rebake } ||
            rebake.GpuBindPoseInterleavedVertices is not { Length: > 0 } bindVerts ||
            !frame.EntityBoneSnapshotValid ||
            frame.EntityBoneSnapshotCount <= 0)
        {
            return;
        }

        var baked = EntityGpuBindMeshPreviewSpaceTransform.SkinAndBakeToPreviewLayout(
            bindVerts,
            _entityBoneScratch.AsSpan(0, frame.EntityBoneSnapshotCount),
            frame.EntityBoneSnapshotCount,
            frame.BlockModel.EntityGpuMeshSpaceLiftY);
        if (baked.Length == 0 || frame.BlockModel.Indices.Length == 0)
        {
            return;
        }

        UploadPreviewMesh(baked, frame.BlockModel.Indices, PreviewMesh.FloatsPerVertex);
        var cpuSubject = new PreviewModelSubject
        {
            InterleavedVertices = baked,
            Indices = frame.BlockModel.Indices,
            DrawBatches = frame.BlockModel.DrawBatches,
            Materials = frame.BlockModel.Materials,
            PrimaryMaterialIndex = frame.BlockModel.PrimaryMaterialIndex,
            Sprite2DFoliageTarget = frame.BlockModel.Sprite2DFoliageTarget,
            EnableRenderTimeAnimation = frame.BlockModel.EnableRenderTimeAnimation,
            AnimationPreset = frame.BlockModel.AnimationPreset,
            EmulatedRebake = frame.BlockModel.EmulatedRebake,
            GpuEntityBoneSkinning = true,
            VertexStrideFloats = PreviewMesh.FloatsPerVertex,
            EntityGpuMeshSpaceLiftY = 0f,
            EntityGpuVerticesInPreviewSpace = true,
            EntityPreviewAnchorOffset = frame.BlockModel.EntityPreviewAnchorOffset,
            EntityPreviewPlacementApplied = frame.BlockModel.EntityPreviewPlacementApplied,
            MeshProvenance = frame.BlockModel.MeshProvenance
        };
        frame.BlockModel = cpuSubject;
        lock (_sync)
        {
            _blockModelSubject = cpuSubject;
        }

        var norm = rebake.AssetArchivePath.Replace('\\', '/').TrimStart('/');
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return;
        }

        var diagKey = $"{norm}|anim={(setupAnimMotion ? 1 : 0)}|cpu=1";
        if (string.Equals(diagKey, _forceEntityCpuSkinningDiagKey, StringComparison.Ordinal))
        {
            return;
        }

        _forceEntityCpuSkinningDiagKey = diagKey;
        EmitDiagnostic(
            $"[3D preview] ForceEntityCpuSkinning: path={norm} anim={(setupAnimMotion ? 1 : 0)} " +
            $"verts={baked.Length / PreviewMesh.FloatsPerVertex} (12-float preview-space VBO; GPU W()/skinning bypassed).");
    }

    private int _entityPreviewDebugRevision = -1;

    private void SyncEntityPreviewDebugRevision(PreviewModelSubject? subject)
    {
        if (!EntityPreviewDebugSettings.RequiresMeshRebuild(_entityPreviewDebugRevision))
        {
            return;
        }

        _entityPreviewDebugRevision = EntityPreviewDebugSettings.Revision;
        _entityBindPoseCommittedKey = null;
        _emulatedGpuSkinPrepFailedKey = null;
        _emulatedRebakeSubjectKey = null;
        _parityPlacementDiagKey = null;
        _entityCpuRebakeDiagKey = null;
        ResetEntityGpuRuntimeDiagState();
        if (subject?.EmulatedRebake is { } rebake)
        {
            rebake.GpuPreparedBoneCount = null;
            rebake.GpuBindPoseInverseLocalToParent = null;
            rebake.GpuBindPoseBonePalette = null;
            rebake.GpuBindPoseInterleavedVertices = null;
        }

        lock (_sync)
        {
            _meshDirty = true;
        }
    }
}
