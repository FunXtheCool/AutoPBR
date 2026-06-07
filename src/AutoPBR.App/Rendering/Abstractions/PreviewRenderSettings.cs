namespace AutoPBR.App.Rendering.Abstractions;

public sealed class PreviewRenderSettings
{
    public float NormalStrength { get; init; } = 1f;
    public float HeightStrength { get; init; } = 0.05f;
    public float SpecularStrength { get; init; } = 1f;
    public float RoughnessScale { get; init; } = 1f;
    public float AmbientStrength { get; init; } = 0.35f;
    public float Exposure { get; init; } = 1f;
    public bool EnableParallax { get; init; } = true;
    public bool EnableNormalMap { get; init; } = true;
    public bool EnableSpecularMap { get; init; } = true;
    public bool AutoRotate { get; init; } = true;
    public float LightYawDegrees { get; init; } = -35f;
    public float LightPitchDegrees { get; init; } = -55f;
    public bool NearestTextureFilter { get; init; } = true;
    /// <summary>Item plane: alpha test threshold in linear 0..1.</summary>
    public float AlphaCutoff { get; init; } = 0.5f;
    public bool ItemUseAlphaBlend { get; init; }

    /// <summary>Emulated entity meshes: alpha test / blend for diffuse and shadow passes.</summary>
    public PreviewEntityAlphaMode EntityAlphaMode { get; init; } = PreviewEntityAlphaMode.Cutout;

    /// <summary>
    /// When true (default), runtime entity preview uses normal + specular maps like block previews.
    /// Turn off if a rig shows tangent mismatch artifacts.
    /// </summary>
    public bool EnableEntityLabPbrShading { get; init; } = true;

    /// <summary>
    /// When true, POM / parallax AO / parallax self-shadow apply to emulated entity meshes (off by default; sensitive to UV seams).
    /// </summary>
    public bool EnableEntityParallax { get; init; }

    public int SpritePlaneCount { get; init; } = 1;

    /// <summary>Draw a subtle XZ grid under the preview object in 3D mode.</summary>
    public bool ShowBackgroundGrid { get; init; } = true;

    /// <summary>Draw the textured grass ground plane under the preview object in 3D mode.</summary>
    public bool ShowGroundMesh { get; init; } = true;

    /// <summary>Draw RGB world-axis lines in a corner (matches block Y-rotation).</summary>
    public bool ShowCornerAxes { get; init; } = true;

    /// <summary>
    /// When false, only the shared environment is drawn (sky, sun, ground plane, grid/axes); the preview cube/item mesh is omitted until maps exist.
    /// </summary>
    public bool DrawPreviewSubject { get; init; } = true;

    /// <summary>Genesis: cheap subsurface scattering approximation when LabPBR _s.b >= 65.</summary>
    public bool EnableSss { get; init; } = true;

    /// <summary>Genesis: parallax self-shadow trace toward the light from the POM hit point.</summary>
    public bool EnableParallaxShadow { get; init; } = true;

    /// <summary>Genesis: toggle for POM-derived contact ambient occlusion.</summary>
    public bool EnableParallaxAo { get; init; } = true;

    /// <summary>Genesis: contact ambient occlusion strength derived from the POM hit neighborhood.</summary>
    public float ParallaxAoStrength { get; init; } = 1f;

    /// <summary>Genesis: environment IBL — sky LUT + reflected sun when atmospheric sky is on; otherwise procedural hemisphere.</summary>
    public bool EnableIbl { get; init; } = true;

    /// <summary>Atmospheric sky: render LUT-driven sky background and ambient probes.</summary>
    public bool EnableAtmosphericSky { get; init; } = true;

    /// <summary>Atmospheric sky: aerosol concentration proxy (higher = hazier horizon).</summary>
    public float AtmosphereTurbidity { get; init; } = 2.6f;

    /// <summary>Atmospheric sky: sun radiance multiplier for sky/background lighting.</summary>
    public float AtmosphereSunIntensity { get; init; } = 10f;

    /// <summary>Atmospheric sky: controls horizon extinction rolloff.</summary>
    public float AtmosphereHorizonFalloff { get; init; } = 1.35f;

    /// <summary>Atmospheric sky: master brightness on the composite sky pass.</summary>
    public float AtmosphereSkyExposure { get; init; } = 0.85f;

    /// <summary>Atmospheric sky: additive glare around the sun disc (separate from scatter intensity).</summary>
    public float AtmosphereSunDiscStrength { get; init; } = 0.35f;

    /// <summary>Horizon fog on sky dome below horizon and aerial perspective on geometry.</summary>
    public float AerialFogStrength { get; init; } = 1f;

    /// <summary>Clock time (0–24 h) for sun/moon cycle UI; drives yaw/pitch when edited.</summary>
    public float TimeOfDayHours { get; init; } = 12f;

    /// <summary>When true, advance <see cref="TimeOfDayHours"/> from render time (full cycle in 24 / speed seconds).</summary>
    public bool AnimateTimeOfDay { get; init; }

    /// <summary>Game-hours advanced per real second when <see cref="AnimateTimeOfDay"/> is on.</summary>
    public float TimeOfDaySpeed { get; init; } = 1f;

    /// <summary>Debug: downsample and hash the default framebuffer after the preview frame (diagnostics only).</summary>
    public bool CapturePreviewFingerprint { get; init; }

    /// <summary>Genesis: SSS contribution scalar (multiplier on wrap + transmission lobes).</summary>
    public float SssStrength { get; init; } = 1f;

    /// <summary>Genesis: indirect environment IBL contribution scalar.</summary>
    public float IblStrength { get; init; } = 0.6f;

    /// <summary>Genesis: emission scalar (LabPBR _s.a additive).</summary>
    public float EmissionStrength { get; init; } = 1f;

    /// <summary>Render-time animation speed multiplier for emulated entity preview rigs.</summary>
    public float EntityAnimationSpeed { get; init; } = 1f;

    /// <summary>Render-time animation amplitude multiplier for emulated entity preview rigs.</summary>
    public float EntityAnimationAmplitude { get; init; } = 1f;

    /// <summary>When false, render-time animation for emulated entity preview rigs is disabled.</summary>
    public bool EnableEntityAnimation { get; init; } = true;

    /// <summary>When true, vertex-baked idle motion for emulated entities holds at the clock value from when this was enabled.</summary>
    public bool PauseEntityIdleAnimation { get; init; }

    /// <summary>
    /// Legacy whole-mesh bob/yaw/roll on the model matrix (pre–setupAnim IR). Off by default; use lifted IR / GPU bones instead.
    /// </summary>
    public bool EnableLegacyEntityWobble { get; init; }

    /// <summary>
    /// When true, skin entity preview on the CPU (<c>SkinAndBakeToPreviewLayout</c>) and upload a 12-float preview-space VBO
    /// instead of the GPU bind mesh + entity shader path. Debug / parity fallback only.
    /// </summary>
    public bool ForceEntityCpuSkinning { get; init; }

    /// <summary>Genesis Shadows Phase 2: master toggle for the directional shadow map pass.</summary>
    public bool EnableShadows { get; init; } = true;

    /// <summary>Genesis Shadows Phase 2: shadow map resolution per side (256-4096, square).</summary>
    public int ShadowMapResolution { get; init; } = 1024;

    /// <summary>Genesis Shadows Phase 2: minimum slope-scaled depth bias to combat acne.</summary>
    public float ShadowMinBias { get; init; } = 0.0008f;

    /// <summary>Genesis Shadows Phase 2: maximum slope-scaled depth bias (used at grazing N.L).</summary>
    public float ShadowMaxBias { get; init; } = 0.005f;

    /// <summary>
    /// PHASE3-CSM stub: persisted boolean so Phase 3 can light up cascades without restructuring.
    /// No runtime branch in Phase 2 (single shadow map).
    /// </summary>
    public bool EnableShadowCascades { get; init; }

    /// <summary>Genesis: froxel volume god rays toward the sun.</summary>
    public bool EnableGodRays { get; init; } = true;

    /// <summary>Genesis: froxel volume inject + integrate god-ray path.</summary>
    public bool EnableVolumeGodRays { get; init; } = true;

    /// <summary>Genesis: procedural volumetric cloud layer in the sky pass.</summary>
    public bool EnableVolumetricClouds { get; init; }

    /// <summary>Volumetric cost preset: 0 = low, 1 = medium, 2 = high.</summary>
    public int VolumetricQuality { get; init; } = 1;

    public float GodRayStrength { get; init; } = 0.45f;
    public float GodRayConeScale { get; init; } = 1f;

    public float CloudDensity { get; init; } = 0.35f;
    public float CloudVolumeSize { get; init; } = 48f;
    public float CloudLayerHeight { get; init; }
    public float CloudVolumeHeight { get; init; } = 24f;
    public int CloudQuality { get; init; } = 1;

    /// <summary>When true, log froxel inject/integrate timings that exceed the documented budget.</summary>
    public bool LogVolumetricTiming { get; init; }

    /// <summary>Debug overlay: sun projection frustum lines in the preview viewport.</summary>
    public bool ShowSunProjectionDebug { get; init; }
}
