using System.Runtime.InteropServices;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Native <c>IDXGIKeyedMutex</c> acquire/release with finite timeouts (no Avalonia MicroCom).</summary>
internal sealed class PreviewDxgiKeyedMutex : IDisposable
{
    private static readonly Guid IID_IDXGIKeyedMutex = new("9d8e1289-d7b3-465f-8126-250e349af85d");

    private const int AcquireSyncVtableIndex = 8; // IUnknown(3) + IDXGIObject(4) + GetDevice(1) → AcquireSync
    private const int ReleaseSyncVtableIndex = 9;

    private const int S_OK = 0;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceDelegate(IntPtr thisPtr, ref Guid iid, out IntPtr ppv);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseDelegate(IntPtr thisPtr);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AcquireSyncDelegate(IntPtr thisPtr, ulong key, uint milliseconds);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseSyncDelegate(IntPtr thisPtr, ulong key);

    private IntPtr _keyedMutex;
    private readonly bool _ownsReference;
    private bool _disposed;

    private PreviewDxgiKeyedMutex(IntPtr keyedMutex, bool ownsReference)
    {
        _keyedMutex = keyedMutex;
        _ownsReference = ownsReference;
    }

    public static bool TryCreateFromTexture2D(IntPtr d3d11Texture2D, out PreviewDxgiKeyedMutex? mutex, out int hr)
    {
        mutex = null;
        hr = unchecked((int)0x80004005);
        if (d3d11Texture2D == IntPtr.Zero)
        {
            return false;
        }

        unsafe
        {
            var vtable = *(IntPtr*)d3d11Texture2D;
            var queryInterfacePtr = ((IntPtr*)vtable)[0];
            var queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryInterfacePtr);
            var iid = IID_IDXGIKeyedMutex;
            hr = queryInterface(d3d11Texture2D, ref iid, out var keyedMutex);
            if (hr != S_OK || keyedMutex == IntPtr.Zero)
            {
                return false;
            }

            mutex = new PreviewDxgiKeyedMutex(keyedMutex, ownsReference: true);
            return true;
        }
    }

    /// <summary>Wraps an existing <c>IDXGIKeyedMutex*</c> without AddRef/Release ownership.</summary>
    public static bool TryBorrow(IntPtr keyedMutex, out PreviewDxgiKeyedMutex? mutex)
    {
        mutex = null;
        if (keyedMutex == IntPtr.Zero)
        {
            return false;
        }

        mutex = new PreviewDxgiKeyedMutex(keyedMutex, ownsReference: false);
        return true;
    }

    public bool TryAcquire(ulong key, uint timeoutMilliseconds)
    {
        if (_keyedMutex == IntPtr.Zero)
        {
            return false;
        }

        var hr = InvokeAcquireSync(key, timeoutMilliseconds);
        return hr == S_OK;
    }

    public bool TryRelease(ulong key)
    {
        if (_keyedMutex == IntPtr.Zero)
        {
            return false;
        }

        var hr = InvokeReleaseSync(key);
        return hr == S_OK;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_keyedMutex == IntPtr.Zero || !_ownsReference)
        {
            _keyedMutex = IntPtr.Zero;
            return;
        }

        unsafe
        {
            var vtable = *(IntPtr*)_keyedMutex;
            var releasePtr = ((IntPtr*)vtable)[2];
            var release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(releasePtr);
            release(_keyedMutex);
        }

        _keyedMutex = IntPtr.Zero;
    }

    private int InvokeAcquireSync(ulong key, uint timeoutMilliseconds)
    {
        unsafe
        {
            var vtable = *(IntPtr*)_keyedMutex;
            var fnPtr = ((IntPtr*)vtable)[AcquireSyncVtableIndex];
            var fn = Marshal.GetDelegateForFunctionPointer<AcquireSyncDelegate>(fnPtr);
            return fn(_keyedMutex, key, timeoutMilliseconds);
        }
    }

    private int InvokeReleaseSync(ulong key)
    {
        unsafe
        {
            var vtable = *(IntPtr*)_keyedMutex;
            var fnPtr = ((IntPtr*)vtable)[ReleaseSyncVtableIndex];
            var fn = Marshal.GetDelegateForFunctionPointer<ReleaseSyncDelegate>(fnPtr);
            return fn(_keyedMutex, key);
        }
    }
}
