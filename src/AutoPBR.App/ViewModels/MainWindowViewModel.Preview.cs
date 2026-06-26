using System.Collections.ObjectModel;
using System.Numerics;

using Avalonia.Input;
using Avalonia.Threading;

using AutoPBR.App.Controls;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.App.Lang;
using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoPBR.App.ViewModels;

public partial class MainWindowViewModel
{
    private GlPbrPreviewControl? _glPreview;
    private PreviewTextureMaps? _lastPreviewTextureMaps;
    private PreviewModelSubject? _lastPreviewModelSubject;
    private PreviewMeshProvenance? _lastPreviewMeshProvenance;
    private PreviewMeshProvenance? _lastLoggedPreviewMeshProvenance;
    private string? _lastPreview3DLoggedError;
    private DispatcherTimer? _preview3DCameraPoseTimer;
    private CancellationTokenSource? _preview3DSpriteThicknessDebounceCts;

    public static string[] Preview3DEntityAlphaModeOptions { get; } =
    [
        LocalizedStrings.Preview3DEntityAlphaModeOpaque,
        LocalizedStrings.Preview3DEntityAlphaModeCutout,
        LocalizedStrings.Preview3DEntityAlphaModeBlend
    ];

    public ObservableCollection<string> Preview3DCameraResetKeyChoices { get; } =
        new(["R", "Home", "Escape", "Back", "Delete", "F5", "Space"]);

    [ObservableProperty] private int _previewDisplayMode;
    [ObservableProperty] private bool _preview3DAutoRotate = true;
    [ObservableProperty] private double _preview3DEntityAnimationSpeed = 1.0;
    [ObservableProperty] private double _preview3DEntityAnimationAmplitude = 1.0;
    [ObservableProperty] private bool _preview3DEnableEntityAnimation = true;
    [ObservableProperty] private bool _preview3DEnableLegacyEntityWobble;
    [ObservableProperty] private bool _preview3DForceEntityCpuSkinning;
    [ObservableProperty] private bool _preview3DPauseEntityIdleAnimation;
    [ObservableProperty] private bool _preview3DItemUseAlphaBlend;
    [ObservableProperty] private int _preview3DEntityAlphaMode = 1;
    [ObservableProperty] private bool _preview3DEnableEntityLabPbrShading = true;
    [ObservableProperty] private bool _preview3DEnableEntityParallax;
    [ObservableProperty] private bool _preview3DShowGrid = true;
    [ObservableProperty] private bool _preview3DShowGroundMesh = true;
    [ObservableProperty] private bool _preview3DShowAxes = true;
    [ObservableProperty] private bool _preview3DEnableParallax = true;
    [ObservableProperty] private bool _preview3DEnableNormalMap = true;
    [ObservableProperty] private bool _preview3DEnableSpecularMap = true;
    [ObservableProperty] private double _preview3DParallaxHeightStrength = 0.05;
    [ObservableProperty] private bool _preview3DEnableSss = true;
    [ObservableProperty] private bool _preview3DEnableParallaxShadow = true;
    [ObservableProperty] private bool _preview3DEnableParallaxAo = true;
    [ObservableProperty] private double _preview3DParallaxAoStrength = 1.0;
    [ObservableProperty] private bool _preview3DEnableIbl = true;
    [ObservableProperty] private double _preview3DIblStrength = 0.6;
    [ObservableProperty] private bool _preview3DEnableAtmosphericSky = true;
    [ObservableProperty] private double _preview3DAtmosphereTurbidity = 2.6;
    [ObservableProperty] private double _preview3DAtmosphereSunIntensity = 10.0;
    [ObservableProperty] private double _preview3DAtmosphereHorizonFalloff = 1.35;
    [ObservableProperty] private double _preview3DAtmosphereSkyExposure = 0.85;
    [ObservableProperty] private double _preview3DAtmosphereSunDiscStrength = 0.35;
    [ObservableProperty] private double _preview3DAtmosphereSunDiscSize = 1.0;
    [ObservableProperty] private double _preview3DHorizonFogStrength = 1.0;
    [ObservableProperty] private double _preview3DTimeOfDayHours = 12.0;
    [ObservableProperty] private bool _preview3DAnimateTimeOfDay;
    [ObservableProperty] private double _preview3DTimeOfDaySpeed = 1.0;
    [ObservableProperty] private bool _preview3DEnableShadows = true;
    [ObservableProperty] private double _preview3DLightYawDegrees = -35.0;
    [ObservableProperty] private double _preview3DLightPitchDegrees = -55.0;
    [ObservableProperty] private bool _preview3DEnableShadowCascades;
    [ObservableProperty] private int _preview3DSpritePlaneCount = 1;
    [ObservableProperty] private double _preview3DSpriteThickness;
    [ObservableProperty] private double _preview3DCameraOrbitSensitivity = 0.006;
    [ObservableProperty] private double _preview3DCameraPanSensitivity = 0.0022;
    [ObservableProperty] private double _preview3DCameraZoomSensitivity = 0.12;
    [ObservableProperty] private double _preview3DCameraOrbitBoomDistance = PreviewCamera.DefaultOrbitBoomArmDistance;
    [ObservableProperty] private string _preview3DCameraResetKey = "R";
    [ObservableProperty] private double _preview3DCameraFlyLookSensitivity = 0.006;
    [ObservableProperty] private bool _preview3DCameraInvertLookY;
    [ObservableProperty] private double _preview3DCameraFlyMoveSpeed = 1.0;
    [ObservableProperty] private bool _preview3DCameraFlySmoothAcceleration = true;
    [ObservableProperty] private string? _preview3DCameraDebugText;
    [ObservableProperty] private bool _specularForceNoEmissive;
    [ObservableProperty] private bool _preview3DEnableGodRays = true;
    [ObservableProperty] private bool _preview3DEnableVolumetricClouds;
    [ObservableProperty] private int _preview3DVolumetricQuality = 1;
    [ObservableProperty] private double _preview3DGodRayStrength = 0.45;
    [ObservableProperty] private double _preview3DGodRayScatterGain = 3.4;
    [ObservableProperty] private double _preview3DGodRayExtinction = 1.15;
    [ObservableProperty] private double _preview3DGodRayDebugDensity;
    [ObservableProperty] private bool _preview3DGodRayStabilizeDebug = true;
    [ObservableProperty] private double _preview3DCloudDensity = 0.35;
    [ObservableProperty] private double _preview3DCloudCoverageScale = 1.0;
    [ObservableProperty] private double _preview3DCloudLayerHeight;
    [ObservableProperty] private double _preview3DCloudVolumeHeight = 24.0;
    [ObservableProperty] private double _preview3DCloudVolumeSize = 48.0;
    [ObservableProperty] private double _preview3DCloudWindSpeed = 1.5;
    [ObservableProperty] private double _preview3DCloudWindHeadingDegrees = 35.0;
    [ObservableProperty] private double _preview3DCloudCirrusStrength = 0.45;
    [ObservableProperty] private int _preview3DCloudDebugView;
    [ObservableProperty] private bool _preview3DCloudDisableTemporal;
    [ObservableProperty] private double _preview3DCloudMarchStepOverride;
    [ObservableProperty] private bool _preview3DCloudFreezeWind;
    [ObservableProperty] private bool _preview3DEnablePreviewTaa = true;
    [ObservableProperty] private bool _preview3DGpuInitOverlayVisible;
    [ObservableProperty] private string _preview3DGpuInitOverlayText = PreviewGpuInitProgress.Starting.Phase;
    [ObservableProperty] private double _preview3DGpuInitProgressFraction;
    [ObservableProperty] private bool _preview3DGpuInitProgressIndeterminate = true;

    public string[] Preview3DVolumetricQualityOptions { get; } =
    [
        LocalizedStrings.Preview3DVolumetricQualityLow,
        LocalizedStrings.Preview3DVolumetricQualityMedium,
        LocalizedStrings.Preview3DVolumetricQualityHigh
    ];

    internal void InitPreviewShaderPrewarm()
    {
        PreviewShaderPrewarm.EnsureStarted();
        PreviewShaderPrewarm.ProgressChanged += () => RunOnUiThread(OnPreviewShaderPrewarmProgress);
    }

    private void OnPreviewShaderPrewarmProgress()
    {
        if (_glPreview is not null || !IsPreview3D)
        {
            return;
        }

        ApplyEarlyPrewarmOverlay();
    }

    private void ApplyEarlyPrewarmOverlay()
    {
        Preview3DGpuInitOverlayVisible = true;
        Preview3DGpuInitOverlayText = PreviewShaderPrewarm.IsComplete
            ? "Starting GPU preview…"
            : "Preparing shader sources…";
        Preview3DGpuInitProgressFraction = Math.Clamp(PreviewShaderPrewarm.Fraction * 0.18, 0.0, 0.18);
        Preview3DGpuInitProgressIndeterminate = Preview3DGpuInitProgressFraction <= 0.001;
    }

    public string[] Preview3DCloudDebugViewOptions { get; } =
    [
        LocalizedStrings.Preview3DCloudDebugViewOff,
        LocalizedStrings.Preview3DCloudDebugViewCoverage,
        LocalizedStrings.Preview3DCloudDebugViewDensitySlice
    ];

    public bool IsPreview2D => PreviewDisplayMode == 0;
    public bool IsPreview3D => PreviewDisplayMode == 1;
    public bool IsPreview3DItemMode =>
        IsPreview3D &&
        (_lastPreviewTextureMaps?.Sprite2DFoliageTarget ?? false) &&
        (_lastPreviewTextureMaps?.IsItemTexturePath ?? false);
    public bool IsPreview3DFoliageSpriteMode =>
        IsPreview3D &&
        (_lastPreviewTextureMaps?.Sprite2DFoliageTarget ?? false) &&
        !(_lastPreviewTextureMaps?.IsItemTexturePath ?? false);
    public bool IsPreview3DSpriteMode => IsPreview3DItemMode || IsPreview3DFoliageSpriteMode;
    public bool IsPreviewSpriteTarget => _lastPreviewTextureMaps?.Sprite2DFoliageTarget ?? false;

    internal void RegisterGlPreview(GlPbrPreviewControl glPreview)
    {
        _glPreview = glPreview;
        glPreview.SetRendererLog(line => RunOnUiThread(() => AddLogLine(line)));
        glPreview.Backend.GpuInitProgressChanged += OnPreviewGpuInitProgressChanged;
        ApplyPreviewGpuInitOverlay(glPreview.Backend.GpuInitProgress);
        PushPreview3DCamera();
        Apply3DPreviewIfNeeded();
        EnsurePreview3DCameraPoseTimer();
    }

    private void OnPreviewGpuInitProgressChanged(PreviewGpuInitProgress progress) =>
        RunOnUiThread(() => ApplyPreviewGpuInitOverlay(progress));

    private void ApplyPreviewGpuInitOverlay(PreviewGpuInitProgress progress)
    {
        Preview3DGpuInitOverlayVisible = IsPreview3D && !progress.IsFullyReady;
        Preview3DGpuInitOverlayText = progress.Phase;
        Preview3DGpuInitProgressFraction = progress.ProgressFraction;
        Preview3DGpuInitProgressIndeterminate = progress.ProgressFraction <= 0.001;
    }

    [RelayCommand]
    private void ForceInvalidateShaderCache()
    {
        _glPreview?.InvalidateShaderCaches();
        AddLogLine(LocalizedStrings.ShaderCacheInvalidatedLog);
    }

    partial void OnPreviewDisplayModeChanged(int value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsPreview2D));
        OnPropertyChanged(nameof(IsPreview3D));
        OnPropertyChanged(nameof(IsPreview3DItemMode));
        OnPropertyChanged(nameof(IsPreview3DFoliageSpriteMode));
        OnPropertyChanged(nameof(IsPreview3DSpriteMode));
        OnPropertyChanged(nameof(IsPreviewSpriteTarget));
        if (!_loadingSettings)
        {
            SaveSettings();
        }

        if (IsPreview3D)
        {
            Apply3DPreviewIfNeeded();
            if (_glPreview is not null)
            {
                ApplyPreviewGpuInitOverlay(_glPreview.Backend.GpuInitProgress);
            }
            else if (!PreviewShaderPrewarm.IsComplete)
            {
                ApplyEarlyPrewarmOverlay();
            }
        }
        else
        {
            ScheduleRefreshPreviewIfActive(0);
        }
    }

    partial void OnSpecularForceNoEmissiveChanged(bool value)
    {
        _ = value;
        SaveSettings();
        ScheduleRefreshPreviewIfActive();
    }

    partial void OnPreview3DAutoRotateChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEntityAnimationSpeedChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEntityAnimationAmplitudeChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableEntityAnimationChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableLegacyEntityWobbleChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DForceEntityCpuSkinningChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DPauseEntityIdleAnimationChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DItemUseAlphaBlendChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEntityAlphaModeChanged(int value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableEntityLabPbrShadingChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableEntityParallaxChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DShowGridChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DShowGroundMeshChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DShowAxesChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableParallaxChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableNormalMapChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableSpecularMapChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DParallaxHeightStrengthChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableSssChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableParallaxShadowChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableParallaxAoChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DParallaxAoStrengthChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableIblChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DIblStrengthChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableAtmosphericSkyChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DAtmosphereTurbidityChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DAtmosphereSunIntensityChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DAtmosphereHorizonFalloffChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DAtmosphereSkyExposureChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DAtmosphereSunDiscStrengthChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DAtmosphereSunDiscSizeChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DHorizonFogStrengthChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DTimeOfDayHoursChanged(double value) => OnPreview3DTimeOfDayChanged(value);
    partial void OnPreview3DAnimateTimeOfDayChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DTimeOfDaySpeedChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableGodRaysChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableVolumetricCloudsChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DVolumetricQualityChanged(int value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DGodRayStrengthChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DGodRayScatterGainChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DGodRayExtinctionChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DGodRayDebugDensityChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DGodRayStabilizeDebugChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudDensityChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudCoverageScaleChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudLayerHeightChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudVolumeHeightChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudVolumeSizeChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudWindSpeedChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudWindHeadingDegreesChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudCirrusStrengthChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudDebugViewChanged(int value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudDisableTemporalChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudMarchStepOverrideChanged(double value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DCloudFreezeWindChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnablePreviewTaaChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DEnableShadowsChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DLightYawDegreesChanged(double value) => OnPreview3DLightDirectionChanged(value);
    partial void OnPreview3DLightPitchDegreesChanged(double value) => OnPreview3DLightDirectionChanged(value);
    partial void OnPreview3DEnableShadowCascadesChanged(bool value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DSpritePlaneCountChanged(int value) => OnPreview3DGpuSettingChanged(value);
    partial void OnPreview3DSpriteThicknessChanged(double value)
    {
        _ = value;
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
        ScheduleDebouncedSpriteThicknessMeshRebuild();
    }

    private void ScheduleDebouncedSpriteThicknessMeshRebuild()
    {
        _preview3DSpriteThicknessDebounceCts?.Cancel();
        _preview3DSpriteThicknessDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _preview3DSpriteThicknessDebounceCts = cts;
        _ = RunDebouncedSpriteThicknessMeshRebuildAsync(cts);
    }

    private async Task RunDebouncedSpriteThicknessMeshRebuildAsync(CancellationTokenSource debounceCts)
    {
        try
        {
            await Task.Delay(PreviewStageConstants.SpriteThicknessMeshDebounceMs, debounceCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!ReferenceEquals(_preview3DSpriteThicknessDebounceCts, debounceCts))
        {
            return;
        }

        RunOnUiThread(() =>
        {
            if (!ReferenceEquals(_preview3DSpriteThicknessDebounceCts, debounceCts))
            {
                return;
            }

            Push3DRenderSettingsOnly();
        });
    }

    partial void OnPreview3DCameraOrbitSensitivityChanged(double value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraPanSensitivityChanged(double value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraZoomSensitivityChanged(double value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraOrbitBoomDistanceChanged(double value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraResetKeyChanged(string value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraFlyLookSensitivityChanged(double value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraInvertLookYChanged(bool value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraFlyMoveSpeedChanged(double value) => OnPreview3DCameraSettingChanged(value);
    partial void OnPreview3DCameraFlySmoothAccelerationChanged(bool value) => OnPreview3DCameraSettingChanged(value);

    private bool _syncingPreviewLightFromTimeOfDay;

    private void OnPreview3DTimeOfDayChanged(double hours)
    {
        if (_loadingSettings || _syncingPreviewLightFromTimeOfDay)
        {
            return;
        }

        _syncingPreviewLightFromTimeOfDay = true;
        var (yaw, pitch) = PreviewLightMath.LightYawPitchFromTimeOfDay(hours);
        Preview3DLightYawDegrees = yaw;
        Preview3DLightPitchDegrees = pitch;
        _syncingPreviewLightFromTimeOfDay = false;
        OnPreview3DGpuSettingChanged(hours);
    }

    private void OnPreview3DLightDirectionChanged(double _)
    {
        if (_loadingSettings || _syncingPreviewLightFromTimeOfDay)
        {
            return;
        }

        _syncingPreviewLightFromTimeOfDay = true;
        Preview3DTimeOfDayHours = PreviewLightMath.TimeOfDayFromLightYawPitch(
            Preview3DLightYawDegrees,
            Preview3DLightPitchDegrees,
            Preview3DTimeOfDayHours);
        _syncingPreviewLightFromTimeOfDay = false;
        OnPreview3DGpuSettingChanged(_);
    }

    private void OnPreview3DGpuSettingChanged<T>(T _)
    {
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
        Push3DRenderSettingsOnly();
    }

    private void OnPreview3DCameraSettingChanged<T>(T _)
    {
        if (_loadingSettings)
        {
            return;
        }

        SaveSettings();
        PushPreview3DCamera();
        Push3DRenderSettingsOnly();
    }

    private void ApplyPreviewDetailedResult(PreviewDetailedResult previewResult)
    {
        _lastPreviewTextureMaps = previewResult.Maps;
        _lastPreviewModelSubject = previewResult.ModelSubject;
        _lastPreviewMeshProvenance = previewResult.MeshProvenance ?? previewResult.ModelSubject?.MeshProvenance;
        ApplyExploreParityCatalogPreviewDefaults(_lastPreviewModelSubject);
        LogPreviewMeshProvenance(_lastPreviewMeshProvenance);
        OnPropertyChanged(nameof(IsPreview3DItemMode));
        OnPropertyChanged(nameof(IsPreview3DFoliageSpriteMode));
        OnPropertyChanged(nameof(IsPreview3DSpriteMode));
        OnPropertyChanged(nameof(IsPreviewSpriteTarget));
        Apply3DPreviewIfNeeded();
    }

    private void ApplyExploreParityCatalogPreviewDefaults(PreviewModelSubject? subject)
    {
        if (subject?.EmulatedRebake?.AssetArchivePath is not { } path)
        {
            return;
        }

        var norm = path.Replace('\\', '/').TrimStart('/');
        if (!EntityTextureParityCatalog.IsCatalogued(norm))
        {
            return;
        }

        Preview3DEnableEntityAnimation = false;
        Preview3DEnableLegacyEntityWobble = false;
    }

    private PreviewRenderSettings BuildPreview3DRenderSettings()
    {
        var isItemFlatSprite = _lastPreviewTextureMaps is { Sprite2DFoliageTarget: true, IsItemTexturePath: true };
        return new()
        {
            NormalStrength = (float)NormalIntensity,
            HeightStrength = (float)Preview3DParallaxHeightStrength,
            RoughnessScale = (float)SmoothnessScale,
            EnableParallax = Preview3DEnableParallax,
            EnableNormalMap = Preview3DEnableNormalMap,
            EnableSpecularMap = Preview3DEnableSpecularMap,
            AutoRotate = Preview3DAutoRotate,
            LightYawDegrees = (float)Preview3DLightYawDegrees,
            LightPitchDegrees = (float)Preview3DLightPitchDegrees,
            ItemUseAlphaBlend = Preview3DItemUseAlphaBlend,
            EntityAlphaMode = (PreviewEntityAlphaMode)Math.Clamp(Preview3DEntityAlphaMode, 0, 2),
            EnableEntityLabPbrShading = Preview3DEnableEntityLabPbrShading,
            EnableEntityParallax = Preview3DEnableEntityParallax,
            SpritePlaneCount = isItemFlatSprite ? 1 : Math.Clamp(Preview3DSpritePlaneCount, 1, 8),
            SpriteThickness = (float)Math.Clamp(
                Preview3DSpriteThickness,
                PreviewStageConstants.SpriteThicknessMin,
                PreviewStageConstants.SpriteThicknessMax),
            ItemFlatSpritePreview = isItemFlatSprite,
            ShowBackgroundGrid = Preview3DShowGrid,
            ShowGroundMesh = Preview3DShowGroundMesh,
            ShowCornerAxes = Preview3DShowAxes,
            DrawPreviewSubject = _lastPreviewTextureMaps is not null,
            EnableSss = Preview3DEnableSss,
            EnableParallaxShadow = Preview3DEnableParallaxShadow,
            EnableParallaxAo = Preview3DEnableParallaxAo,
            ParallaxAoStrength = (float)Preview3DParallaxAoStrength,
            EnableIbl = Preview3DEnableIbl,
            IblStrength = (float)Preview3DIblStrength,
            EnableAtmosphericSky = Preview3DEnableAtmosphericSky,
            AtmosphereTurbidity = (float)Preview3DAtmosphereTurbidity,
            AtmosphereSunIntensity = (float)Preview3DAtmosphereSunIntensity,
            AtmosphereHorizonFalloff = (float)Preview3DAtmosphereHorizonFalloff,
            AtmosphereSkyExposure = (float)Preview3DAtmosphereSkyExposure,
            AtmosphereSunDiscStrength = (float)Preview3DAtmosphereSunDiscStrength,
            AtmosphereSunDiscSize = (float)Preview3DAtmosphereSunDiscSize,
            AerialFogStrength = (float)Preview3DHorizonFogStrength,
            TimeOfDayHours = (float)Preview3DTimeOfDayHours,
            AnimateTimeOfDay = Preview3DAnimateTimeOfDay,
            TimeOfDaySpeed = (float)Preview3DTimeOfDaySpeed,
            CapturePreviewFingerprint = DebugMode,
            EnableShadows = Preview3DEnableShadows,
            EnableShadowCascades = Preview3DEnableShadowCascades,
            EntityAnimationSpeed = (float)Preview3DEntityAnimationSpeed,
            EntityAnimationAmplitude = (float)Preview3DEntityAnimationAmplitude,
            EnableEntityAnimation = Preview3DEnableEntityAnimation,
            PauseEntityIdleAnimation = Preview3DPauseEntityIdleAnimation,
            EnableLegacyEntityWobble = Preview3DEnableLegacyEntityWobble,
            ForceEntityCpuSkinning = Preview3DForceEntityCpuSkinning,
            EnableGodRays = Preview3DEnableGodRays,
            EnableVolumeGodRays = true,
            EnableVolumetricClouds = Preview3DEnableVolumetricClouds,
            VolumetricQuality = Math.Clamp(Preview3DVolumetricQuality, 0, 2),
            GodRayStrength = (float)Preview3DGodRayStrength,
            GodRayScatterGain = (float)Preview3DGodRayScatterGain,
            GodRayExtinction = (float)Preview3DGodRayExtinction,
            GodRayDebugDensity = (float)Preview3DGodRayDebugDensity,
            GodRayStabilizeDebug = Preview3DGodRayStabilizeDebug,
            CloudDensity = (float)Preview3DCloudDensity,
            CloudCoverageScale = (float)Preview3DCloudCoverageScale,
            CloudLayerHeight = (float)Preview3DCloudLayerHeight,
            CloudVolumeHeight = (float)Preview3DCloudVolumeHeight,
            CloudVolumeSize = (float)Preview3DCloudVolumeSize,
            CloudWindSpeed = (float)Preview3DCloudWindSpeed,
            CloudWindHeadingDegrees = (float)Preview3DCloudWindHeadingDegrees,
            CloudCirrusStrength = (float)Preview3DCloudCirrusStrength,
            CloudDebugView = (PreviewCloudDebugView)Math.Clamp(Preview3DCloudDebugView, 0, 2),
            CloudDisableTemporal = Preview3DCloudDisableTemporal,
            CloudMarchStepOverride = (int)Math.Clamp(Math.Round(Preview3DCloudMarchStepOverride), 0, 64),
            CloudFreezeWind = Preview3DCloudFreezeWind,
            EnablePreviewTaa = Preview3DEnablePreviewTaa,
            CloudQuality = PreviewVolumetricQuality.Resolve(Math.Clamp(Preview3DVolumetricQuality, 0, 2)).CloudQuality,
            LogVolumetricTiming = DebugMode
        };
    }

    private void PushPreview3DCamera()
    {
        if (_glPreview is null)
        {
            return;
        }

        var resetKey = Enum.TryParse<Key>(Preview3DCameraResetKey, ignoreCase: true, out var parsedKey)
            ? parsedKey
            : Key.R;
        _glPreview.SetCameraInteractionFromSettings(
            (float)Preview3DCameraOrbitSensitivity,
            (float)Preview3DCameraPanSensitivity,
            (float)Preview3DCameraZoomSensitivity,
            (float)Preview3DCameraFlyLookSensitivity,
            Preview3DCameraInvertLookY,
            (float)Preview3DCameraFlyMoveSpeed,
            Preview3DCameraFlySmoothAcceleration,
            resetKey,
            (float)Preview3DCameraOrbitBoomDistance);
    }

    private void Push3DRenderSettingsOnly()
    {
        if (_glPreview is null || !IsPreview3D)
        {
            return;
        }

        try
        {
            _glPreview.UpdatePreview3DSettings(BuildPreview3DRenderSettings());
            _lastPreview3DLoggedError = null;
        }
        catch (Exception ex)
        {
            if (!string.Equals(_lastPreview3DLoggedError, ex.Message, StringComparison.Ordinal))
            {
                _lastPreview3DLoggedError = ex.Message;
                AddLogLine($"[Preview 3D] GPU update failed: {ex.Message}");
            }
        }
    }

    private void Push3DPreviewStateToGpu()
    {
        if (_glPreview is null || !IsPreview3D)
        {
            return;
        }

        var settings = BuildPreview3DRenderSettings();
        var maps = _lastPreviewTextureMaps;
        var material = maps is not null
            ? PreviewMaterialMapper.FromCoreMaps(maps)
            : IdlePreview3DMaterial();

        PreviewSceneKind kind;
        PreviewModelSubject? subject = _lastPreviewModelSubject;
        PreviewMaterial[]? slotMaterials = null;

        if (maps is { Sprite2DFoliageTarget: true, IsItemTexturePath: true })
        {
            kind = PreviewSceneKind.ItemPlane;
            subject = null;
        }
        else if (subject is { Materials.Length: > 0 })
        {
            kind = PreviewSceneKind.BlockModel;
            slotMaterials = subject.Materials.Select(PreviewMaterialMapper.FromCoreMaps).ToArray();
        }
        else if (maps?.Sprite2DFoliageTarget == true)
        {
            kind = PreviewSceneKind.ItemPlane;
            subject = null;
        }
        else
        {
            kind = PreviewSceneKind.BlockCube;
            subject = null;
        }

        try
        {
            _glPreview.UpdatePreview3D(material, settings, kind, subject, slotMaterials);
            _lastPreview3DLoggedError = null;
        }
        catch (Exception ex)
        {
            if (!string.Equals(_lastPreview3DLoggedError, ex.Message, StringComparison.Ordinal))
            {
                _lastPreview3DLoggedError = ex.Message;
                AddLogLine($"[Preview 3D] GPU update failed: {ex.Message}");
            }
        }
    }

    private void Apply3DPreviewIfNeeded()
    {
        if (IsPreview3D)
        {
            Push3DPreviewStateToGpu();
        }
    }

    private static PreviewMaterial IdlePreview3DMaterial()
    {
        var rgba = new byte[] { 128, 128, 128, 255 };
        return PreviewMaterialMapper.FromCoreMaps(new PreviewTextureMaps
        {
            Width = 1,
            Height = 1,
            DiffuseRgba = rgba
        });
    }

    private void LogPreviewMeshProvenance(PreviewMeshProvenance? provenance)
    {
        if (Nullable.Equals(provenance, _lastLoggedPreviewMeshProvenance))
        {
            return;
        }

        _lastLoggedPreviewMeshProvenance = provenance;
        if (provenance is { } p)
        {
            AddLogLine(p.ToLogLine());
        }
    }

    private void EnsurePreview3DCameraPoseTimer()
    {
        if (_preview3DCameraPoseTimer is not null)
        {
            return;
        }

        _preview3DCameraPoseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _preview3DCameraPoseTimer.Tick += OnPreview3DCameraPoseTimerTick;
        _preview3DCameraPoseTimer.Start();
    }

    private void OnPreview3DCameraPoseTimerTick(object? sender, EventArgs e)
    {
        if (!IsPreview3D || _glPreview is null)
        {
            Preview3DCameraDebugText = null;
            return;
        }

        var lines = new List<string>();
        if (_lastPreviewMeshProvenance is { } provenance)
        {
            lines.Add(provenance.ToOverlayLine());
        }

        if (_glPreview.Backend.TryGetCameraDebugPose(out Vector3 eye, out Vector3 target))
        {
            lines.Add($"Eye: {eye.X:0.00}, {eye.Y:0.00}, {eye.Z:0.00}");
            lines.Add($"Target: {target.X:0.00}, {target.Y:0.00}, {target.Z:0.00}");
        }

        Preview3DCameraDebugText = lines.Count > 0 ? string.Join('\n', lines) : null;
    }

    private void DisposePreviewResources()
    {
        _preview3DSpriteThicknessDebounceCts?.Cancel();
        _preview3DSpriteThicknessDebounceCts?.Dispose();
        _preview3DSpriteThicknessDebounceCts = null;

        if (_preview3DCameraPoseTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnPreview3DCameraPoseTimerTick;
            _preview3DCameraPoseTimer = null;
        }
    }
}
