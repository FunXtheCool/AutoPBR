using System.Diagnostics;
using System.Numerics;
using System.Threading;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Avalonia.Threading;

namespace AutoPBR.App.Controls;

/// <summary>
/// OpenGL PBR preview. Implements <see cref="ICustomHitTest"/> so pointer events are delivered on Avalonia 11+
/// (composition GPU surfaces otherwise skip normal hit-testing on <see cref="OpenGlControlBase"/>).
/// </summary>
public sealed class GlPbrPreviewControl : OpenGlControlBase, ICustomHitTest, IDisposable
{
    private readonly OpenGlPreviewBackend _backend = new();
    private long _lastTicks = Stopwatch.GetTimestamp();
    private double _fpsAccumSeconds;
    private int _fpsFrameCount;
    private double _smoothedFps;
    private bool _capFpsAt60;
    private long _lastCapFrameTicks;
    private int _capScheduleGeneration;
    private bool _disposed;
    private const double CapFrameIntervalSeconds = 1.0 / 60.0;
    private Key _cameraResetKey = Key.R;
    private bool _cameraDrag;
    private bool _cameraDragIsOrbit;
    private bool _debugFlyRmbLook;
    private bool _flyKeyW;
    private bool _flyKeyA;
    private bool _flyKeyS;
    private bool _flyKeyD;
    private bool _flyKeyQ;
    private bool _flyKeyE;
    private bool _flySpeedBoost;
    private bool _flySpeedSlow;
    private Point _cameraDragLast;
    private DateTime _lastLeftClickUtc = DateTime.MinValue;
    private Point _lastLeftClickPos;
    private const int DoubleClickMs = 400;
    private const double DoubleClickMaxDist = 8;

    public GlPbrPreviewControl()
    {
        ClipToBounds = true;
        Focusable = true;
        IsTabStop = false;
        PointerPressed += OnPreviewPointerPressed;
        PointerMoved += OnPreviewPointerMoved;
        PointerReleased += OnPreviewPointerReleased;
        PointerWheelChanged += OnPreviewPointerWheelChanged;
        PointerEntered += OnPreviewPointerEntered;
        PointerCaptureLost += OnPointerCaptureLost;
    }

    /// <inheritdoc />
    public bool HitTest(Point point) =>
        Bounds is { Width: > 0, Height: > 0 } && new Rect(Bounds.Size).Contains(point);

    public IRenderPreviewBackend Backend => _backend;

    /// <summary>Frames per second averaged over the last ~0.5s of rendered preview frames.</summary>
    public double SmoothedPreviewFps => Volatile.Read(ref _smoothedFps);

    public bool TryGetPreviewViewportInfo(out int pixelWidth, out int pixelHeight,
        out double logicalWidth, out double logicalHeight, out double renderScale)
    {
        renderScale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        logicalWidth = Bounds.Width;
        logicalHeight = Bounds.Height;
        pixelWidth = Math.Max(1, (int)Math.Ceiling(logicalWidth * renderScale));
        pixelHeight = Math.Max(1, (int)Math.Ceiling(logicalHeight * renderScale));
        return logicalWidth > 0.0 && logicalHeight > 0.0;
    }

    /// <summary>Routes shader and GL init messages into the main app log (invoked from the GL thread).</summary>
    public void SetRendererLog(Action<string>? log) => _backend.SetDiagnosticLog(log);

    public void InvalidateShaderCaches() => _backend.InvalidateShaderCachesAndReload();

    /// <summary>When true, continuous preview rendering is limited to 60 FPS. Off by default (uncapped).</summary>
    public void SetPreviewFrameRateCap(bool capAt60)
    {
        if (_capFpsAt60 == capAt60)
        {
            return;
        }

        _capFpsAt60 = capAt60;
        Interlocked.Increment(ref _capScheduleGeneration);
        RequestNextFrameRendering();
    }

    /// <summary>Updates orbit boom arm length, orbit/pan/zoom sensitivities, and the reset-camera key.</summary>
    public void SetCameraInteractionFromSettings(float orbitRadPerPx, float panPerPixel, float zoomPerWheelStep,
        float flyLookRadPerPx, bool invertLookY, float flyMoveSpeed, bool flySmoothAcceleration,
        Key resetKey, float orbitBoomArmDistanceWorld)
    {
        _cameraResetKey = resetKey;
        _backend.SetCameraSensitivities(orbitRadPerPx, panPerPixel, zoomPerWheelStep, flyLookRadPerPx, invertLookY,
            flyMoveSpeed, flySmoothAcceleration);
        _backend.SetOrbitBoomArmDistance(orbitBoomArmDistanceWorld);
    }

    /// <summary>Updates render settings only (no scene/block-model re-push).</summary>
    public void UpdatePreview3DSettings(PreviewRenderSettings settings)
    {
        void Core()
        {
            _backend.SetRenderSettings(settings);
            RequestNextFrameRendering();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    /// <summary>Updates scene, GPU settings, and material on the UI thread.</summary>
    public void UpdatePreview3D(
        PreviewMaterial material,
        PreviewRenderSettings settings,
        PreviewSceneKind kind,
        PreviewModelSubject? javaBlockModel = null,
        PreviewMaterial[]? javaSlotMaterials = null)
    {
        void Core()
        {
            PreviewScene scene;
            if (javaBlockModel is not null && javaSlotMaterials is not null)
            {
                var displaySubject = PreviewSubjectPlacement.LiftSubjectIfClipping(javaBlockModel);
                var stride = displaySubject.VertexStrideFloats > 0
                    ? displaySubject.VertexStrideFloats
                    : PreviewMesh.FloatsPerVertex;
                var orbitTarget = displaySubject.EmulatedRebake is not null
                    ? EntityPreviewPlacement.ComputeEntityOrbitTarget(displaySubject.InterleavedVertices, stride)
                    : (Vector3?)null;
                // Emulated entities upload geometry only via OpenGL TryRebakeMesh commit — never from scene mesh.
                var mesh = displaySubject.EmulatedRebake is not null
                    ? PreviewMeshFactory.CreateEmptySubjectPlaceholder("entity_rebake_pending")
                    : new PreviewMesh
                    {
                        Name = "java_block_model",
                        InterleavedVertices = displaySubject.InterleavedVertices,
                        Indices = displaySubject.Indices
                    };
                scene = BlockModelPreviewSceneFactory.Create(settings, mesh, orbitTarget);
                javaBlockModel = displaySubject;
            }
            else if (kind == PreviewSceneKind.ItemPlane)
            {
                scene = ItemPreviewSceneFactory.Create(settings, material);
            }
            else
            {
                scene = BlockPreviewSceneFactory.Create(settings);
            }

            _backend.SetScene(scene);
            _backend.SetRenderSettings(settings);
            _backend.SetMaterial(material);
            _backend.SetBlockModelPreview(javaBlockModel, javaSlotMaterials);
            RequestNextFrameRendering();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    /// <summary>Updates the LabPBR ground plane material (grass_block_top).</summary>
    public void SetGroundMaterial(PreviewMaterial? material)
    {
        void Core()
        {
            _backend.SetGroundMaterial(material);
            RequestNextFrameRendering();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Core();
        }
        else
        {
            Dispatcher.UIThread.Post(Core);
        }
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        _backend.Initialize(new RenderPreviewInitializationOptions());
        _backend.GlInit(gl);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _backend.GlDeinit(gl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var now = Stopwatch.GetTimestamp();
        var dt = TimeSpan.FromTicks(now - _lastTicks);
        _lastTicks = now;
        PushDebugFlyInput();
        _backend.RenderFrame(dt);
        RecordRenderFrame(dt);
        TryGetPreviewViewportInfo(out var w, out var h, out _, out _, out _);
        _backend.GlRender(gl, fb, w, h);
        _lastCapFrameTicks = now;
        RequestContinuousFrameIfNeeded();
    }

    private void RequestContinuousFrameIfNeeded()
    {
        if (!_backend.NeedsContinuousRendering)
        {
            return;
        }

        if (!_capFpsAt60)
        {
            RequestNextFrameRendering();
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var elapsed = (now - _lastCapFrameTicks) / (double)Stopwatch.Frequency;
        if (elapsed >= CapFrameIntervalSeconds)
        {
            RequestNextFrameRendering();
            return;
        }

        var delayMs = Math.Max(1, (int)Math.Ceiling((CapFrameIntervalSeconds - elapsed) * 1000.0));
        var generation = Volatile.Read(ref _capScheduleGeneration);
        _ = ScheduleCappedFrameAsync(generation, delayMs);
    }

    private async Task ScheduleCappedFrameAsync(int generation, int delayMs)
    {
        await Task.Delay(delayMs).ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (generation != Volatile.Read(ref _capScheduleGeneration) ||
                !_capFpsAt60 ||
                !_backend.NeedsContinuousRendering)
            {
                return;
            }

            RequestNextFrameRendering();
        });
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != BoundsProperty)
        {
            return;
        }

        TryGetPreviewViewportInfo(out var w, out var h, out _, out _, out _);
        _backend.Resize(w, h);
        RequestNextFrameRendering();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == _cameraResetKey)
        {
            _backend.ResetPreviewCameraToDefaults();
            RequestNextFrameRendering();
            e.Handled = true;
        }
        else
        {
            switch (e.Key)
            {
                case Key.W:
                    _flyKeyW = true;
                    e.Handled = true;
                    break;
                case Key.A:
                    _flyKeyA = true;
                    e.Handled = true;
                    break;
                case Key.S:
                    _flyKeyS = true;
                    e.Handled = true;
                    break;
                case Key.D:
                    _flyKeyD = true;
                    e.Handled = true;
                    break;
                case Key.Q:
                    _flyKeyQ = true;
                    e.Handled = true;
                    break;
                case Key.E:
                    _flyKeyE = true;
                    e.Handled = true;
                    break;
                case Key.LeftShift:
                case Key.RightShift:
                    _flySpeedBoost = true;
                    e.Handled = true;
                    break;
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    _flySpeedSlow = true;
                    e.Handled = true;
                    break;
            }
        }

        if (e.Handled)
        {
            RequestNextFrameRendering();
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.W:
                _flyKeyW = false;
                e.Handled = true;
                break;
            case Key.A:
                _flyKeyA = false;
                e.Handled = true;
                break;
            case Key.S:
                _flyKeyS = false;
                e.Handled = true;
                break;
            case Key.D:
                _flyKeyD = false;
                e.Handled = true;
                break;
            case Key.Q:
                _flyKeyQ = false;
                e.Handled = true;
                break;
            case Key.E:
                _flyKeyE = false;
                e.Handled = true;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                _flySpeedBoost = false;
                e.Handled = true;
                break;
            case Key.LeftCtrl:
            case Key.RightCtrl:
                _flySpeedSlow = false;
                e.Handled = true;
                break;
        }

        if (e.Handled)
        {
            RequestNextFrameRendering();
        }

        base.OnKeyUp(e);
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _cameraDrag = false;
        _debugFlyRmbLook = false;
        PushDebugFlyInput();
        _backend.SetUserCameraDragging(false);
    }

    private void PushDebugFlyInput() =>
        _backend.SetDebugFlyInput(_debugFlyRmbLook, _flyKeyW, _flyKeyA, _flyKeyS, _flyKeyD, _flyKeyQ, _flyKeyE,
            _flySpeedBoost, _flySpeedSlow);

    private void OnPreviewPointerEntered(object? sender, PointerEventArgs e)
    {
        Focus();
    }

    private static bool IsOrbitPanDragStart(PointerPoint p, KeyModifiers keys, out bool orbit)
    {
        var props = p.Properties;
        var left = props.IsLeftButtonPressed;
        var middle = props.IsMiddleButtonPressed;
        if (!left && !middle)
        {
            orbit = false;
            return false;
        }

        orbit = keys.HasFlag(KeyModifiers.Alt);
        return true;
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsRightButtonPressed)
        {
            _debugFlyRmbLook = true;
            _cameraDragLast = e.GetPosition(this);
            PushDebugFlyInput();
            _backend.SetUserCameraDragging(true);
            e.Pointer.Capture(this);
            e.Handled = true;
            RequestNextFrameRendering();
            return;
        }

        if (!IsOrbitPanDragStart(pt, e.KeyModifiers, out var orbit))
        {
            return;
        }

        var pressPos = e.GetPosition(this);
        var now = DateTime.UtcNow;
        var clickDx = pressPos.X - _lastLeftClickPos.X;
        var clickDy = pressPos.Y - _lastLeftClickPos.Y;
        if (pt.Properties.IsLeftButtonPressed &&
            (now - _lastLeftClickUtc).TotalMilliseconds <= DoubleClickMs &&
            clickDx * clickDx + clickDy * clickDy <= DoubleClickMaxDist * DoubleClickMaxDist)
        {
            _backend.FocusOrbitOnSubject();
            _lastLeftClickUtc = DateTime.MinValue;
            e.Handled = true;
            RequestNextFrameRendering();
            return;
        }

        if (pt.Properties.IsLeftButtonPressed)
        {
            _lastLeftClickUtc = now;
            _lastLeftClickPos = pressPos;
        }

        _cameraDrag = true;
        _cameraDragIsOrbit = orbit;
        _cameraDragLast = e.GetPosition(this);
        _backend.SetUserCameraDragging(true);
        e.Pointer.Capture(this);
        e.Handled = true;
        RequestNextFrameRendering();
    }

    private void OnPreviewPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_debugFlyRmbLook)
        {
            var cur = e.GetPosition(this);
            var dx = (float)(cur.X - _cameraDragLast.X);
            var dy = (float)(cur.Y - _cameraDragLast.Y);
            _cameraDragLast = cur;
            _backend.ApplyFlyLookPixels(dx, dy);
            e.Handled = true;
            RequestNextFrameRendering();
            return;
        }

        if (!_cameraDrag)
        {
            return;
        }

        var cur2 = e.GetPosition(this);
        var dx2 = (float)(cur2.X - _cameraDragLast.X);
        var dy2 = (float)(cur2.Y - _cameraDragLast.Y);
        _cameraDragLast = cur2;
        if (_cameraDragIsOrbit)
        {
            _backend.ApplyCameraOrbitPixels(dx2, dy2);
        }
        else
        {
            _backend.ApplyCameraPanPixels(dx2, dy2);
        }

        e.Handled = true;
        RequestNextFrameRendering();
    }

    private void OnPreviewPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right)
        {
            _debugFlyRmbLook = false;
            PushDebugFlyInput();
            _backend.SetUserCameraDragging(false);
            e.Pointer.Capture(null);
            e.Handled = true;
            RequestNextFrameRendering();
            return;
        }

        if (e.InitialPressMouseButton is not (MouseButton.Left or MouseButton.Middle))
        {
            return;
        }

        _cameraDrag = false;
        _backend.SetUserCameraDragging(false);
        e.Pointer.Capture(null);
        e.Handled = true;
        RequestNextFrameRendering();
    }

    private void OnPreviewPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var dy = e.Delta.Y;
        if (Math.Abs(dy) < double.Epsilon)
        {
            return;
        }

        var notches = (float)(dy / 120.0);
        if (Math.Abs(notches) < 0.08f)
        {
            notches = Math.Sign(notches);
        }

        _backend.ApplyCameraZoom(notches);
        e.Handled = true;
        RequestNextFrameRendering();
    }

    private void RecordRenderFrame(TimeSpan dt)
    {
        _fpsAccumSeconds += dt.TotalSeconds;
        _fpsFrameCount++;
        if (_fpsAccumSeconds < 0.5)
        {
            return;
        }

        Volatile.Write(ref _smoothedFps, _fpsFrameCount / _fpsAccumSeconds);
        _fpsAccumSeconds = 0;
        _fpsFrameCount = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backend.Dispose();
    }
}
