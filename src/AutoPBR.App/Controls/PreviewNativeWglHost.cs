using AutoPBR.App.Rendering.OpenGL;

using Avalonia.Controls;
using Avalonia.Platform;

namespace AutoPBR.App.Controls;

internal sealed class PreviewNativeWglHost : NativeControlHost, IPreviewNativeWglInputSink
{
    public event Action<IntPtr>? NativeWindowCreated;
    public event Action<IntPtr>? NativeWindowDestroyed;
    public event Action? NativeWindowCreationFailed;
    public event Action<PreviewNativeWglMouseButton, PreviewNativeWglPointerEvent>? NativePointerPressed;
    public event Action<PreviewNativeWglPointerEvent>? NativePointerMoved;
    public event Action<PreviewNativeWglMouseButton, PreviewNativeWglPointerEvent>? NativePointerReleased;
    public event Action<PreviewNativeWglPointerEvent, int>? NativePointerWheel;
    public event Action<int>? NativeKeyDown;
    public event Action<int>? NativeKeyUp;
    public event Action? NativeInputLost;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (parent.Handle == IntPtr.Zero ||
            !string.Equals(parent.HandleDescriptor, "HWND", StringComparison.OrdinalIgnoreCase) ||
            !PreviewNativeWglChildWindow.TryCreate(parent.Handle, this, out var hwnd))
        {
            NativeWindowCreationFailed?.Invoke();
            return null!;
        }

        NativeWindowCreated?.Invoke(hwnd);
        return new PlatformHandle(hwnd, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        var hwnd = control.Handle;
        if (hwnd != IntPtr.Zero)
        {
            NativeWindowDestroyed?.Invoke(hwnd);
        }

        PreviewNativeWglChildWindow.Destroy(hwnd);
    }

    public void OnNativePointerPressed(PreviewNativeWglMouseButton button, PreviewNativeWglPointerEvent e) =>
        NativePointerPressed?.Invoke(button, e);

    public void OnNativePointerMoved(PreviewNativeWglPointerEvent e) =>
        NativePointerMoved?.Invoke(e);

    public void OnNativePointerReleased(PreviewNativeWglMouseButton button, PreviewNativeWglPointerEvent e) =>
        NativePointerReleased?.Invoke(button, e);

    public void OnNativePointerWheel(PreviewNativeWglPointerEvent e, int delta) =>
        NativePointerWheel?.Invoke(e, delta);

    public void OnNativeKeyDown(int virtualKey) => NativeKeyDown?.Invoke(virtualKey);

    public void OnNativeKeyUp(int virtualKey) => NativeKeyUp?.Invoke(virtualKey);

    public void OnNativeInputLost() => NativeInputLost?.Invoke();
}
