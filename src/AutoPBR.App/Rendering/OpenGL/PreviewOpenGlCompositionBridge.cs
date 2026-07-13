using System.Reflection;

using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.OpenGL.Egl;
using Avalonia.Platform;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Resolves ANGLE presentation context + external-memory exports from <see cref="OpenGlControlBase"/>.</summary>
internal sealed class PreviewOpenGlCompositionBridge
{
    private static readonly FieldInfo? ResourcesField = typeof(OpenGlControlBase)
        .GetField("_resources", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly IGlContext _presentationContext;
    private readonly IGlContextExternalObjectsFeature _externalObjects;
    private readonly IGlExportableExternalImageTexture?[] _sharedExports = new IGlExportableExternalImageTexture?[2];
    private PixelSize _sharedExportSize;
    private PreviewAngleD3D11Presentation _cachedPresentation;
    private string? _cachedPresentationDeviceDetail;

    private PreviewOpenGlCompositionBridge(IGlContext presentationContext, IGlContextExternalObjectsFeature externalObjects)
    {
        _presentationContext = presentationContext;
        _externalObjects = externalObjects;
    }

    public IGlContext PresentationContext => _presentationContext;

    public IGlContextExternalObjectsFeature ExternalObjects => _externalObjects;

    public bool TryWarmPresentationCache() =>
        TryResolvePresentation(out _, out _);

    public bool TryResolvePresentationDevice(out IntPtr d3d11Device, out string resolutionDetail)
    {
        d3d11Device = IntPtr.Zero;
        if (!TryResolvePresentation(out var presentation, out resolutionDetail))
        {
            return false;
        }

        d3d11Device = presentation.NativeDevice;
        return d3d11Device != IntPtr.Zero;
    }

    public bool TryResolvePresentation(out PreviewAngleD3D11Presentation presentation, out string resolutionDetail)
    {
        if (_cachedPresentation.IsValid)
        {
            presentation = _cachedPresentation;
            resolutionDetail = _cachedPresentationDeviceDetail ?? "cached";
            return true;
        }

        IDisposable? restorePresentationContext = BindPresentationContextIfNeeded(_presentationContext);
        try
        {
            if (!PreviewAngleD3D11Access.TryResolvePresentation(
                    _externalObjects,
                    _presentationContext,
                    out presentation,
                    out resolutionDetail))
            {
                return false;
            }
        }
        finally
        {
            restorePresentationContext?.Dispose();
        }

        _cachedPresentation = presentation;
        _cachedPresentationDeviceDetail = resolutionDetail;
        return true;
    }

    private bool IsPresentationContextCurrent()
    {
        if (_presentationContext is EglContext egl)
        {
            return egl.IsCurrent;
        }

        var isCurrent = _presentationContext.GetType().GetProperty("IsCurrent", BindingFlags.Instance | BindingFlags.Public);
        return isCurrent?.GetValue(_presentationContext) is true;
    }

    internal static IDisposable? BindPresentationContextIfNeeded(IGlContext context)
    {
        if (context is EglContext egl)
        {
            return egl.EnsureCurrent();
        }

        var isCurrent = context.GetType().GetProperty("IsCurrent", BindingFlags.Instance | BindingFlags.Public);
        if (isCurrent?.GetValue(context) is true)
        {
            return null;
        }

        return context.MakeCurrent();
    }

    public static bool TryCreate(OpenGlControlBase control, out PreviewOpenGlCompositionBridge? bridge)
    {
        bridge = null;
        if (ResourcesField?.GetValue(control) is not { } resources)
        {
            return false;
        }

        var contextProp = resources.GetType().GetProperty("Context", BindingFlags.Instance | BindingFlags.Public);
        if (contextProp?.GetValue(resources) is not IGlContext context)
        {
            return false;
        }

        if (!TryResolveExternalObjects(context, resources, out var externalObjects))
        {
            return false;
        }

        bridge = new PreviewOpenGlCompositionBridge(context, externalObjects);
        return true;
    }

    public bool TryEnsureSharedExport(PixelSize size, out IGlExportableExternalImageTexture export)
    {
        export = null!;
        if (!TryEnsureSharedExportPair(size, out var exports))
        {
            return false;
        }

        export = exports[0];
        return true;
    }

    public bool TryEnsureSharedExportPair(PixelSize size, out IGlExportableExternalImageTexture[] exports)
    {
        exports = null!;
        if (_sharedExports[0] is not null &&
            _sharedExports[1] is not null &&
            _sharedExportSize == size)
        {
            exports = [_sharedExports[0]!, _sharedExports[1]!];
            return true;
        }

        DisposeSharedExports();

        var restorePresentationContext = BindPresentationContextIfNeeded(_presentationContext);
        try
        {
            for (var i = 0; i < _sharedExports.Length; i++)
            {
                _sharedExports[i] = _externalObjects.CreateImage(
                    KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle,
                    size,
                    PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm);
            }
        }
        finally
        {
            restorePresentationContext?.Dispose();
        }

        if (_sharedExports[0] is null || _sharedExports[1] is null)
        {
            DisposeSharedExports();
            return false;
        }

        _sharedExportSize = size;
        exports = [_sharedExports[0]!, _sharedExports[1]!];
        return true;
    }

    public void InvalidateSharedExports() => DisposeSharedExports();

    public void Dispose()
    {
        DisposeSharedExports();
        _cachedPresentation = default;
        _cachedPresentationDeviceDetail = null;
    }

    private void DisposeSharedExports()
    {
        for (var i = 0; i < _sharedExports.Length; i++)
        {
            _sharedExports[i]?.Dispose();
            _sharedExports[i] = null;
        }

        _sharedExportSize = default;
    }

    private static bool TryResolveExternalObjects(
        IGlContext context,
        object resources,
        out IGlContextExternalObjectsFeature externalObjects)
    {
        if (context.TryGetFeature<IGlContextExternalObjectsFeature>(out externalObjects) &&
            SupportsD3D11SharedExport(externalObjects))
        {
            return true;
        }

        var swapchainField = resources.GetType().GetField("_swapchain", BindingFlags.Instance | BindingFlags.NonPublic);
        if (swapchainField?.GetValue(resources) is { } swapchain)
        {
            var externalField = swapchain.GetType().GetField(
                "_externalObjectsFeature",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (externalField?.GetValue(swapchain) is IGlContextExternalObjectsFeature fromSwapchain &&
                SupportsD3D11SharedExport(fromSwapchain))
            {
                externalObjects = fromSwapchain;
                return true;
            }
        }

        externalObjects = null!;
        return false;
    }

    private static bool SupportsD3D11SharedExport(IGlContextExternalObjectsFeature externalObjects) =>
        externalObjects.SupportedExportableExternalImageTypes.Contains(
            KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureGlobalSharedHandle);
}
