using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.OpenGL;

[Flags]
internal enum GenesisShaderFeatureMask : byte
{
    None = 0,
    Pom = 1 << 0,
    PomShadow = 1 << 1,
    PomAo = 1 << 2,
    NormalMap = 1 << 3,
    SpecularMap = 1 << 4,
    Sss = 1 << 5,
    Ibl = 1 << 6,
    Shadow = 1 << 7,
    All = Pom | PomShadow | PomAo | NormalMap | SpecularMap | Sss | Ibl | Shadow,
}

internal static class GenesisShaderFeatureMaskBuilder
{
    /// <summary>
    /// Compile-time feature mask from global preview toggles. Entity/block/ground runtime gating stays on
    /// <c>uEnableParallax</c>, <c>uEnableNormalMap</c>, etc. so ground POM works while entity POM is off.
    /// </summary>
    public static GenesisShaderFeatureMask Build(
        in PreviewRenderSettingsSnapshot settings,
        bool entityEmulatedPreview)
    {
        _ = entityEmulatedPreview;
        var mask = GenesisShaderFeatureMask.None;
        if (settings.EnableParallax)
        {
            mask |= GenesisShaderFeatureMask.Pom;
        }

        if (settings.EnableParallaxShadow)
        {
            mask |= GenesisShaderFeatureMask.PomShadow;
        }

        if (settings.EnableParallaxAo)
        {
            mask |= GenesisShaderFeatureMask.PomAo;
        }

        if (settings.EnableNormalMap)
        {
            mask |= GenesisShaderFeatureMask.NormalMap;
        }

        if (settings.EnableSpecularMap)
        {
            mask |= GenesisShaderFeatureMask.SpecularMap;
        }

        if (settings.EnableSss)
        {
            mask |= GenesisShaderFeatureMask.Sss;
        }

        if (settings.EnableIbl)
        {
            mask |= GenesisShaderFeatureMask.Ibl;
        }

        if (settings.EnableShadows)
        {
            mask |= GenesisShaderFeatureMask.Shadow;
        }

        return mask;
    }

    public static IReadOnlyDictionary<string, int> ToDefines(GenesisShaderFeatureMask mask)
    {
        var defines = new Dictionary<string, int>(8);
        if (mask.HasFlag(GenesisShaderFeatureMask.Pom))
        {
            defines["GENESIS_ENABLE_POM"] = 1;
        }

        if (mask.HasFlag(GenesisShaderFeatureMask.PomShadow))
        {
            defines["GENESIS_ENABLE_POM_SHADOW"] = 1;
        }

        if (mask.HasFlag(GenesisShaderFeatureMask.PomAo))
        {
            defines["GENESIS_ENABLE_POM_AO"] = 1;
        }

        if (mask.HasFlag(GenesisShaderFeatureMask.NormalMap))
        {
            defines["GENESIS_ENABLE_NORMAL_MAP"] = 1;
        }

        if (mask.HasFlag(GenesisShaderFeatureMask.SpecularMap))
        {
            defines["GENESIS_ENABLE_SPECULAR_MAP"] = 1;
        }

        if (mask.HasFlag(GenesisShaderFeatureMask.Sss))
        {
            defines["GENESIS_ENABLE_SSS"] = 1;
        }

        if (mask.HasFlag(GenesisShaderFeatureMask.Ibl))
        {
            defines["GENESIS_ENABLE_IBL"] = 1;
        }

        if (mask.HasFlag(GenesisShaderFeatureMask.Shadow))
        {
            defines["GENESIS_ENABLE_SHADOW"] = 1;
        }

        return defines;
    }
}
