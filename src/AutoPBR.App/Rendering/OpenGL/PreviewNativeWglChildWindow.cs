using System.Runtime.InteropServices;

namespace AutoPBR.App.Rendering.OpenGL;

internal enum PreviewNativeWglMouseButton
{
    Left,
    Middle,
    Right
}

[Flags]
internal enum PreviewNativeWglMouseButtons
{
    None = 0,
    Left = 1,
    Middle = 2,
    Right = 4
}

[Flags]
internal enum PreviewNativeWglKeyModifiers
{
    None = 0,
    Alt = 1,
    Shift = 2,
    Control = 4
}

internal readonly record struct PreviewNativeWglPointerEvent(
    int X,
    int Y,
    PreviewNativeWglMouseButtons Buttons,
    PreviewNativeWglKeyModifiers Modifiers);

internal interface IPreviewNativeWglInputSink
{
    void OnNativePointerPressed(PreviewNativeWglMouseButton button, PreviewNativeWglPointerEvent e);

    void OnNativePointerMoved(PreviewNativeWglPointerEvent e);

    void OnNativePointerReleased(PreviewNativeWglMouseButton button, PreviewNativeWglPointerEvent e);

    void OnNativePointerWheel(PreviewNativeWglPointerEvent e, int delta);

    void OnNativeKeyDown(int virtualKey);

    void OnNativeKeyUp(int virtualKey);

    void OnNativeInputLost();
}

/// <summary>Win32 child window used as the direct WGL preview presentation target.</summary>
internal static class PreviewNativeWglChildWindow
{
    private const uint CsOwndc = 0x0000_0020;
    private const int WsChild = 0x4000_0000;
    private const int WsVisible = 0x1000_0000;
    private const int WsClipChildren = 0x0200_0000;
    private const int WsClipSiblings = 0x0400_0000;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmMouseMove = 0x0200;
    private const uint WmLButtonDown = 0x0201;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmMButtonUp = 0x0208;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmNcHitTest = 0x0084;
    private const uint WmEraseBkgnd = 0x0014;
    private const uint WmCaptureChanged = 0x0215;
    private const uint WmKillFocus = 0x0008;
    private const int HtClient = 1;
    private const int MkLButton = 0x0001;
    private const int MkRButton = 0x0002;
    private const int MkShift = 0x0004;
    private const int MkControl = 0x0008;
    private const int MkMButton = 0x0010;
    private const int VkMenu = 0x12;

    private static readonly object Gate = new();
    private static readonly Dictionary<IntPtr, IPreviewNativeWglInputSink> InputSinks = [];
    private static readonly WndProcDelegate WndProc = WndProcCore;
    private static bool _classRegistered;

    public static bool TryCreate(IntPtr parent, IPreviewNativeWglInputSink inputSink, out IntPtr hwnd)
    {
        hwnd = IntPtr.Zero;
        if (!OperatingSystem.IsWindows() || parent == IntPtr.Zero || !EnsureClassRegistered())
        {
            return false;
        }

        hwnd = CreateWindowExW(
            0,
            ClassName,
            "AutoPBR WGL Preview",
            WsChild | WsVisible | WsClipChildren | WsClipSiblings,
            0,
            0,
            1,
            1,
            parent,
            IntPtr.Zero,
            GetModuleHandleW(IntPtr.Zero),
            IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        lock (Gate)
        {
            InputSinks[hwnd] = inputSink;
        }

        return true;
    }

    public static void Destroy(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            lock (Gate)
            {
                InputSinks.Remove(hwnd);
            }

            if (GetCapture() == hwnd)
            {
                _ = ReleaseCapture();
            }

            _ = DestroyWindow(hwnd);
        }
    }

    private const string ClassName = "AutoPBR.NativeWglPreview";

    private static bool EnsureClassRegistered()
    {
        if (_classRegistered)
        {
            return true;
        }

        lock (Gate)
        {
            if (_classRegistered)
            {
                return true;
            }

            var wc = new WndClass
            {
                Style = CsOwndc,
                LpfnWndProc = WndProc,
                HInstance = GetModuleHandleW(IntPtr.Zero),
                LpszClassName = ClassName,
            };
            _classRegistered = RegisterClassW(ref wc) != 0;
            return _classRegistered;
        }
    }

    private static IntPtr WndProcCore(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmNcHitTest)
        {
            return HtClient;
        }

        if (msg == WmEraseBkgnd)
        {
            return 1;
        }

        if (TryGetInputSink(hWnd, out var sink))
        {
            switch (msg)
            {
                case WmLButtonDown:
                    SetCapture(hWnd);
                    sink.OnNativePointerPressed(PreviewNativeWglMouseButton.Left, BuildPointerEvent(wParam, lParam));
                    return IntPtr.Zero;
                case WmMButtonDown:
                    SetCapture(hWnd);
                    sink.OnNativePointerPressed(PreviewNativeWglMouseButton.Middle, BuildPointerEvent(wParam, lParam));
                    return IntPtr.Zero;
                case WmRButtonDown:
                    SetCapture(hWnd);
                    sink.OnNativePointerPressed(PreviewNativeWglMouseButton.Right, BuildPointerEvent(wParam, lParam));
                    return IntPtr.Zero;
                case WmMouseMove:
                    sink.OnNativePointerMoved(BuildPointerEvent(wParam, lParam));
                    return IntPtr.Zero;
                case WmLButtonUp:
                    sink.OnNativePointerReleased(PreviewNativeWglMouseButton.Left, BuildPointerEvent(wParam, lParam));
                    ReleaseCaptureIfNoButtons(hWnd, wParam);
                    return IntPtr.Zero;
                case WmMButtonUp:
                    sink.OnNativePointerReleased(PreviewNativeWglMouseButton.Middle, BuildPointerEvent(wParam, lParam));
                    ReleaseCaptureIfNoButtons(hWnd, wParam);
                    return IntPtr.Zero;
                case WmRButtonUp:
                    sink.OnNativePointerReleased(PreviewNativeWglMouseButton.Right, BuildPointerEvent(wParam, lParam));
                    ReleaseCaptureIfNoButtons(hWnd, wParam);
                    return IntPtr.Zero;
                case WmMouseWheel:
                    sink.OnNativePointerWheel(BuildWheelPointerEvent(hWnd, wParam, lParam), SignedHighWord(wParam));
                    return IntPtr.Zero;
                case WmKeyDown:
                case WmSysKeyDown:
                    sink.OnNativeKeyDown(LowWord(wParam));
                    return IntPtr.Zero;
                case WmKeyUp:
                case WmSysKeyUp:
                    sink.OnNativeKeyUp(LowWord(wParam));
                    return IntPtr.Zero;
                case WmCaptureChanged:
                case WmKillFocus:
                    sink.OnNativeInputLost();
                    break;
            }
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static bool TryGetInputSink(IntPtr hwnd, out IPreviewNativeWglInputSink sink)
    {
        lock (Gate)
        {
            return InputSinks.TryGetValue(hwnd, out sink!);
        }
    }

    private static PreviewNativeWglPointerEvent BuildPointerEvent(IntPtr wParam, IntPtr lParam)
    {
        var flags = LowWord(wParam);
        return new PreviewNativeWglPointerEvent(
            SignedLowWord(lParam),
            SignedHighWord(lParam),
            GetButtons(flags),
            GetModifiers(flags));
    }

    private static PreviewNativeWglPointerEvent BuildWheelPointerEvent(IntPtr hwnd, IntPtr wParam, IntPtr lParam)
    {
        var point = new Point32(SignedLowWord(lParam), SignedHighWord(lParam));
        _ = ScreenToClient(hwnd, ref point);
        var flags = LowWord(wParam);
        return new PreviewNativeWglPointerEvent(point.X, point.Y, GetButtons(flags), GetModifiers(flags));
    }

    private static PreviewNativeWglMouseButtons GetButtons(int flags)
    {
        var buttons = PreviewNativeWglMouseButtons.None;
        if ((flags & MkLButton) != 0)
        {
            buttons |= PreviewNativeWglMouseButtons.Left;
        }

        if ((flags & MkMButton) != 0)
        {
            buttons |= PreviewNativeWglMouseButtons.Middle;
        }

        if ((flags & MkRButton) != 0)
        {
            buttons |= PreviewNativeWglMouseButtons.Right;
        }

        return buttons;
    }

    private static PreviewNativeWglKeyModifiers GetModifiers(int flags)
    {
        var modifiers = PreviewNativeWglKeyModifiers.None;
        if ((GetKeyState(VkMenu) & 0x8000) != 0)
        {
            modifiers |= PreviewNativeWglKeyModifiers.Alt;
        }

        if ((flags & MkShift) != 0)
        {
            modifiers |= PreviewNativeWglKeyModifiers.Shift;
        }

        if ((flags & MkControl) != 0)
        {
            modifiers |= PreviewNativeWglKeyModifiers.Control;
        }

        return modifiers;
    }

    private static void ReleaseCaptureIfNoButtons(IntPtr hwnd, IntPtr wParam)
    {
        var buttons = GetButtons(LowWord(wParam));
        if (buttons == PreviewNativeWglMouseButtons.None && GetCapture() == hwnd)
        {
            _ = ReleaseCapture();
        }
    }

    private static int LowWord(IntPtr value) => unchecked((int)(value.ToInt64() & 0xffffL));

    private static int SignedLowWord(IntPtr value) => unchecked((short)(value.ToInt64() & 0xffffL));

    private static int SignedHighWord(IntPtr value) => unchecked((short)((value.ToInt64() >> 16) & 0xffffL));

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClass
    {
        public uint Style;
        public WndProcDelegate LpfnWndProc;
        public int CbClsExtra;
        public int CbWndExtra;
        public IntPtr HInstance;
        public IntPtr HIcon;
        public IntPtr HCursor;
        public IntPtr HbrBackground;
        public string? LpszMenuName;
        public string LpszClassName;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WndClass lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(IntPtr lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCapture(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetCapture();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr hWnd, ref Point32 lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;

        public Point32(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
