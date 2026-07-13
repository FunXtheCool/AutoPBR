using System.Runtime.InteropServices;

using Avalonia.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Creates a desktop WGL sidecar on the same physical GPU as Avalonia/ANGLE's D3D11 presentation device.
/// Uses <c>WGL_NV_gpu_affinity</c> when the default adapter cannot open the shared D3D11 device.
/// </summary>
internal static class PreviewDesktopWglGpuAffinity
{
    private const int WglContextMajorVersionArb = 0x2091;
    private const int WglContextMinorVersionArb = 0x2092;
    private const int WglContextProfileMaskArb = 0x9126;
    private const int WglContextCoreProfileBitArb = 0x0000_0001;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglEnumGpusNvDelegate(uint index, out IntPtr gpuHandle);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WglCreateAffinityDcNvDelegate(IntPtr gpuMask);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglDestroyAffinityDcNvDelegate(IntPtr affinityDc);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WglCreateAssociatedContextAttribsNvDelegate(
        IntPtr gpuHandle,
        IntPtr shareContext,
        int[] attribList);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglMakeAssociatedContextCurrentNvDelegate(IntPtr affinityDc, IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglDeleteContextDelegate(IntPtr context);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool WglChoosePixelFormatArbDelegate(
        IntPtr hdc,
        int[]? intAttribs,
        float[]? floatAttribs,
        int maxFormats,
        int[] formats,
        out int numFormats);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate bool SetPixelFormatDelegate(IntPtr hdc, int format, ref PixelFormatDescriptor pfd);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int DescribePixelFormatDelegate(IntPtr hdc, int format, int size, ref PixelFormatDescriptor pfd);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr GetStringDelegate(uint name);

    [StructLayout(LayoutKind.Sequential)]
    private struct PixelFormatDescriptor
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

    public static IGlContext? TryCreateSidecarContext(
        IReadOnlyList<GlVersion> profiles,
        IntPtr presentationD3d11Device,
        Action<string>? log,
        bool probePresentationAdapter = true)
    {
        var bootstrap = CreateDefaultContext(profiles);
        if (bootstrap is null)
        {
            return null;
        }

        if (!probePresentationAdapter || presentationD3d11Device == IntPtr.Zero)
        {
            log?.Invoke(presentationD3d11Device == IntPtr.Zero
                ? "[3D preview] Sidecar WGL created on default adapter (adapter matching deferred)."
                : "[3D preview] Sidecar WGL created on default adapter.");
            return bootstrap;
        }

        PreviewAngleD3D11Access.TryDescribePresentationDevice(presentationD3d11Device, out var angleAdapter);
        log?.Invoke($"[3D preview] ANGLE D3D11 adapter: {angleAdapter}.");

        using (bootstrap.MakeCurrent())
        {
            if (ContextCanOpenDevice(bootstrap.GlInterface, presentationD3d11Device, out var defaultError))
            {
                log?.Invoke("[3D preview] Sidecar WGL matched ANGLE D3D11 adapter on default GPU.");
                return bootstrap;
            }

            log?.Invoke(
                $"[3D preview] Default sidecar adapter cannot open ANGLE D3D11 device ({PreviewDesktopWglDxInteropDiagnostics.Describe(PreviewDesktopWglDxInteropFailure.OpenDeviceFailed, defaultError)}). Probing NV GPU affinity...");
        }

        if (!TryLoadAffinityProcs(bootstrap.GlInterface, out var procs))
        {
            log?.Invoke("[3D preview] WGL_NV_gpu_affinity unavailable; sidecar stays on default adapter.");
            return bootstrap;
        }

        IGlContext? matched = null;
        var gpuCount = 0;
        const uint maxGpuProbeCount = 16;
        for (uint gpuIndex = 0;
             gpuIndex < maxGpuProbeCount && procs.EnumGpus(gpuIndex, out var gpuHandle);
             gpuIndex++)
        {
            gpuCount++;
            var candidate = TryCreateAffinityContext(profiles, gpuHandle, procs, log, gpuIndex);
            if (candidate is null)
            {
                continue;
            }

            using (candidate.MakeCurrent())
            {
                PreviewDesktopWglDxInterop.ResetProcCache();
                if (!ContextCanOpenDevice(candidate.GlInterface, presentationD3d11Device, out _))
                {
                    candidate.Dispose();
                    continue;
                }
            }

            matched = candidate;
            log?.Invoke($"[3D preview] Sidecar WGL bound to GPU #{gpuIndex} for D3D11/WGL interop.");
            break;
        }

        if (matched is null)
        {
            log?.Invoke(
                gpuCount == 0
                    ? "[3D preview] WGL_NV_gpu_affinity reported no GPUs; sidecar stays on default adapter."
                    : $"[3D preview] Sidecar could not match any of {gpuCount} enumerated GPU(s) to ANGLE's D3D11 adapter. Async PBO fallback will be used.");
            return bootstrap;
        }

        bootstrap.Dispose();
        return matched;
    }

    private static bool ContextCanOpenDevice(GlInterface glInterface, IntPtr d3d11Device, out int errorCode)
    {
        errorCode = 0;
        if (!PreviewDesktopWglDxInterop.EnsureProcs(glInterface))
        {
            errorCode = PreviewWin32Error.GetLastErrorCode();
            return false;
        }

        if (!PreviewDesktopWglDxInterop.TryOpenDevice(glInterface, d3d11Device, out var interopDevice))
        {
            errorCode = PreviewWin32Error.GetLastErrorCode();
            return false;
        }

        PreviewDesktopWglDxInterop.CloseDevice(interopDevice);
        return true;
    }

    internal static bool ContextCanOpenPresentationDevice(GlInterface glInterface, IntPtr d3d11Device) =>
        ContextCanOpenDevice(glInterface, d3d11Device, out _);

    private static IGlContext? CreateDefaultContext(IReadOnlyList<GlVersion> profiles) =>
        PreviewDesktopWglBootstrap.TryCreateContext(profiles);

    private static bool TryLoadAffinityProcs(GlInterface gl, out AffinityProcs procs)
    {
        procs = default;
        var enumGpus = Load<WglEnumGpusNvDelegate>(gl, "wglEnumGpusNV");
        var createAffinityDc = Load<WglCreateAffinityDcNvDelegate>(gl, "wglCreateAffinityDCNV");
        var destroyAffinityDc = Load<WglDestroyAffinityDcNvDelegate>(gl, "wglDestroyAffinityDCNV");
        var createAssociated = Load<WglCreateAssociatedContextAttribsNvDelegate>(gl, "wglCreateAssociatedContextAttribsNV");
        var makeAssociatedCurrent = Load<WglMakeAssociatedContextCurrentNvDelegate>(gl, "wglMakeAssociatedContextCurrentNV");
        if (enumGpus is null || createAffinityDc is null || destroyAffinityDc is null ||
            createAssociated is null || makeAssociatedCurrent is null)
        {
            return false;
        }

        var openGl32 = GetOpenGl32();
        procs = new AffinityProcs(
            enumGpus,
            createAffinityDc,
            destroyAffinityDc,
            createAssociated,
            makeAssociatedCurrent,
            Load<WglDeleteContextDelegate>(openGl32, "wglDeleteContext")!,
            Load<SetPixelFormatDelegate>(openGl32, "SetPixelFormat")!,
            Load<DescribePixelFormatDelegate>(openGl32, "DescribePixelFormat")!,
            Load<WglChoosePixelFormatArbDelegate>(gl, "wglChoosePixelFormatARB"));
        return true;
    }

    private static IGlContext? TryCreateAffinityContext(
        IReadOnlyList<GlVersion> profiles,
        IntPtr gpuHandle,
        AffinityProcs procs,
        Action<string>? log,
        uint gpuIndex)
    {
        unsafe
        {
            var gpuMask = stackalloc IntPtr[2];
            gpuMask[0] = gpuHandle;
            gpuMask[1] = IntPtr.Zero;
            var affinityDc = procs.CreateAffinityDc((IntPtr)gpuMask);
            if (affinityDc == IntPtr.Zero)
            {
                return null;
            }

            if (!TryChoosePixelFormat(affinityDc, procs))
            {
                procs.DestroyAffinityDc(affinityDc);
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
                var context = procs.CreateAssociatedContext(gpuHandle, IntPtr.Zero, attribs);
                if (context == IntPtr.Zero)
                {
                    continue;
                }

                if (!procs.MakeAssociatedCurrent(affinityDc, context))
                {
                    procs.DeleteContext(context);
                    continue;
                }

                var renderer = TryReadRenderer();
                log?.Invoke(
                    string.IsNullOrWhiteSpace(renderer)
                        ? $"[3D preview] GPU #{gpuIndex}: affinity context created."
                        : $"[3D preview] GPU #{gpuIndex}: {renderer.Trim()}.");

                return AffinityContext.Create(profile, affinityDc, context, procs);
            }

            procs.DestroyAffinityDc(affinityDc);
        }

        return null;
    }

    private static bool TryChoosePixelFormat(IntPtr dc, AffinityProcs procs)
    {
        if (procs.ChoosePixelFormatArb is not null)
        {
            var attribs = new[]
            {
                0x2001, 1,
                0x2003, 1,
                0x2010, 1,
                0x2011, 1,
                0x201B, 0x202B,
                0x2014, 32,
                0x2022, 24,
                0x2023, 8,
                0,
            };
            var formats = new int[1];
            if (procs.ChoosePixelFormatArb(dc, attribs, null, 1, formats, out var count) && count > 0)
            {
                var pfd = new PixelFormatDescriptor { Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>() };
                procs.DescribePixelFormat(dc, formats[0], Marshal.SizeOf<PixelFormatDescriptor>(), ref pfd);
                return procs.SetPixelFormat(dc, formats[0], ref pfd);
            }
        }

        var fallback = new PixelFormatDescriptor
        {
            Size = (ushort)Marshal.SizeOf<PixelFormatDescriptor>(),
            Version = 1,
            Flags = 0x0000_0025,
            ColorBits = 32,
            DepthBits = 24,
            StencilBits = 8,
        };
        return procs.SetPixelFormat(dc, 1, ref fallback);
    }

    private static string? TryReadRenderer()
    {
        unsafe
        {
            var proc = wglGetProcAddress("glGetString");
            if (proc == IntPtr.Zero)
            {
                proc = GetProcAddress(GetOpenGl32(), "glGetString");
            }

            if (proc == IntPtr.Zero)
            {
                return null;
            }

            var getString = Marshal.GetDelegateForFunctionPointer<GetStringDelegate>(proc);
            var ptr = getString(0x1F01);
            return ptr == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(ptr);
        }
    }

    private static T? Load<T>(GlInterface gl, string name) where T : Delegate
    {
        var proc = gl.GetProcAddress(name);
        return proc == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(proc);
    }

    private static T? Load<T>(IntPtr module, string name) where T : Delegate
    {
        var proc = GetProcAddress(module, name);
        return proc == IntPtr.Zero ? null : Marshal.GetDelegateForFunctionPointer<T>(proc);
    }

    private static IntPtr GetOpenGl32() => PreviewDesktopWglBootstrap.OpenGl32Handle;

    [DllImport("opengl32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr wglGetProcAddress(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);

    private readonly struct AffinityProcs(
        WglEnumGpusNvDelegate enumGpus,
        WglCreateAffinityDcNvDelegate createAffinityDc,
        WglDestroyAffinityDcNvDelegate destroyAffinityDc,
        WglCreateAssociatedContextAttribsNvDelegate createAssociatedContext,
        WglMakeAssociatedContextCurrentNvDelegate makeAssociatedCurrent,
        WglDeleteContextDelegate deleteContext,
        SetPixelFormatDelegate setPixelFormat,
        DescribePixelFormatDelegate describePixelFormat,
        WglChoosePixelFormatArbDelegate? choosePixelFormatArb)
    {
        public WglEnumGpusNvDelegate EnumGpus { get; } = enumGpus;
        public WglCreateAffinityDcNvDelegate CreateAffinityDc { get; } = createAffinityDc;
        public WglDestroyAffinityDcNvDelegate DestroyAffinityDc { get; } = destroyAffinityDc;
        public WglCreateAssociatedContextAttribsNvDelegate CreateAssociatedContext { get; } = createAssociatedContext;
        public WglMakeAssociatedContextCurrentNvDelegate MakeAssociatedCurrent { get; } = makeAssociatedCurrent;
        public WglDeleteContextDelegate DeleteContext { get; } = deleteContext;
        public SetPixelFormatDelegate SetPixelFormat { get; } = setPixelFormat;
        public DescribePixelFormatDelegate DescribePixelFormat { get; } = describePixelFormat;
        public WglChoosePixelFormatArbDelegate? ChoosePixelFormatArb { get; } = choosePixelFormatArb;
    }

    private sealed class AffinityContext : IGlContext
    {
        private readonly IntPtr _affinityDc;
        private readonly IntPtr _context;
        private readonly WglMakeAssociatedContextCurrentNvDelegate _makeAssociatedCurrent;
        private readonly WglDeleteContextDelegate _deleteContext;
        private readonly WglDestroyAffinityDcNvDelegate _destroyAffinityDc;

        private AffinityContext(GlVersion version, IntPtr affinityDc, IntPtr context, AffinityProcs procs)
        {
            _affinityDc = affinityDc;
            _context = context;
            _makeAssociatedCurrent = procs.MakeAssociatedCurrent;
            _deleteContext = procs.DeleteContext;
            _destroyAffinityDc = procs.DestroyAffinityDc;
            Version = version;
            GlInterface = CreateGlInterface(version);
        }

        public static AffinityContext Create(GlVersion version, IntPtr affinityDc, IntPtr context, AffinityProcs procs) =>
            new(version, affinityDc, context, procs);

        public GlVersion Version { get; }
        public GlInterface GlInterface { get; }
        public int SampleCount => 0;
        public int StencilSize => 8;
        public bool IsLost => false;
        public bool CanCreateSharedContext => false;

        public IGlContext? CreateSharedContext(IEnumerable<GlVersion>? preferredVersions = null) => null;

        public bool IsSharedWith(IGlContext context) => ReferenceEquals(this, context);

        public object? TryGetFeature(Type featureType) => null;

        public IDisposable EnsureCurrent() => MakeCurrent();

        public IDisposable MakeCurrent()
        {
            if (!_makeAssociatedCurrent(_affinityDc, _context))
            {
                throw new InvalidOperationException("wglMakeAssociatedContextCurrentNV failed.");
            }

            return new Restore(this);
        }

        public void Dispose()
        {
            _makeAssociatedCurrent(IntPtr.Zero, IntPtr.Zero);
            _deleteContext(_context);
            _destroyAffinityDc(_affinityDc);
        }

        private static GlInterface CreateGlInterface(GlVersion version) =>
            PreviewDesktopWglBootstrap.CreateGlInterface(version);

        private sealed class Restore : IDisposable
        {
            private readonly AffinityContext _owner;
            private bool _disposed;

            public Restore(AffinityContext owner) => _owner = owner;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner._makeAssociatedCurrent(IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
}
