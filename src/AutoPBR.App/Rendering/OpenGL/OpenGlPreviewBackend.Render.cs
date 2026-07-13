using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using AutoPBR.App.Lang;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

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
        if (IsAwaitingDesktopWglSidecar)
        {
            return;
        }

        PreviewDesktopWglContext? sidecar;
        lock (_sync)
        {
            sidecar = _desktopWglSidecar;
        }

        if (sidecar is not null)
        {
            PreviewOpenGlCompositionBridge? compositionBridge;
            lock (_sync)
            {
                compositionBridge = _compositionBridge;
            }

            if (compositionBridge is not null &&
                sidecar is not null &&
                IsSidecarAdapterMatchComplete &&
                sidecar.CanAttemptDxInterop &&
                sidecar.TryRenderViaDxInterop(
                    compositionBridge,
                    glInterface,
                    framebuffer,
                    pixelWidth,
                    pixelHeight,
                    fbo => GlRenderCore(fbo, pixelWidth, pixelHeight)))
            {
                lock (_sync)
                {
                    if (!_dxInteropSuccessLogged)
                    {
                        _dxInteropSuccessLogged = true;
                        sidecar.EnableDxInteropHangDiagnostics(EmitDiagnostic, RequestPreviewFrame);
                        EmitDiagnostic("[3D preview] D3D11/WGL interop active; async GPU present (timed mutex + timed GPU drain).");
                        RecordActiveContextSummary();
                    }
                }

                return;
            }

            if (compositionBridge is not null)
            {
                lock (_sync)
                {
                    if (!_dxInteropFallbackLogged)
                    {
                        _dxInteropFallbackLogged = true;
                        EmitDiagnostic(sidecar.DxInteropOptInEnabled
                            ? "[3D preview] D3D11/WGL interop unavailable; using async PBO sidecar presentation. " +
                              sidecar.LastInteropFailureSummary
                            : "[3D preview] D3D11/WGL interop skipped; using stable async PBO sidecar presentation.");
                    }
                }
            }

            var forceSyncPresent = false;
            if (sidecar.IsOwnerThreadLikelyWedged)
            {
                ClearPresentationFramebuffer(glInterface, framebuffer, pixelWidth, pixelHeight);
                return;
            }

            try
            {
                sidecar.Invoke(() =>
                {
                    using (sidecar.BindOnOwnerThread())
                    {
                        sidecar.EnsureRenderTargetCore(pixelWidth, pixelHeight);
                        GlRenderCore(sidecar.RenderFbo, pixelWidth, pixelHeight);
                    }
                }, TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                EmitDiagnostic("[3D preview] Sidecar WGL render timed out (owner thread likely wedged); skipping frame.");
                ClearPresentationFramebuffer(glInterface, framebuffer, pixelWidth, pixelHeight);
                return;
            }
            catch (Exception ex)
            {
                EmitDiagnostic($"[3D preview] Sidecar WGL render failed: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            lock (_sync)
            {
                if (_forceSyncSidecarPresent)
                {
                    forceSyncPresent = true;
                    _forceSyncSidecarPresent = false;
                }
            }

            try
            {
                sidecar.CopyColorToEsFbo(glInterface, framebuffer, pixelWidth, pixelHeight, forceSyncPresent);
            }
            catch (Exception ex)
            {
                EmitDiagnostic($"[3D preview] Sidecar CPU presentation failed: {ex.GetType().Name}: {ex.Message}");
                return;
            }
            if (sidecar.UsesAsyncPboReadback)
            {
                lock (_sync)
                {
                    if (!_asyncPboReadbackLogged)
                    {
                        _asyncPboReadbackLogged = true;
                        EmitDiagnostic("[3D preview] Async PBO readback active for sidecar CPU presentation fallback.");
                    }
                }
            }

            return;
        }

        GlRenderCore(framebuffer, pixelWidth, pixelHeight);
    }

    private void GlRenderCore(int framebuffer, int pixelWidth, int pixelHeight)
    {
        PreviewRenderSettingsSnapshot settings;
        int settingsRevision;
        IRenderPreviewScene? scene;
        PreviewMaterial? material;
        PreviewModelSubject? blockModel;
        PreviewMaterial[]? blockSlots;
        double rotation;
        double renderTime;
        Vector3 orbitBaseTarget;
        Vector3 orbitPan;
        bool flyCamActive;
        Vector3 flyPosition;
        float flyYaw;
        float flyPitch;
        float orbitYaw;
        float orbitPitch;
        float orbitDistance;
        var drawBootstrapOnly = false;
        var previewPixelWidth = 0;
        var previewPixelHeight = 0;
        var meshDirty = false;
        var materialDirty = false;
        lock (_sync)
        {
            if (_gl is null)
            {
                return;
            }

            settings = _settings;
            settingsRevision = _settingsRevision;
            _previewPixelWidth = Math.Max(1, pixelWidth);
            _previewPixelHeight = Math.Max(1, pixelHeight);
            previewPixelWidth = _previewPixelWidth;
            previewPixelHeight = _previewPixelHeight;
            meshDirty = _meshDirty;
            materialDirty = _materialDirty;

            HandlePendingShaderReloadLocked();
            if (_gpuBootstrap is not null && _desktopWglSidecar is null)
            {
                if (!_gpuBootstrap.IsComplete)
                {
                    _gpuBootstrap.Advance(this, 14.0);
                }

                // Advance may abort bootstrap (e.g. shader link failure) and clear the runner.
                var bootstrap = _gpuBootstrap;
                if (bootstrap is not null)
                {
                    var phase = bootstrap.IsComplete ? PreviewGpuInitPhases.CoreReady : bootstrap.Phase;
                    RaiseGpuInitProgress(phase, settings);
                    if (bootstrap.IsComplete || _gpuBootstrapAborted)
                    {
                        _gpuBootstrap = null;
                        _gpuBootstrapAborted = false;
                    }
                }
            }
            else if (_gpuBootstrap is not null && _desktopWglSidecar is not null)
            {
                var bootstrap = _gpuBootstrap;
                if (bootstrap is not null)
                {
                    RaiseGpuInitProgress(bootstrap.Phase, settings);
                }
            }

            drawBootstrapOnly = !_gpuAlive || _gpuBootstrap is not null;
            if (drawBootstrapOnly)
            {
                scene = _scene;
                material = null;
                blockModel = null;
                blockSlots = null;
                rotation = 0;
                renderTime = _renderTimeAccum;
                orbitBaseTarget = default;
                orbitPan = default;
                flyCamActive = false;
                flyPosition = default;
                flyYaw = 0;
                flyPitch = 0;
                orbitYaw = 0;
                orbitPitch = 0;
                orbitDistance = 0;
            }
            else if (_program is null || !_program.IsValid || _albedo is null ||
                     _normal is null || _spec is null || _height is null || _mesh is null || _groundMesh is null ||
                     _grassGroundAlbedo is null || _grassGroundNormal is null || _grassGroundSpec is null ||
                     _grassGroundHeight is null || _neutralNormal is null || _neutralSpec is null ||
                     _neutralHeight is null)
            {
                return;
            }
            else
            {
                scene = _scene;
                material = _material;
                blockModel = _blockModelSubject;
                blockSlots = _blockModelSlots;
                rotation = _rotationAccum;
                renderTime = _renderTimeAccum;
                orbitBaseTarget = _orbitBaseTarget;
                orbitPan = _orbitPan;
                flyCamActive = _debugFlyRmbHeld && _flyEngaged;
                flyPosition = _flyPosition;
                flyYaw = _flyYaw;
                flyPitch = _flyPitch;
                orbitYaw = _orbitYaw;
                orbitPitch = _orbitPitch;
                orbitDistance = _orbitDistance;
            }
        }

        var gl = _gl!;
        var defaultFbo = framebuffer;
        var vpX = 0;
        var vpY = 0;
        var vw = previewPixelWidth;
        var vh = previewPixelHeight;

        if (defaultFbo != 0)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)defaultFbo);
        }
        else
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        ConfigureDefaultFramebufferColorOutput(gl, defaultFbo);
        gl.Viewport(vpX, vpY, (uint)vw, (uint)vh);
        gl.Disable(EnableCap.ScissorTest);

        if (drawBootstrapOnly)
        {
            gl.ClearColor(0.01f, 0.012f, 0.02f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            return;
        }

        EnsureGpuTier(settings);

        if (scene is null)
        {
            gl.ClearColor(0.12f, 0.12f, 0.14f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            return;
        }

        var frame = new GlRenderFrame
        {
            Gl = gl,
            DefaultFbo = defaultFbo,
            VpX = vpX,
            VpY = vpY,
            Vw = vw,
            Vh = vh,
            Settings = settings,
            SettingsRevision = settingsRevision,
            Scene = scene,
            Material = material,
            BlockModel = blockModel,
            BlockSlots = blockSlots,
            Rotation = rotation,
            RenderTime = renderTime,
            OrbitBaseTarget = orbitBaseTarget,
            OrbitPan = orbitPan,
            FlyCamActive = flyCamActive,
            FlyPosition = flyPosition,
            FlyYaw = flyYaw,
            FlyPitch = flyPitch,
            OrbitYaw = orbitYaw,
            OrbitPitch = orbitPitch,
            OrbitDistance = orbitDistance,
            MeshDirty = meshDirty,
            MaterialDirty = materialDirty,
        };

        GlRenderPassSetup(ref frame);
        GlRenderPassShadow(ref frame);
        GlRenderPassScene(ref frame);
        GlRenderPassPost(ref frame);
    }

    private static void ClearPresentationFramebuffer(GlInterface glInterface, int framebuffer, int width, int height)
    {
        var esGl = GL.GetApi(glInterface.GetProcAddress);
        esGl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)framebuffer);
        esGl.Viewport(0, 0, (uint)Math.Max(1, width), (uint)Math.Max(1, height));
        esGl.ClearColor(0.01f, 0.012f, 0.02f, 1f);
        esGl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }
}
