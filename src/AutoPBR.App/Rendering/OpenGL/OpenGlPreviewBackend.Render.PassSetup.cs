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
                // When entity animation is off, rebake to lifted IR bind pose (no setupAnim overlay). Initial pack
                // conversion bakes with setupAnim enabled; this path restores static pose until animation is re-enabled.
                if (frame.EntityEmulatedMaterialsOk &&
                    frame.BlockModel is not null &&
                    frame.EntityRebakeCtx is not null &&
                    !frame.Settings.EnableEntityAnimation &&
                    frame.BlockModel.GpuEntityBoneSkinning &&
                    EntityEmulatedPreviewRebaker.TryRebakeMesh(
                        frame.EntityRebakeCtx,
                        frame.BlockModel.Materials,
                        frame.EntityEmulatedAnimClock,
                        out var revertVerts,
                        out var revertIdx,
                        out var revertBatches,
                        applyGeometryIrSetupAnimMotion: false) &&
                    revertVerts is not null &&
                    revertIdx is not null &&
                    revertBatches is not null)
                {
                    frame.EntityRebakeCtx.GpuPreparedBoneCount = null;
                    frame.EntityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
                    var lifted = PreviewSubjectPlacement.LiftSubjectIfClipping(new PreviewModelSubject
                    {
                        InterleavedVertices = revertVerts,
                        Indices = revertIdx,
                        DrawBatches = revertBatches,
                        Materials = frame.BlockModel.Materials,
                        PrimaryMaterialIndex = frame.BlockModel.PrimaryMaterialIndex,
                        Sprite2DFoliageTarget = frame.BlockModel.Sprite2DFoliageTarget,
                        EnableRenderTimeAnimation = frame.BlockModel.EnableRenderTimeAnimation,
                        AnimationPreset = frame.BlockModel.AnimationPreset,
                        EmulatedRebake = frame.BlockModel.EmulatedRebake,
                        GpuEntityBoneSkinning = false,
                        EntityGpuMeshSpaceLiftY = 0f,
                    });
                    frame.BlockModel = lifted;
                    _mesh!.Upload(lifted.InterleavedVertices, lifted.Indices);
                    frame.UploadedLiveEntityAnim = true;
                    lock (_sync)
                    {
                        _blockModelSubject = lifted;
                        if (frame.MeshDirty)
                        {
                            _meshDirty = false;
                        }
                    }

                    EmitDiagnostic(
                        $"[3D preview] Emulated entity CPU mesh (animation off): verts={lifted.InterleavedVertices.Length / PreviewMesh.FloatsPerVertex}, indices={lifted.Indices.Length}.");
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
                            0.002f,
                            out var gpuVerts,
                            out var gpuIdx,
                            out var gpuBatches,
                            out var gpuBoneCount,
                            out var gpuLift))
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
                        };
                        frame.BlockModel = liftedGpu;
                        _mesh!.Upload(gpuVerts!, gpuIdx!, EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex);
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
                    }
                    else
                    {
                        if (shouldTryGpuLayout)
                        {
                            _emulatedGpuSkinPrepFailedKey = rebakeKey;
                            frame.EntityRebakeCtx.GpuPreparedBoneCount = null;
                            frame.EntityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
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
                                Materials = frame.BlockModel.Materials,
                                PrimaryMaterialIndex = frame.BlockModel.PrimaryMaterialIndex,
                                Sprite2DFoliageTarget = frame.BlockModel.Sprite2DFoliageTarget,
                                EnableRenderTimeAnimation = frame.BlockModel.EnableRenderTimeAnimation,
                                AnimationPreset = frame.BlockModel.AnimationPreset,
                                EmulatedRebake = frame.BlockModel.EmulatedRebake,
                                GpuEntityBoneSkinning = false,
                                EntityGpuMeshSpaceLiftY = 0f,
                            });
                            frame.BlockModel = lifted;
                            _mesh!.Upload(lifted.InterleavedVertices, lifted.Indices);
                            frame.UploadedLiveEntityAnim = true;
                            _lastEmulatedEntityRebakeRenderTime = frame.RenderTime;
                            lock (_sync)
                            {
                                _blockModelSubject = lifted;
                                if (frame.MeshDirty)
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
                if (frame.MeshDirty && !frame.UploadedLiveEntityAnim)
                {
                    // Keep mesh dirty until we actually have geometry to upload; render can start a few frames
                    // before frame.Scene meshes are ready, and clearing this flag too early leaves the GPU buffer empty.
                    if (!frame.Settings.DrawPreviewSubject)
                    {
                        var empty = PreviewMeshFactory.CreateEmptySubjectPlaceholder();
                        _mesh!.Upload(empty.InterleavedVertices, empty.Indices);
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
                        _mesh!.Upload(
                            gpuSkinned.InterleavedVertices,
                            gpuSkinned.Indices,
                            gpuSkinned.VertexStrideFloats);
                        EmitDiagnostic(
                            $"[3D preview] Mesh upload: frame.Scene={frame.Scene.SceneKind}, sourceCount={frame.Scene.Meshes.Count}, verts={gpuSkinned.InterleavedVertices.Length / gpuSkinned.VertexStrideFloats}, indices={gpuSkinned.Indices.Length}, strideFloats={gpuSkinned.VertexStrideFloats} (GPU-skinned subject).");
                    }
                    else if (frame.Scene.Meshes.Count > 0)
                    {
                        var uploadMesh = frame.Scene.Meshes[0];
                        _mesh!.Upload(uploadMesh.InterleavedVertices, uploadMesh.Indices);
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
                        _mesh!.Upload(uploadMesh.InterleavedVertices, uploadMesh.Indices);
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
                if (frame.BlockModel is { GpuEntityBoneSkinning: true, EmulatedRebake: { } ebBone } &&
                    frame.EntityEmulatedMaterialsOk &&
                    EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(
                        ebBone,
                        frame.EntityEmulatedAnimClock,
                        _entityBoneScratch.AsSpan(),
                        out frame.EntityBoneSnapshotCount))
                {
                    frame.EntityBoneSnapshotValid = frame.EntityBoneSnapshotCount > 0;
                }

                if (frame.EntityBoneSnapshotValid &&
                    frame.BlockModel?.GpuEntityBoneSkinning == true &&
                    _entityBoneUbo != 0)
                {
                    UploadEntitySkinningBoneMatrices(frame.Gl, frame.EntityBoneSnapshotCount);
                }
    }
}
