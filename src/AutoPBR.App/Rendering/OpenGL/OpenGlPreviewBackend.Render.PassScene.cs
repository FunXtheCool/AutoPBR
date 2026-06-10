using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>
public sealed partial class OpenGlPreviewBackend
{
    private void GlRenderPassScene(ref GlRenderFrame frame)
    {
                if (_program is null || _albedo is null || _normal is null || _spec is null || _height is null || _mesh is null)
                {
                    return;
                }

                SyncGodRayToggleState(frame.Settings);
                SyncVolumetricToggleState(frame.Settings);
                frame.GodRayCaptureActive = TryBeginGodRaySceneRender(ref frame);

                // Restore main-pass framebuffer + viewport (BeginShadowPass snapshots & EndShadowPass restores
                // the GL viewport, but binding our actual default FBO again is cheap and explicit).
                if (!frame.GodRayCaptureActive)
                {
                    if (frame.DefaultFbo != 0)
                    {
                        frame.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)frame.DefaultFbo);
                    }
                    else
                    {
                        frame.Gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                    }
                }

                frame.Gl.Viewport(frame.VpX, frame.VpY, (uint)frame.Vw, (uint)frame.Vh);
                frame.Gl.Disable(EnableCap.ScissorTest);
                frame.Gl.Enable(EnableCap.DepthTest);
                frame.Gl.DepthFunc(GLEnum.Lequal);
                frame.Gl.DepthMask(true);
                if (ShouldCullSolidBackFaces(frame.Scene.SceneKind, frame.BlockModel))
                {
                    frame.Gl.Enable(EnableCap.CullFace);
                    frame.Gl.CullFace(GLEnum.Back);
                    frame.Gl.FrontFace(GLEnum.Ccw);
                }
                else
                {
                    frame.Gl.Disable(EnableCap.CullFace);
                }

                // Camera must exist before sky / sun projection / froxel placement.
                var cam = frame.Scene.Camera;
                ComposeOrbitEye(frame.OrbitBaseTarget, frame.OrbitPan, frame.DebugFlyWorldOffset, frame.OrbitYaw, frame.OrbitPitch, frame.OrbitDistance,
                    out frame.Eye, out frame.LookTarget);
                var aspect = frame.Vw / (float)Math.Max(frame.Vh, 1);
                frame.Proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
                    cam.FieldOfViewDegrees * (MathF.PI / 180f),
                    aspect,
                    cam.NearPlane,
                    cam.FarPlane);
                frame.View = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(frame.Eye, frame.LookTarget, Vector3.UnitY);

                frame.LightDir = frame.WorldLightDir;
                if (frame.LightDir.LengthSquared() < 1e-8f)
                {
                    frame.LightDir = frame.Scene.Light.Direction.LengthSquared() < 1e-8f
                        ? new Vector3(-0.35f, -0.85f, -0.4f)
                        : Vector3.Normalize(frame.Scene.Light.Direction);
                }

                var lutSkyReady = _atmoLutsValid && _atmoSkyViewTex != 0;
                var drawSky = frame.Settings.EnableAtmosphericSky && _atmoQuadVao != 0 &&
                              (_atmoSkyProgram is { IsValid: true } || _proceduralSkyProgram is { IsValid: true });
                if (drawSky)
                {
                    frame.Gl.ClearColor(0.01f, 0.012f, 0.02f, 1f);
                }
                else
                {
                    frame.Gl.ClearColor(0.12f, 0.12f, 0.14f, 1f);
                }

                frame.Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                frame.DrewAtmosphereSky = false;
                if (drawSky)
                {
                    frame.Gl.Disable(EnableCap.DepthTest);
                    frame.Gl.DepthMask(false);
                    DrawAtmosphereSky(frame.Gl, ref frame, lutSkyReady);
                    frame.Gl.DepthMask(true);
                    frame.Gl.Enable(EnableCap.DepthTest);
                    frame.DrewAtmosphereSky = true;
                    frame.Gl.Clear(ClearBufferMask.DepthBufferBit);
                }

                // Sun disc + aureole are rendered by the sky shader (skySunDiscAureole); only the moon
                // remains a billboard, drawn before opaque geometry so depth testing hides it.
                frame.Gl.Enable(EnableCap.DepthTest);
                frame.Gl.DepthFunc(GLEnum.Lequal);
                DrawMoonBillboard(frame.Gl, frame.Proj, frame.View, frame.Eye, frame.LightDir,
                    frame.Settings.AtmosphereSunDiscStrength * 0.85f,
                    ShouldCullSolidBackFaces(frame.Scene.SceneKind, frame.BlockModel));

                _program.Use();
                SetMatrix("uView", frame.View);
                SetMatrix("uProj", frame.Proj);
                SetMatrix("uLightViewProj", frame.ShadowVp);

                SetVec3("uCameraPos", frame.Eye);
                SetVec3("uLightDir", frame.LightDir);
                SetVec3("uLightColor", frame.Scene.Light.Color);
                SetFloat("uAmbient", frame.Settings.AmbientStrength);
                SetFloat("uNormalStrength", frame.Settings.NormalStrength);
                SetFloat("uHeightStrength", frame.Settings.HeightStrength);
                SetFloat("uSpecularStrength", frame.Settings.SpecularStrength);
                SetFloat("uRoughnessScale", frame.Settings.RoughnessScale);
                SetFloat("uExposure", frame.Settings.Exposure);
                SetFloat("uParallaxAoStrength", frame.Settings.ParallaxAoStrength);
                SetInt("uEnableParallax", frame.EnableParallaxEff ? 1 : 0);
                SetInt("uEnableParallaxAo", frame.EnableParallaxAoEff ? 1 : 0);
                SetInt("uEnableNormalMap", frame.EnableNormalMapEff ? 1 : 0);
                SetInt("uEnableSpecularMap", frame.EnableSpecularMapEff ? 1 : 0);
                SetInt("uSceneKind", frame.Scene.SceneKind == PreviewSceneKind.ItemPlane ? 1 : 0);
                SetFloat("uAlphaCutoff", frame.Settings.AlphaCutoff);
                SetInt("uItemAlphaBlend", frame.Settings.ItemUseAlphaBlend ? 1 : 0);
                SetInt("uEntityAlphaMode", 0);

                // Genesis-specific uniforms.
                SetInt("uEnableSss", frame.Settings.EnableSss ? 1 : 0);
                SetInt("uEnableParallaxShadow", frame.EnableParallaxShadowEff ? 1 : 0);
                SetInt("uEnableIbl", frame.Settings.EnableIbl ? 1 : 0);
                SetFloat("uSssStrength", frame.Settings.SssStrength);
                SetFloat("uIblStrength", frame.Settings.IblStrength);
                SetFloat("uEmissionStrength", frame.Settings.EmissionStrength);
                SetInt("uEnableAtmosphericSky", frame.Settings.EnableAtmosphericSky ? 1 : 0);
                SetFloat("uAtmosphereTurbidity", frame.Settings.AtmosphereTurbidity);
                SetFloat("uAtmosphereSunIntensity", frame.Settings.AtmosphereSunIntensity);
                SetFloat("uAtmosphereHorizonFalloff", frame.Settings.AtmosphereHorizonFalloff);
                // Soft neutral sky/ground tints; future plan can expose these as user frame.Settings.
                SetVec3("uSkyTint", new Vector3(0.55f, 0.62f, 0.74f));
                SetVec3("uGroundTint", new Vector3(0.22f, 0.20f, 0.18f));

                // Directional shadow map (Genesis Shadows Phase 2). Bound to texture unit 4.
                var shadowEnabledForShader = frame.ShadowAvailable;
                SetInt("uEnableShadowMap", shadowEnabledForShader ? 1 : 0);
                SetFloat("uShadowMinBias", frame.Settings.ShadowMinBias);
                SetFloat("uShadowMaxBias", frame.Settings.ShadowMaxBias);
                var shadowRes = _shadowTarget?.Resolution ?? Math.Clamp(frame.Settings.ShadowMapResolution, 256, 4096);
                SetVec2("uShadowTexelSize", new Vector2(1f / shadowRes, 1f / shadowRes));
                if (_shadowTarget is not null)
                {
                    frame.Gl.ActiveTexture(TextureUnit.Texture4);
                    frame.Gl.BindTexture(TextureTarget.Texture2D, _shadowTarget.DepthTextureHandle);
                    SetInt("uShadowMap", 4);
                }

                // Tinted vanilla grass plane sits under the grid; one texture tile per world unit (nearest + repeat).
                if (frame.Settings.ShowGroundMesh &&
                    _grassGroundReady && _grassGroundAlbedo is not null && _groundMesh!.IndexCount > 0)
                {
                    var restoreCull = frame.Gl.IsEnabled(EnableCap.CullFace);
                    frame.Gl.Disable(EnableCap.CullFace);
                    SetMatrix("uModel", Matrix4x4.Identity);
                    SetInt("uEnableParallax", 0);
                    SetInt("uEnableNormalMap", 0);
                    SetInt("uEnableSpecularMap", 0);
                    SetInt("uSceneKind", 0);
                    SetInt("uEntityAlphaMode", 0);
                    ApplyEntitySkinningUniforms(_program, 0, 0, 0f);
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
                        frame.Gl.Enable(EnableCap.CullFace);
                    }
                }

                if (frame.Settings.ShowBackgroundGrid && _lineProgram?.IsValid == true &&
                    _gridVertexCount > 0)
                {
                    DrawBackgroundGrid(frame.Gl, frame.Proj, frame.View);
                    // DrawBackgroundGrid binds the line program; restore main frame.Material program before mesh uniforms.
                    _program.Use();
                }

                SetMatrix("uModel", frame.ModelMatrix);
                SetInt("uEnableParallax", frame.EnableParallaxEff ? 1 : 0);
                SetInt("uEnableNormalMap", frame.EnableNormalMapEff ? 1 : 0);
                SetInt("uEnableSpecularMap", frame.EnableSpecularMapEff ? 1 : 0);
                SetInt("uSceneKind", frame.Scene.SceneKind == PreviewSceneKind.ItemPlane ? 1 : 0);
                SetInt("uEntityAlphaMode", 0);
                if (_atmoSkyViewTex != 0)
                {
                    frame.Gl.ActiveTexture(TextureUnit.Texture5);
                    frame.Gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
                }

                SetInt("uAtmoSkyViewLut", 5);

                if (!frame.Settings.DrawPreviewSubject || _mesh.IndexCount <= 0)
                {
                    if (!_loggedZeroIndex && frame.Settings.DrawPreviewSubject && _mesh.IndexCount <= 0)
                    {
                        EmitDiagnostic(
                            $"[3D preview] Draw skipped: index buffer empty (frame.Scene={frame.Scene.SceneKind}, sceneMeshCount={frame.Scene.Meshes.Count}, frame.MeshDirty={frame.MeshDirty}).");
                        _loggedZeroIndex = true;
                    }
                }
                else if (frame.BlockModel is not null && frame.BlockSlots is { Length: > 0 })
                {
                    if (!_loggedMeshReady)
                    {
                        EmitDiagnostic(
                            $"[3D preview] Draw ready: indexCount={_mesh.IndexCount}, frame.Scene={frame.Scene.SceneKind}, lightYaw={frame.Settings.LightYawDegrees:F1}, lightPitch={frame.Settings.LightPitchDegrees:F1}.");
                        _loggedMeshReady = true;
                    }

                    SetInt("uEntityAlphaMode", frame.EntityAlphaModeUniform);
                    var blendWasEnabled = false;
                    if (frame.EntityBlendDraw)
                    {
                        blendWasEnabled = frame.Gl.IsEnabled(EnableCap.Blend);
                        frame.Gl.Enable(EnableCap.Blend);
                        frame.Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    }

                    foreach (var batch in frame.BlockModel.DrawBatches)
                    {
                        if ((uint)batch.MaterialIndex >= (uint)frame.BlockSlots.Length)
                        {
                            continue;
                        }

                        var slot = frame.BlockSlots[batch.MaterialIndex];
                        UploadMaterial(frame.Gl, slot, frame.Settings.NearestTextureFilter);
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
                        ApplyEntityBoneSkinningUniformsBeforeDraw(
                            _program,
                            _mainEntityUniformLocs,
                            frame.BlockModel,
                            frame.BlockModel.EntityGpuMeshSpaceLiftY,
                            frame.EntityBoneSnapshotValid,
                            frame.EntityBoneSnapshotCount,
                            frame.Settings.EnableEntityAnimation,
                            frame.EntityBonePaletteUploaded,
                            "main");
                        _mesh.DrawRange(batch.FirstIndex, batch.IndexCount);
                    }

                    if (frame.EntityBlendDraw && !blendWasEnabled)
                    {
                        frame.Gl.Disable(EnableCap.Blend);
                    }
                }
                else
                {
                    var hasN = frame.Material?.NormalRgba is { Length: > 0 };
                    var hasS = frame.Material?.SpecularRgba is { Length: > 0 };
                    var hasH = frame.Material?.HeightRgba is { Length: > 0 };
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
                            $"[3D preview] Draw ready: indexCount={_mesh.IndexCount}, frame.Scene={frame.Scene.SceneKind}, lightYaw={frame.Settings.LightYawDegrees:F1}, lightPitch={frame.Settings.LightPitchDegrees:F1}.");
                        _loggedMeshReady = true;
                    }

                    ApplyEntitySkinningUniforms(_program, 0, 0, 0f);
                    _mesh.Draw();
                }

                FinishGodRaySceneRender(ref frame);
    }
}
