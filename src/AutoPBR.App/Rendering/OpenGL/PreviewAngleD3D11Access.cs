using System.Reflection;
using System.Runtime.InteropServices;

using Avalonia.OpenGL;
using Avalonia.Platform;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Reflects Avalonia ANGLE's D3D11 device + shared texture opens for WGL/DXGI interop.</summary>
internal static class PreviewAngleD3D11Access
{
    private static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    public static bool TryGetPresentationDevice(IGlContextExternalObjectsFeature feature, out IntPtr d3d11Device)
    {
        d3d11Device = IntPtr.Zero;
        if (!TryResolvePresentation(feature, null, out var presentation, out _))
        {
            return false;
        }

        d3d11Device = presentation.NativeDevice;
        return d3d11Device != IntPtr.Zero;
    }

    public static bool TryResolvePresentation(
        IGlContextExternalObjectsFeature? feature,
        IGlContext? presentationContext,
        out PreviewAngleD3D11Presentation presentation,
        out string resolutionDetail)
    {
        presentation = default;
        resolutionDetail = "none";
        var failures = new List<string>();

        object? microComDevice = null;
        object? microComDevice1 = null;
        if (feature is not null)
        {
            TryReflectFieldValue(feature, "_device", out microComDevice);
            TryReflectFieldValue(feature, "_device1", out microComDevice1);
            if (microComDevice is null)
            {
                failures.Add("feature._device");
            }
        }

        IntPtr nativeDevice = IntPtr.Zero;
        if (presentationContext is not null && TryGetDeviceFromEglContext(presentationContext, out nativeDevice))
        {
            resolutionDetail = $"ANGLE GetDirect3DDevice via {presentationContext.GetType().FullName}";
        }
        else if (presentationContext is not null)
        {
            failures.Add("presentationContext.Display.GetDirect3DDevice");
        }

        if (nativeDevice == IntPtr.Zero || microComDevice is null)
        {
            if (feature is not null)
            {
                resolutionDetail =
                    $"feature={feature.GetType().FullName}, context={presentationContext?.GetType().FullName ?? "(null)"}, tried=[{string.Join(", ", failures)}]";
            }
            else if (presentationContext is not null)
            {
                resolutionDetail = $"context={presentationContext.GetType().FullName}, feature=(null)";
            }

            return false;
        }

        presentation = new PreviewAngleD3D11Presentation
        {
            NativeDevice = nativeDevice,
            MicroComDevice = microComDevice,
            MicroComDevice1 = microComDevice1,
        };
        return true;
    }

    public static bool TryDescribePresentationDevice(IntPtr d3d11Device, out string description)
    {
        description = d3d11Device == IntPtr.Zero ? "(null)" : $"device=0x{d3d11Device:x}";
        return d3d11Device != IntPtr.Zero;
    }

    public static bool TryOpenSharedTexture2D(
        in PreviewAngleD3D11Presentation presentation,
        IPlatformHandle sharedHandle,
        out IntPtr d3d11Texture2D,
        out IDisposable? textureLifetime,
        out int hr)
    {
        d3d11Texture2D = IntPtr.Zero;
        textureLifetime = null;
        hr = 0;
        if (!presentation.IsValid || sharedHandle.Handle == IntPtr.Zero)
        {
            return false;
        }

        // Prefer native ID3D11Device::OpenSharedResource on the raw device pointer.
        // Avalonia MicroCom device proxies carry a SynchronizationContext and will marshal
        // calls back to the ANGLE/UI thread — which deadlocks when this runs on the WGL owner
        // thread while present is blocked in Invoke / keyed-mutex / WaitForInFlight.
        var useGlobalHandle = sharedHandle.HandleDescriptor ==
                              KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle;
        if (useGlobalHandle)
        {
            if (TryOpenSharedTexture2DNative(presentation.NativeDevice, sharedHandle.Handle, out d3d11Texture2D, out hr))
            {
                textureLifetime = new PreviewAngleD3D11NativeTextureLease(d3d11Texture2D);
                return true;
            }

            // Do not fall back to MicroCom for global shared handles — that path deadlocks off-thread.
            return false;
        }

        try
        {
            if (presentation.MicroComDevice1 is null)
            {
                hr = unchecked((int)0x80070057);
                return false;
            }

            var openedResource = InvokeOpenSharedResource1(presentation.MicroComDevice1, sharedHandle.Handle);
            var texture = InvokeQueryInterface(openedResource, IID_ID3D11Texture2D);
            if (!TryGetNativeComPointer(texture, out d3d11Texture2D) || d3d11Texture2D == IntPtr.Zero)
            {
                ReleaseComObject(texture);
                ReleaseComObject(openedResource);
                hr = unchecked((int)0x80004005);
                return false;
            }

            textureLifetime = new PreviewAngleD3D11ComLease(openedResource, texture);
            return true;
        }
        catch (COMException ex)
        {
            hr = ex.HResult;
            return false;
        }
        catch (Exception ex)
        {
            hr = ex.HResult;
            return false;
        }
    }

    /// <summary>
    /// <c>ID3D11Device::OpenSharedResource</c> via vtable (index 28). Thread-safe w.r.t. MicroCom.
    /// </summary>
    private static bool TryOpenSharedTexture2DNative(
        IntPtr d3d11Device,
        IntPtr sharedHandle,
        out IntPtr d3d11Texture2D,
        out int hr)
    {
        d3d11Texture2D = IntPtr.Zero;
        hr = unchecked((int)0x80004005);
        if (d3d11Device == IntPtr.Zero || sharedHandle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            unsafe
            {
                var vtable = *(IntPtr*)d3d11Device;
                // IUnknown(3) + Create*(25) → OpenSharedResource at slot 28.
                var openPtr = ((IntPtr*)vtable)[28];
                var open = Marshal.GetDelegateForFunctionPointer<OpenSharedResourceDelegate>(openPtr);
                var iid = IID_ID3D11Texture2D;
                hr = open(d3d11Device, sharedHandle, ref iid, out d3d11Texture2D);
                return hr >= 0 && d3d11Texture2D != IntPtr.Zero;
            }
        }
        catch
        {
            d3d11Texture2D = IntPtr.Zero;
            return false;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int OpenSharedResourceDelegate(
        IntPtr thisPtr,
        IntPtr sharedHandle,
        ref Guid returnedInterface,
        out IntPtr resource);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint NativeReleaseDelegate(IntPtr thisPtr);

    private static object InvokeOpenSharedResource(object device, IntPtr sharedHandle)
    {
        var method = FindMethod(device, "OpenSharedResource", 2) ??
                     throw new MissingMethodException("ID3D11Device.OpenSharedResource was not found on the ANGLE device proxy.");
        return InvokeGuidMethod(device, method, sharedHandle);
    }

    private static object InvokeOpenSharedResource1(object device, IntPtr sharedHandle)
    {
        var method = FindMethod(device, "OpenSharedResource1", 2) ??
                     throw new MissingMethodException("ID3D11Device1.OpenSharedResource1 was not found on the ANGLE device proxy.");
        return InvokeGuidMethod(device, method, sharedHandle);
    }

    private static object InvokeGuidMethod(object target, MethodInfo method, IntPtr sharedHandle)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 2)
        {
            throw new InvalidOperationException($"Unexpected {method.Name} signature on ANGLE D3D11 proxy.");
        }

        if (parameters[1].ParameterType == typeof(Guid).MakeByRefType())
        {
            var guid = IID_ID3D11Texture2D;
            var result = method.Invoke(target, new object?[] { sharedHandle, guid });
            if (result is null)
            {
                throw new InvalidOperationException($"{method.Name} returned null.");
            }

            return result;
        }

        unsafe
        {
            var iidBytes = stackalloc byte[sizeof(Guid)];
            *(Guid*)iidBytes = IID_ID3D11Texture2D;
            var result = method.Invoke(target, new object?[] { sharedHandle, (IntPtr)iidBytes });
            if (result is null)
            {
                throw new InvalidOperationException($"{method.Name} returned null.");
            }

            return result;
        }
    }

    private static object InvokeQueryInterface(object comObject, Guid iid)
    {
        var textureType = FindMicroComInterfaceType(comObject, "ID3D11Texture2D")
                          ?? throw new MissingMethodException("ID3D11Texture2D MicroCom interface type was not found.");
        var query = comObject.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(m =>
                string.Equals(m.Name, "QueryInterface", StringComparison.Ordinal) &&
                m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 1);
        if (query is null)
        {
            throw new MissingMethodException("QueryInterface was not found on the opened shared D3D11 resource.");
        }

        var result = query.MakeGenericMethod(textureType).Invoke(comObject, null);
        if (result is null)
        {
            throw new InvalidOperationException("QueryInterface<ID3D11Texture2D> returned null.");
        }

        return result;
    }

    private static Type? FindMicroComInterfaceType(object comObject, string interfaceName)
    {
        foreach (var assembly in new[] { comObject.GetType().Assembly }
                     .Concat(AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName?.StartsWith("Avalonia.", StringComparison.Ordinal) == true)))
        {
            try
            {
                var type = assembly.GetType($"Avalonia.Win32.DirectX.{interfaceName}", throwOnError: false)
                           ?? assembly.GetTypes().FirstOrDefault(t => t.Name == interfaceName && t.IsInterface);
                if (type is not null)
                {
                    return type;
                }
            }
            catch
            {
                //
            }
        }

        return null;
    }

    private static MethodInfo? FindMethod(object target, string name, int parameterCount) =>
        target.GetType().GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: parameterCount switch
            {
                2 => new[] { typeof(IntPtr), typeof(Guid).MakeByRefType() },
                _ => Type.EmptyTypes,
            },
            modifiers: null) ??
        target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal) && m.GetParameters().Length == parameterCount);

    private static void ReleaseComObject(object comObject)
    {
        if (comObject is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static bool TryGetDeviceFromEglContext(IGlContext context, out IntPtr d3d11Device)
    {
        d3d11Device = IntPtr.Zero;
        for (var contextType = context.GetType(); contextType is not null; contextType = contextType.BaseType)
        {
            var display = contextType.GetProperty("Display", BindingFlags.Instance | BindingFlags.Public)?
                .GetValue(context);
            if (display is null)
            {
                continue;
            }

            for (var displayType = display.GetType(); displayType is not null; displayType = displayType.BaseType)
            {
                var getDirect3DDevice = displayType.GetMethod(
                    "GetDirect3DDevice",
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                if (getDirect3DDevice is null)
                {
                    continue;
                }

                try
                {
                    if (getDirect3DDevice.Invoke(display, null) is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        d3d11Device = ptr;
                        return true;
                    }
                }
                catch
                {
                    //
                }
            }
        }

        return false;
    }

    private static bool TryReflectFieldValue(object target, string fieldName, out object? value)
    {
        value = null;
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field is null)
            {
                continue;
            }

            value = field.GetValue(target);
            return value is not null;
        }

        return false;
    }

    private static bool TryGetNativeComPointer(object comProxy, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        if (comProxy is IntPtr direct && direct != IntPtr.Zero)
        {
            pointer = direct;
            return true;
        }

        foreach (var methodName in new[] { "GetNativeIntPtr", "GetNativePointer", "DangerousGetHandle" })
        {
            if (TryInvokeInstance(comProxy, methodName, out pointer))
            {
                return true;
            }
        }

        foreach (var propertyName in new[] { "NativeIntPtr", "NativePointer", "PPV", "Handle", "Pointer" })
        {
            if (TryReadInstanceProperty(comProxy, propertyName, out pointer))
            {
                return true;
            }
        }

        foreach (var fieldName in new[] { "_native", "_ptr", "Native", "Pointer" })
        {
            if (TryReflectFieldPointer(comProxy, fieldName, out pointer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReflectFieldPointer(object target, string fieldName, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        if (!TryReflectFieldValue(target, fieldName, out var value) || value is null)
        {
            return false;
        }

        return TryGetNativeComPointer(value, out pointer);
    }

    private static bool TryInvokeInstance(object target, string methodName, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method is null || method.GetParameters().Length != 0)
            {
                continue;
            }

            try
            {
                if (method.Invoke(target, null) is IntPtr ptr && ptr != IntPtr.Zero)
                {
                    pointer = ptr;
                    return true;
                }
            }
            catch
            {
                //
            }
        }

        return false;
    }

    private static bool TryReadInstanceProperty(object target, string propertyName, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(target) is not IntPtr ptr || ptr == IntPtr.Zero)
            {
                continue;
            }

            pointer = ptr;
            return true;
        }

        return false;
    }

    private sealed class PreviewAngleD3D11NativeTextureLease : IDisposable
    {
        private IntPtr _texture;

        public PreviewAngleD3D11NativeTextureLease(IntPtr texture) => _texture = texture;

        public void Dispose()
        {
            if (_texture == IntPtr.Zero)
            {
                return;
            }

            unsafe
            {
                var vtable = *(IntPtr*)_texture;
                var releasePtr = ((IntPtr*)vtable)[2];
                var release = Marshal.GetDelegateForFunctionPointer<NativeReleaseDelegate>(releasePtr);
                release(_texture);
            }

            _texture = IntPtr.Zero;
        }
    }

    private sealed class PreviewAngleD3D11ComLease : IDisposable
    {
        private object? _openedResource;
        private object? _texture;

        public PreviewAngleD3D11ComLease(object openedResource, object texture)
        {
            _openedResource = openedResource;
            _texture = texture;
        }

        public void Dispose()
        {
            ReleaseComObject(_texture);
            _texture = null;
            ReleaseComObject(_openedResource);
            _openedResource = null;
        }
    }
}
