using System.Reflection;

using Avalonia.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>
/// Timed acquire/release on Avalonia export textures. <c>AcquireKeyedMutex</c> hard-codes
/// <see cref="int.MaxValue"/>; this uses the export's <c>Mutex</c> with a finite
/// <c>IDXGIKeyedMutex::AcquireSync</c> timeout so the present thread cannot stall forever.
/// </summary>
internal static class PreviewAvaloniaExportKeyedMutex
{
    private static readonly object Gate = new();
    private static PropertyInfo? _mutexProperty;
    private static bool _resolved;

    public static bool TryAcquire(IGlExportableExternalImageTexture export, ulong key, uint timeoutMilliseconds)
    {
        if (!TryBorrowNativeMutex(export, out var nativeMutex) || nativeMutex is null)
        {
            return false;
        }

        return nativeMutex.TryAcquire(key, timeoutMilliseconds);
    }

    public static bool TryRelease(IGlExportableExternalImageTexture export, ulong key)
    {
        if (!TryBorrowNativeMutex(export, out var nativeMutex) || nativeMutex is null)
        {
            return false;
        }

        return nativeMutex.TryRelease(key);
    }

    private static bool TryBorrowNativeMutex(
        IGlExportableExternalImageTexture export,
        out PreviewDxgiKeyedMutex? nativeMutex)
    {
        nativeMutex = null;
        EnsureResolved(export.GetType());
        if (_mutexProperty is null)
        {
            return false;
        }

        var mutexProxy = _mutexProperty.GetValue(export);
        if (mutexProxy is null ||
            !TryGetNativeComPointer(mutexProxy, out var pointer) ||
            pointer == IntPtr.Zero)
        {
            return false;
        }

        return PreviewDxgiKeyedMutex.TryBorrow(pointer, out nativeMutex);
    }

    private static void EnsureResolved(Type exportType)
    {
        if (_resolved)
        {
            return;
        }

        lock (Gate)
        {
            if (_resolved)
            {
                return;
            }

            var type = exportType;
            while (type is not null && _mutexProperty is null)
            {
                _mutexProperty = type.GetProperty(
                    "Mutex",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            _resolved = true;
        }
    }

    private static bool TryGetNativeComPointer(object comProxy, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        foreach (var methodName in new[] { "GetNativeIntPtr", "GetNativePointer", "DangerousGetHandle" })
        {
            var method = comProxy.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method is null || method.ReturnType != typeof(IntPtr) || method.GetParameters().Length != 0)
            {
                continue;
            }

            if (method.Invoke(comProxy, null) is IntPtr p && p != IntPtr.Zero)
            {
                pointer = p;
                return true;
            }
        }

        foreach (var propertyName in new[] { "NativeIntPtr", "NativePointer", "PPV", "Handle", "Pointer" })
        {
            var prop = comProxy.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.GetValue(comProxy) is IntPtr p && p != IntPtr.Zero)
            {
                pointer = p;
                return true;
            }
        }

        foreach (var field in comProxy.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType != typeof(IntPtr))
            {
                continue;
            }

            if (field.GetValue(comProxy) is IntPtr p && p != IntPtr.Zero)
            {
                pointer = p;
                return true;
            }
        }

        return false;
    }
}
