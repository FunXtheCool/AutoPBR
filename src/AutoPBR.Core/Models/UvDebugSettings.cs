namespace AutoPBR.Core.Models;

/// <summary>
/// Optional UV Debug window overrides. When unset, <see cref="Preview.PreviewUvBakePolicy"/> production baselines apply.
/// </summary>
public static class UvDebugSettings
{
    private static bool _flipUOverride;
    private static bool _flipVOverride;
    private static bool _swapFaceNorthSouthOverride;
    private static bool _swapFaceEastWestOverride;
    private static bool _swapFaceUpDownOverride;
    private static bool _preserveDirectionalBoundsOverride;
    private static bool _useBottomLeftUvOriginOverride;
    private static bool _uvCornerOrderModeOverride;
    private static bool _offsetUPixelsOverride;
    private static bool _offsetVPixelsOverride;
    private static bool _globalFaceRotationOverride;

    private static bool _flipU;
    private static bool _flipV;
    private static double _offsetUPixels;
    private static double _offsetVPixels;
    private static int _globalFaceRotationDegrees;
    private static bool _swapFaceNorthSouth;
    private static bool _swapFaceEastWest;
    private static bool _swapFaceUpDown;
    private static bool _preserveDirectionalBounds = true;
    private static bool _useBottomLeftUvOrigin;
    private static int _uvCornerOrderMode;

    public static bool FlipU => _flipU;
    public static bool FlipV => _flipV;
    public static double OffsetUPixels => _offsetUPixels;
    public static double OffsetVPixels => _offsetVPixels;
    public static int GlobalFaceRotationDegrees => _globalFaceRotationDegrees;
    public static bool SwapFaceNorthSouth => _swapFaceNorthSouth;
    public static bool SwapFaceEastWest => _swapFaceEastWest;
    public static bool SwapFaceUpDown => _swapFaceUpDown;
    public static bool PreserveDirectionalBounds => _preserveDirectionalBounds;
    public static bool UseBottomLeftUvOrigin => _useBottomLeftUvOrigin;
    public static int UvCornerOrderMode => _uvCornerOrderMode;

    public static bool HasActiveOverrides =>
        _flipUOverride ||
        _flipVOverride ||
        _swapFaceNorthSouthOverride ||
        _swapFaceEastWestOverride ||
        _swapFaceUpDownOverride ||
        _preserveDirectionalBoundsOverride ||
        _useBottomLeftUvOriginOverride ||
        _uvCornerOrderModeOverride ||
        _offsetUPixelsOverride ||
        _offsetVPixelsOverride ||
        _globalFaceRotationOverride;

    public static void ResetAllOverrides()
    {
        _flipUOverride = false;
        _flipVOverride = false;
        _swapFaceNorthSouthOverride = false;
        _swapFaceEastWestOverride = false;
        _swapFaceUpDownOverride = false;
        _preserveDirectionalBoundsOverride = false;
        _useBottomLeftUvOriginOverride = false;
        _uvCornerOrderModeOverride = false;
        _offsetUPixelsOverride = false;
        _offsetVPixelsOverride = false;
        _globalFaceRotationOverride = false;

        _flipU = false;
        _flipV = false;
        _offsetUPixels = 0;
        _offsetVPixels = 0;
        _globalFaceRotationDegrees = 0;
        _swapFaceNorthSouth = false;
        _swapFaceEastWest = false;
        _swapFaceUpDown = false;
        _preserveDirectionalBounds = true;
        _useBottomLeftUvOrigin = false;
        _uvCornerOrderMode = 0;
    }

    public static bool TryGetFlipUOverride(out bool value) => TryRead(_flipUOverride, _flipU, out value);
    public static bool TryGetFlipVOverride(out bool value) => TryRead(_flipVOverride, _flipV, out value);
    public static bool TryGetSwapFaceNorthSouthOverride(out bool value) => TryRead(_swapFaceNorthSouthOverride, _swapFaceNorthSouth, out value);
    public static bool TryGetSwapFaceEastWestOverride(out bool value) => TryRead(_swapFaceEastWestOverride, _swapFaceEastWest, out value);
    public static bool TryGetSwapFaceUpDownOverride(out bool value) => TryRead(_swapFaceUpDownOverride, _swapFaceUpDown, out value);
    public static bool TryGetPreserveDirectionalBoundsOverride(out bool value) => TryRead(_preserveDirectionalBoundsOverride, _preserveDirectionalBounds, out value);
    public static bool TryGetUseBottomLeftUvOriginOverride(out bool value) => TryRead(_useBottomLeftUvOriginOverride, _useBottomLeftUvOrigin, out value);
    public static bool TryGetUvCornerOrderModeOverride(out int value) => TryRead(_uvCornerOrderModeOverride, _uvCornerOrderMode, out value);
    public static bool TryGetOffsetUPixelsOverride(out float value) => TryRead(_offsetUPixelsOverride, (float)_offsetUPixels, out value);
    public static bool TryGetOffsetVPixelsOverride(out float value) => TryRead(_offsetVPixelsOverride, (float)_offsetVPixels, out value);
    public static bool TryGetGlobalFaceRotationDegreesOverride(out int value) => TryRead(_globalFaceRotationOverride, _globalFaceRotationDegrees, out value);

    public static void SetFlipU(bool value, bool isOverride = true) => SetBool(ref _flipUOverride, ref _flipU, value, isOverride);
    public static void SetFlipV(bool value, bool isOverride = true) => SetBool(ref _flipVOverride, ref _flipV, value, isOverride);
    public static void SetSwapFaceNorthSouth(bool value, bool isOverride = true) => SetBool(ref _swapFaceNorthSouthOverride, ref _swapFaceNorthSouth, value, isOverride);
    public static void SetSwapFaceEastWest(bool value, bool isOverride = true) => SetBool(ref _swapFaceEastWestOverride, ref _swapFaceEastWest, value, isOverride);
    public static void SetSwapFaceUpDown(bool value, bool isOverride = true) => SetBool(ref _swapFaceUpDownOverride, ref _swapFaceUpDown, value, isOverride);
    public static void SetPreserveDirectionalBounds(bool value, bool isOverride = true) => SetBool(ref _preserveDirectionalBoundsOverride, ref _preserveDirectionalBounds, value, isOverride);
    public static void SetUseBottomLeftUvOrigin(bool value, bool isOverride = true) => SetBool(ref _useBottomLeftUvOriginOverride, ref _useBottomLeftUvOrigin, value, isOverride);
    public static void SetUvCornerOrderMode(int value, bool isOverride = true) => SetValue(ref _uvCornerOrderModeOverride, ref _uvCornerOrderMode, value, isOverride);
    public static void SetOffsetUPixels(double value, bool isOverride = true) => SetDouble(ref _offsetUPixelsOverride, ref _offsetUPixels, value, isOverride);
    public static void SetOffsetVPixels(double value, bool isOverride = true) => SetDouble(ref _offsetVPixelsOverride, ref _offsetVPixels, value, isOverride);
    public static void SetGlobalFaceRotationDegrees(int value, bool isOverride = true) => SetValue(ref _globalFaceRotationOverride, ref _globalFaceRotationDegrees, value, isOverride);

    private static bool TryRead<T>(bool hasOverride, T value, out T result)
    {
        result = value;
        return hasOverride;
    }

    private static void SetBool(ref bool flag, ref bool field, bool value, bool isOverride)
    {
        field = value;
        flag = isOverride;
    }

    private static void SetValue<T>(ref bool flag, ref T field, T value, bool isOverride)
    {
        field = value;
        flag = isOverride;
    }

    private static void SetDouble(ref bool flag, ref double field, double value, bool isOverride)
    {
        field = value;
        flag = isOverride;
    }
}
