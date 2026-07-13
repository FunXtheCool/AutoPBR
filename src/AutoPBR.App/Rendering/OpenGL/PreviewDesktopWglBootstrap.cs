using System.Runtime.InteropServices;

using Avalonia.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Creates desktop WGL contexts without Avalonia's <c>WglDisplay</c> (which requires the UI thread).
/// </summary>
internal static class PreviewDesktopWglBootstrap
{
    private const int WglContextMajorVersionArb = 0x2091;
    private const int WglContextMinorVersionArb = 0x2092;
    private const int WglContextProfileMaskArb = 0x9126;
    private const int WglContextCoreProfileBitArb = 0x0000_0001;
    private const uint WglDrawToWindowArb = 0x2001;
    private const uint WglAccelerationArb = 0x2003;
    private const uint WglFullAccelerationArb = 0x2027;
    private const uint WglSupportOpenglArb = 0x2010;
    private const uint WglDoubleBufferArb = 0x2011;
    private const uint WglPixelTypeArb = 0x2013;
    private const uint WglTypeRgbaArb = 0x202B;
    private const uint WglColorBitsArb = 0x2014;
    private const uint WglAlphaBitsArb = 0x201B;
    private const uint WglDepthBitsArb = 0x2022;
    private const uint WglStencilBitsArb = 0x2023;

    private const uint CsOwndc = 0x0000_0020;
    private const int WsPopup = unchecked((int)0x8000_0000);
    private const int WsExToolwindow = 0x0000_0080;

    private static readonly object BootstrapLock = new();
    private static IntPtr _openGl32Handle;
    private static WglCreateContextAttribsArbDelegate? _createContextAttribsArb;
    private static WglChoosePixelFormatArbDelegate? _choosePixelFormatArb;
    private static int _defaultPixelFormat;
    private static PixelFormatDescriptor _defaultPfd;
    private static bool _bootstrapReady;

    public static IntPtr OpenGl32Handle => EnsureOpenGl32Handle();

    public static IGlContext? TryCreateContext(IReadOnlyList<GlVersion> profiles, Action<string>? log = null)
    {
        if (!OperatingSystem.IsWindows() || !EnsureBootstrapReady())
        {
            return null;
        }

        var window = CreateHiddenWindow();
        if (window == IntPtr.Zero)
        {
            log?.Invoke("[3D preview] WGL bootstrap hidden window creation failed.");
            return null;
        }

        var dc = GetDC(window);
        if (dc == IntPtr.Zero)
        {
            DestroyWindow(window);
            log?.Invoke("[3D preview] WGL bootstrap GetDC failed.");
            return null;
        }

        try
        {
            var pfd = _defaultPfd;
            if (!SetPixelFormat(dc, _defaultPixelFormat, ref pfd))
            {
                log?.Invoke("[3D preview] WGL bootstrap SetPixelFormat failed.");
                return null;
            }

            foreach (var profile in profiles)
            {
                if (profile.Type != GlProfileType.OpenGL)
                {
                    continue;
                }

                var attribs = new[]
                {
                    WglContextMajorVersionArb, profile.Major,
                    WglContextMinorVersionArb, profile.Minor,
                    WglContextProfileMaskArb, WglContextCoreProfileBitArb,
                    0, 0,
                };

                var context = _createContextAttribsArb!(dc, IntPtr.Zero, attribs);
                if (context == IntPtr.Zero)
                {
                    continue;
                }

                log?.Invoke($"[3D preview] WGL sidecar context created (OpenGL {profile.Major}.{profile.Minor} core).");
                return new WglSidecarContext(profile, context, window, dc, _defaultPixelFormat, _defaultPfd);
            }

            log?.Invoke("[3D preview] WGL bootstrap could not create any requested OpenGL profile.");
            return null;
        }
        catch (Exception ex)
        {
            log?.Invoke("[3D preview] WGL bootstrap failed: " + ex.Message);
            ReleaseDC(window, dc);
            DestroyWindow(window);
            return null;
        }
    }

    internal static GlInterface CreateGlInterface(GlVersion version) =>
        new(version, ResolveProcAddress);

    private static IntPtr ResolveProcAddress(string proc)
    {
        var ext = wglGetProcAddress(proc);
        if (ext != IntPtr.Zero)
        {
            return ext;
        }

        var openGl32 = EnsureOpenGl32Handle();
        return openGl32 != IntPtr.Zero ? GetProcAddress(openGl32, proc) : IntPtr.Zero;
    }

    private static bool EnsureBootstrapReady()
    {
        if (_bootstrapReady)
        {
            return true;
        }

        lock (BootstrapLock)
        {
            if (_bootstrapReady)
            {
                return true;
            }

            var window = CreateHiddenWindow();
            if (window == IntPtr.Zero)
            {
                return false;
            }

            var dc = GetDC(window);
            if (dc == IntPtr.Zero)
            {
                DestroyWindow(window);
                return false;
            }

            try
            {
                var pfd = new PixelFormatDescriptor
                {
                    Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
                    Version = 1,
                    Flags = 0x0000_0025,
                    ColorBits = 32,
                    DepthBits = 24,
                    StencilBits = 8,
                };

                var pixelFormat = ChoosePixelFormat(dc, ref pfd);
                if (pixelFormat == 0 || !SetPixelFormat(dc, pixelFormat, ref pfd))
                {
                    return false;
                }

                var bootstrapContext = wglCreateContext(dc);
                if (bootstrapContext == IntPtr.Zero || !wglMakeCurrent(dc, bootstrapContext))
                {
                    return false;
                }

                _createContextAttribsArb = LoadProc<WglCreateContextAttribsArbDelegate>("wglCreateContextAttribsARB");
                _choosePixelFormatArb = LoadProc<WglChoosePixelFormatArbDelegate>("wglChoosePixelFormatARB");
                if (_createContextAttribsArb is null)
                {
                    return false;
                }

                _defaultPfd = pfd;
                _defaultPixelFormat = pixelFormat;
                if (_choosePixelFormatArb is not null)
                {
                    var attribs = new int[]
                    {
                        (int)WglDrawToWindowArb, 1,
                        (int)WglAccelerationArb, (int)WglFullAccelerationArb,
                        (int)WglSupportOpenglArb, 1,
                        (int)WglDoubleBufferArb, 1,
                        (int)WglPixelTypeArb, (int)WglTypeRgbaArb,
                        (int)WglColorBitsArb, 32,
                        (int)WglAlphaBitsArb, 8,
                        (int)WglDepthBitsArb, 24,
                        (int)WglStencilBitsArb, 8,
                        0,
                    };
                    var formats = new int[1];
                    if (_choosePixelFormatArb(dc, attribs, null, 1, formats, out var count) && count > 0)
                    {
                        DescribePixelFormat(dc, formats[0], Marshal.SizeOf<PixelFormatDescriptor>(), ref _defaultPfd);
                        _defaultPixelFormat = formats[0];
                    }
                }

                wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                wglDeleteContext(bootstrapContext);
                _bootstrapReady = true;
                return true;
            }
            finally
            {
                ReleaseDC(window, dc);
                DestroyWindow(window);
            }
        }
    }

    private static T? LoadProc<T>(string name) where T : Delegate
    {
        var ptr = wglGetProcAddress(name);
        return ptr == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private static IntPtr EnsureOpenGl32Handle()
    {
        if (_openGl32Handle == IntPtr.Zero)
        {
            _openGl32Handle = LoadLibraryW("opengl32.dll");
        }

        return _openGl32Handle;
    }

    private static IntPtr CreateHiddenWindow()
    {
        var hInstance = GetModuleHandleW(IntPtr.Zero);
        var className = "AutoPBR.PreviewWglBootstrap";
        if (!s_windowClassRegistered)
        {
            var wc = new WndClass
            {
                Style = CsOwndc,
                LpfnWndProc = s_wndProc,
                HInstance = hInstance,
                LpszClassName = className,
            };
            s_windowClassRegistered = RegisterClassW(ref wc) != 0;
        }

        return CreateWindowExW(
            WsExToolwindow,
            className,
            "AutoPBR WGL Bootstrap",
            WsPopup,
            0,
            0,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);
    }

    private static bool s_windowClassRegistered;
    private static readonly WndProcDelegate s_wndProc = DefWindowProcW;

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

    [StructLayout(LayoutKind.Sequential)]
    internal struct PixelFormatDescriptor
    {
        public ushort Size;
        public ushort Version;
        public uint Flags;
        public byte PixelType;
        public byte ColorBits;
        public byte RedBits;
        public byte RedShift;
        public byte GreenBits;
        public byte GreenShift;
        public byte BlueBits;
        public byte BlueShift;
        public byte AlphaBits;
        public byte AlphaShift;
        public byte AccumBits;
        public byte AccumRedBits;
        public byte AccumGreenBits;
        public byte AccumBlueBits;
        public byte AccumAlphaBits;
        public byte DepthBits;
        public byte StencilBits;
        public byte AuxBuffers;
        public byte LayerType;
        public byte Reserved;
        public uint LayerMask;
        public uint VisibleMask;
        public uint DamageMask;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WglCreateContextAttribsArbDelegate(IntPtr hdc, IntPtr shareContext, int[] attribList);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglChoosePixelFormatArbDelegate(
        IntPtr hdc,
        int[]? intAttribs,
        float[]? floatAttribs,
        int maxFormats,
        int[] formats,
        out int numFormats);

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
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int ChoosePixelFormat(IntPtr hdc, ref PixelFormatDescriptor ppfd);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetPixelFormat(IntPtr hdc, int format, ref PixelFormatDescriptor ppfd);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int DescribePixelFormat(IntPtr hdc, int iPixelFormat, int nBytes, ref PixelFormatDescriptor ppfd);

    [DllImport("opengl32.dll", SetLastError = true)]
    private static extern IntPtr wglCreateContext(IntPtr hdc);

    [DllImport("opengl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool wglDeleteContext(IntPtr hglrc);

    [DllImport("opengl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

    [DllImport("opengl32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr wglGetProcAddress(string lpszProc);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    private sealed class WglSidecarContext : IGlContext
    {
        private readonly object _lock = new();
        private readonly IntPtr _context;
        private readonly IntPtr _window;
        private readonly IntPtr _dc;
        private readonly int _pixelFormat;
        private readonly PixelFormatDescriptor _pfd;
        private bool _isLost;

        public WglSidecarContext(
            GlVersion version,
            IntPtr context,
            IntPtr window,
            IntPtr dc,
            int pixelFormat,
            PixelFormatDescriptor pfd)
        {
            Version = version;
            _context = context;
            _window = window;
            _dc = dc;
            _pixelFormat = pixelFormat;
            _pfd = pfd;
            StencilSize = pfd.StencilBits;
            using (MakeCurrent())
            {
                GlInterface = CreateGlInterface(version);
            }
        }

        public GlVersion Version { get; }
        public GlInterface GlInterface { get; }
        public int SampleCount => 0;
        public int StencilSize { get; }
        public bool IsLost => _isLost;
        public bool CanCreateSharedContext => false;

        public IGlContext? CreateSharedContext(IEnumerable<GlVersion>? preferredVersions = null) => null;

        public bool IsSharedWith(IGlContext context) => ReferenceEquals(this, context);

        public object? TryGetFeature(Type featureType) => null;

        public IDisposable EnsureCurrent() => MakeCurrent();

        public IDisposable MakeCurrent()
        {
            if (_isLost)
            {
                throw new InvalidOperationException("WGL sidecar context is lost.");
            }

            if (wglGetCurrentContext() == _context && wglGetCurrentDC() == _dc)
            {
                return NoopRestore.Instance;
            }

            Monitor.Enter(_lock);
            if (!wglMakeCurrent(_dc, _context))
            {
                Monitor.Exit(_lock);
                throw new InvalidOperationException("wglMakeCurrent failed for desktop WGL sidecar.");
            }

            return new Restore(_dc, _context, _lock);
        }

        public void Dispose()
        {
            if (_isLost)
            {
                return;
            }

            _isLost = true;
            wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            wglDeleteContext(_context);
            ReleaseDC(_window, _dc);
            DestroyWindow(_window);
        }

        private sealed class Restore : IDisposable
        {
            private readonly IntPtr _dc;
            private readonly IntPtr _context;
            private readonly object _lock;
            private bool _disposed;

            public Restore(IntPtr dc, IntPtr context, object lockObj)
            {
                _dc = dc;
                _context = context;
                _lock = lockObj;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                Monitor.Exit(_lock);
            }
        }

        private sealed class NoopRestore : IDisposable
        {
            public static readonly NoopRestore Instance = new();
            public void Dispose()
            {
            }
        }
    }

    [DllImport("opengl32.dll")]
    private static extern IntPtr wglGetCurrentContext();

    [DllImport("opengl32.dll")]
    private static extern IntPtr wglGetCurrentDC();
}
