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
    private void GlRenderPassShadow(ref GlRenderFrame frame)
    {
                // Compute the world-space light direction once; both the shadow ortho and the main pass use it.
                frame.WorldLightDir = PreviewLightMath.LightDirectionFromYawPitch(
                    frame.Settings.LightYawDegrees, frame.Settings.LightPitchDegrees);
                if (frame.Settings.EnableAtmosphericSky)
                {
                    EnsureAtmosphereLuts(frame.Gl, frame.WorldLightDir, frame.Settings);
                }

                // Build orthographic light frame.View-projection (covers the unit cube + max POM displacement).
                // Half-extent 1.5 covers a unit cube's diagonal; near/far chosen so the boom (frame.Scene-extent + margin)
                // sits centered. Boom is intentionally larger than 2.5 so near > 0 (depth precision).
                const float shadowOrthoHalfExtent = 1.5f;
                const float shadowBoom = 4.0f;
                const float shadowNear = shadowBoom - 2.5f;
                const float shadowFar = shadowBoom + 2.5f;
                var shadowTargetPos = Vector3.Zero;
                var shadowEye = shadowTargetPos - frame.WorldLightDir * shadowBoom;
                var shadowUp = PreviewLightMath.PickShadowViewUp(frame.WorldLightDir);
                var shadowView = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(shadowEye, shadowTargetPos, shadowUp);
                var shadowProj = PreviewGlMatrices.CreateOrthographicOpenGlRowStorage(
                    -shadowOrthoHalfExtent, shadowOrthoHalfExtent,
                    -shadowOrthoHalfExtent, shadowOrthoHalfExtent,
                    shadowNear, shadowFar);
                frame.ShadowVp = shadowProj * shadowView;

                frame.EntityAlphaModeUniform = frame.EntityEmulatedPreview ? (int)frame.Settings.EntityAlphaMode : 0;
                frame.EntityBlendDraw =
                    frame.EntityEmulatedPreview &&
                    frame.Scene.SceneKind == PreviewSceneKind.BlockModel &&
                    frame.Settings.EntityAlphaMode == PreviewEntityAlphaMode.Blend;
                frame.EnableParallaxEff = PreviewEntityEmulatedShaderGating.EffectiveParallax(
                    frame.Settings.EnableParallax, frame.EntityEmulatedPreview, frame.Settings.EnableEntityParallax);
                frame.EnableParallaxAoEff = PreviewEntityEmulatedShaderGating.EffectiveParallaxAo(
                    frame.Settings.EnableParallaxAo, frame.EntityEmulatedPreview, frame.Settings.EnableEntityParallax);
                frame.EnableNormalMapEff = PreviewEntityEmulatedShaderGating.EffectiveNormalMap(
                    frame.Settings.EnableNormalMap, frame.EntityEmulatedPreview, frame.Settings.EnableEntityLabPbrShading);
                frame.EnableSpecularMapEff = PreviewEntityEmulatedShaderGating.EffectiveSpecularMap(
                    frame.Settings.EnableSpecularMap, frame.EntityEmulatedPreview, frame.Settings.EnableEntityLabPbrShading);
                frame.EnableParallaxShadowEff = PreviewEntityEmulatedShaderGating.EffectiveParallaxShadow(
                    frame.Settings.EnableParallaxShadow, frame.EntityEmulatedPreview, frame.Settings.EnableEntityParallax);

                frame.ModelMatrix = Matrix4x4.CreateRotationY((float)frame.Rotation);
                if (frame.Scene.SceneKind == PreviewSceneKind.ItemPlane)
                {
                    frame.ModelMatrix = Matrix4x4.Identity;
                }
                // Legacy whole-mesh wobble (pre–setupAnim IR); opt-in via Render frame.Settings.
                else if (frame.BlockModel?.EnableRenderTimeAnimation == true &&
                         frame.Settings.EnableEntityAnimation &&
                         frame.Settings.EnableLegacyEntityWobble &&
                         string.Equals(frame.BlockModel.AnimationPreset, "entity_emulated", StringComparison.Ordinal))
                {
                    var animT = frame.EntityEmulatedAnimClock;
                    var amp = Math.Clamp(frame.Settings.EntityAnimationAmplitude, 0f, 2f);
                    var bob = Matrix4x4.CreateTranslation(0f, MathF.Sin(animT * 2.2f) * (0.035f * amp), 0f);
                    var yaw = Matrix4x4.CreateRotationY(MathF.Sin(animT * 0.9f) * (0.22f * amp));
                    var roll = Matrix4x4.CreateRotationZ(MathF.Sin(animT * 1.6f) * (0.03f * amp));
                    frame.ModelMatrix = roll * yaw * bob * frame.ModelMatrix;
                }

                // Shadow depth pre-pass (Phase 2). Skips line overlays so debug grid/axes never cast shadows.
                frame.ShadowAvailable = frame.Settings.EnableShadows && _shadowProgram?.IsValid == true && _shadowTarget is not null;
                if (frame.ShadowAvailable)
                {
                    _shadowTarget!.BeginShadowPass();
                    frame.Gl.Enable(EnableCap.DepthTest);
                    frame.Gl.DepthFunc(GLEnum.Lequal);
                    frame.Gl.DepthMask(true);
                    // Cull front faces during the depth pass to reduce self-shadow acne on solid casters; for
                    // alpha-cut planes (sprite mode) we leave culling off so both sides cast.
                    if (ShouldCullSolidBackFaces(frame.Scene.SceneKind, frame.BlockModel))
                    {
                        frame.Gl.Enable(EnableCap.CullFace);
                        frame.Gl.CullFace(GLEnum.Front);
                        frame.Gl.FrontFace(GLEnum.Ccw);
                    }
                    else
                    {
                        frame.Gl.Disable(EnableCap.CullFace);
                    }

                    _shadowProgram!.Use();
                    SetMatrixOnProgram(_shadowProgram, "uLightViewProj", frame.ShadowVp);
                    SetMatrixOnProgram(_shadowProgram, "uModel", Matrix4x4.Identity);
                    SetIntOnProgram(_shadowProgram, "uSceneKind", 0);
                    SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
                    UploadEntitySkinningUboTail(frame.Gl, 0, 0, 0f);
                    _groundMesh!.Draw();
                    if (frame.Settings.DrawPreviewSubject && _mesh.IndexCount > 0)
                    {
                        SetMatrixOnProgram(_shadowProgram, "uModel", frame.ModelMatrix);
                        if (frame.Scene.SceneKind == PreviewSceneKind.ItemPlane)
                        {
                            UploadEntitySkinningUboTail(frame.Gl, 0, 0, 0f);
                            SetIntOnProgram(_shadowProgram, "uSceneKind", 1);
                            SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
                            SetFloatOnProgram(_shadowProgram, "uAlphaCutoff", frame.Settings.AlphaCutoff);
                            SetIntOnProgram(_shadowProgram, "uItemAlphaBlend", frame.Settings.ItemUseAlphaBlend ? 1 : 0);
                            frame.Gl.ActiveTexture(TextureUnit.Texture0);
                            _albedo!.Bind(0);
                            SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                        }
                        else
                        {
                            SetIntOnProgram(_shadowProgram, "uSceneKind", 0);
                        }

                        if (frame.BlockModel is not null && frame.BlockSlots is { Length: > 0 })
                        {
                            if (frame.EntityAlphaModeUniform != 0)
                            {
                                SetFloatOnProgram(_shadowProgram, "uAlphaCutoff", frame.Settings.AlphaCutoff);
                            }

                            SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", frame.EntityAlphaModeUniform);
                            ApplyEntityBoneSkinningUboTail(
                                frame.Gl,
                                frame.BlockModel,
                                frame.BlockModel.EntityGpuMeshSpaceLiftY,
                                frame.EntityBoneSnapshotValid,
                                frame.EntityBoneSnapshotCount);
                            foreach (var batch in frame.BlockModel.DrawBatches)
                            {
                                if ((uint)batch.MaterialIndex >= (uint)frame.BlockSlots.Length)
                                {
                                    continue;
                                }

                                UploadMaterial(frame.Gl, frame.BlockSlots[batch.MaterialIndex], frame.Settings.NearestTextureFilter);
                                frame.Gl.ActiveTexture(TextureUnit.Texture0);
                                _albedo!.Bind(0);
                                SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                                _mesh.DrawRange(batch.FirstIndex, batch.IndexCount);
                            }
                        }
                        else
                        {
                            SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
                            UploadEntitySkinningUboTail(frame.Gl, 0, 0, 0f);
                            _mesh.Draw();
                        }
                    }

                    _shadowTarget.EndShadowPass();
                }
    }
}
