using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.Core.Models;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private const float ShadowCascadeNearHalfExtent = 0.75f;
    private const float ShadowCascadeFarHalfExtent = 1.5f;
    private const float ShadowCascadeSplitDistance = 6f;

    private void GlRenderPassShadow(ref GlRenderFrame frame)
    {
        var (yaw, pitch) = PreviewLightMath.EffectiveLightYawPitch(frame.Settings, frame.RenderTime);
        frame.WorldLightDir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
        frame.LightDir = PreviewLightMath.SceneLightDirectionFromCelestialCycle(frame.WorldLightDir);
        if (frame.Settings.EnableAtmosphericSky)
        {
            EnsureAtmosphereLuts(frame.Gl, frame.WorldLightDir, frame.Settings);
        }

        frame.CascadeSplitWorldDistance = ShadowCascadeSplitDistance;
        frame.ShadowVp = BuildShadowViewProj(frame.LightDir, ShadowCascadeFarHalfExtent);
        frame.ShadowCascadesActive = frame.Settings is { EnableShadowCascades: true, EnableShadows: true } &&
                                     _shadowTargetCascadeNear is not null;
        if (frame.ShadowCascadesActive)
        {
            frame.ShadowVpNear = BuildShadowViewProj(frame.LightDir, ShadowCascadeNearHalfExtent);
        }

        frame.EntityAlphaModeUniform = PreviewSubjectAlphaPolicy.ResolveAlphaModeUniform(
            frame.Scene.SceneKind,
            frame.EntityEmulatedPreview,
            frame.Settings.EntityAlphaMode);
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
        else if (frame.BlockModel is { EnableRenderTimeAnimation: true, AnimationPreset: "entity_emulated", } &&
                 frame.Settings is { EnableEntityAnimation: true, EnableLegacyEntityWobble: true })
        {
            var animT = frame.EntityEmulatedAnimClock;
            var amp = Math.Clamp(frame.Settings.EntityAnimationAmplitude, 0f, 2f);
            var bob = Matrix4x4.CreateTranslation(0f, MathF.Sin(animT * 2.2f) * (0.035f * amp), 0f);
            var yawWobble = Matrix4x4.CreateRotationY(MathF.Sin(animT * 0.9f) * (0.22f * amp));
            var roll = Matrix4x4.CreateRotationZ(MathF.Sin(animT * 1.6f) * (0.03f * amp));
            frame.ModelMatrix = roll * yawWobble * bob * frame.ModelMatrix;
        }

        frame.ShadowAvailable = frame.Settings.EnableShadows && _shadowProgram?.IsValid == true && _shadowTarget is not null;
        if (!frame.ShadowAvailable)
        {
            return;
        }

        if (frame.ShadowCascadesActive)
        {
            RenderShadowDepthPass(ref frame, frame.ShadowVpNear, _shadowTargetCascadeNear!);
        }

        RenderShadowDepthPass(ref frame, frame.ShadowVp, _shadowTarget!);
    }

    private static Matrix4x4 BuildShadowViewProj(Vector3 worldLightDir, float orthoHalfExtent)
    {
        const float shadowBoom = 4.0f;
        const float shadowNear = shadowBoom - 2.5f;
        const float shadowFar = shadowBoom + 2.5f;
        var shadowTargetPos = Vector3.Zero;
        var shadowEye = shadowTargetPos - worldLightDir * shadowBoom;
        var shadowUp = PreviewLightMath.PickShadowViewUp(worldLightDir);
        var shadowView = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(shadowEye, shadowTargetPos, shadowUp);
        var shadowProj = PreviewGlMatrices.CreateOrthographicOpenGlRowStorage(
            -orthoHalfExtent, orthoHalfExtent,
            -orthoHalfExtent, orthoHalfExtent,
            shadowNear, shadowFar);
        return shadowProj * shadowView;
    }

    private void RenderShadowDepthPass(ref GlRenderFrame frame, Matrix4x4 shadowVp, GlShadowMapTarget target)
    {
        target.BeginShadowPass();
        frame.Gl.Enable(EnableCap.DepthTest);
        frame.Gl.DepthFunc(GLEnum.Lequal);
        frame.Gl.DepthMask(true);
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
        SetMatrixOnProgram(_shadowProgram, "uLightViewProj", shadowVp);
        SetMatrixOnProgram(_shadowProgram, "uModel", Matrix4x4.Identity);
        SetIntOnProgram(_shadowProgram, "uSceneKind", 0);
        SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
        ApplyEntitySkinningUniforms(_shadowProgram, 0, 0, 0f);
        if (frame.Settings.ShowGroundMesh)
        {
            _groundMesh!.Draw();
        }

        if (frame.Settings.DrawPreviewSubject && _mesh is { IndexCount: > 0 })
        {
            SetMatrixOnProgram(_shadowProgram, "uModel", frame.ModelMatrix);
            if (frame.Scene.SceneKind == PreviewSceneKind.ItemPlane)
            {
                ApplyEntitySkinningUniforms(_shadowProgram, 0, 0, 0f);
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
                foreach (var batch in frame.BlockModel.DrawBatches)
                {
                    if ((uint)batch.MaterialIndex >= (uint)frame.BlockSlots.Length)
                    {
                        continue;
                    }

                    if (batch.LayerPolicy.ShadowMode == PreviewDrawLayerShadowMode.Skip)
                    {
                        continue;
                    }

                    UploadMaterial(frame.Gl, frame.BlockSlots[batch.MaterialIndex], frame.Settings.NearestTextureFilter);
                    frame.Gl.ActiveTexture(TextureUnit.Texture0);
                    _albedo!.Bind(0);
                    SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                    ApplyEntityBoneSkinningUniformsBeforeDraw(
                        _shadowProgram,
                        _shadowEntityUniformLocs,
                        frame.BlockModel,
                        frame.BlockModel.EntityGpuMeshSpaceLiftY,
                        frame.EntityBoneSnapshotValid,
                        frame.EntityBoneSnapshotCount,
                        frame.Settings.EnableEntityAnimation,
                        frame.EntityBonePaletteUploaded,
                        "shadow");
                    _mesh.DrawRange(batch.FirstIndex, batch.IndexCount);
                }
            }
            else
            {
                var alphaMode = frame.EntityAlphaModeUniform;
                if (alphaMode != 0)
                {
                    SetFloatOnProgram(_shadowProgram, "uAlphaCutoff", frame.Settings.AlphaCutoff);
                    frame.Gl.ActiveTexture(TextureUnit.Texture0);
                    _albedo!.Bind(0);
                    SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                }

                SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", alphaMode);
                ApplyEntitySkinningUniforms(_shadowProgram, 0, 0, 0f);
                _mesh.Draw();
            }
        }

        target.EndShadowPass();
    }
}
