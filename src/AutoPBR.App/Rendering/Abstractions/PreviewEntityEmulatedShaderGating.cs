namespace AutoPBR.App.Rendering.Abstractions;

/// <summary>
/// Derives effective Genesis toggles for clean-room <c>entity_emulated</c> previews (LabPBR maps are generated in Core;
/// the GPU path may still gate features for tangent/POM stability).
/// </summary>
public static class PreviewEntityEmulatedShaderGating
{
    public static bool EffectiveNormalMap(
        bool enableNormalMap,
        bool entityEmulated,
        bool enableEntityLabPbrShading) =>
        enableNormalMap && (!entityEmulated || enableEntityLabPbrShading);

    public static bool EffectiveSpecularMap(
        bool enableSpecularMap,
        bool entityEmulated,
        bool enableEntityLabPbrShading) =>
        enableSpecularMap && (!entityEmulated || enableEntityLabPbrShading);

    public static bool EffectiveParallax(bool enableParallax, bool entityEmulated, bool enableEntityParallax) =>
        enableParallax && (!entityEmulated || enableEntityParallax);

    public static bool EffectiveParallaxAo(bool enableParallaxAo, bool entityEmulated, bool enableEntityParallax) =>
        enableParallaxAo && (!entityEmulated || enableEntityParallax);

    public static bool EffectiveParallaxShadow(bool enableParallaxShadow, bool entityEmulated, bool enableEntityParallax) =>
        enableParallaxShadow && (!entityEmulated || enableEntityParallax);

    /// <summary>
    /// Height-driven tessellation on emulated entity sheets amplifies 8-bit height quantization into visible stripes;
    /// keep off unless explicitly enabled later (mirrors entity parallax policy).
    /// </summary>
    public static bool EffectiveTessellationDisplacement(bool enableTessellation, bool entityEmulated) =>
        enableTessellation && !entityEmulated;
}
