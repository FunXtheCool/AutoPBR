namespace AutoPBR.Preview;

/// <summary>
/// Slime / magma cube preview sizes (vanilla <c>SlimeRenderState.size</c> from <c>Slime.getSize()</c>).
/// Renderer scale is applied in <see cref="SlimeFamilyPreviewScale"/>, not model setupAnim.
/// </summary>
public static class EntityPreviewSizeCatalog
{
    public const string SizePrefix = "slime.size.";

    public const int MinSize = 1;
    public const int MaxSize = 8;

    private static readonly EntityPreviewSizeOption[] SizeOptions =
    [
        new($"{SizePrefix}1", "Tiny (1)", IsDefault: false),
        new($"{SizePrefix}2", "Small (2)", IsDefault: false),
        new($"{SizePrefix}3", "Size 3", IsDefault: false),
        new($"{SizePrefix}4", "Medium (4)", IsDefault: false),
        new($"{SizePrefix}5", "Size 5", IsDefault: false),
        new($"{SizePrefix}6", "Size 6", IsDefault: false),
        new($"{SizePrefix}7", "Size 7", IsDefault: false),
        new($"{SizePrefix}8", "Large (8)", IsDefault: false),
    ];

    public static bool IsSlimeFamilyBuilderMethod(string? builderMethod) =>
        string.Equals(builderMethod, "Slime", StringComparison.Ordinal) ||
        string.Equals(builderMethod, "MagmaCube", StringComparison.Ordinal);

    public static bool IsSlimeFamilyModelJvm(string modelJvm) =>
        modelJvm.Contains(".model.monster.slime.", StringComparison.Ordinal) &&
        (modelJvm.EndsWith("SlimeModel", StringComparison.Ordinal) ||
         modelJvm.EndsWith("MagmaCubeModel", StringComparison.Ordinal));

    public static bool TryGetSizeOptions(
        string normalizedAssetPath,
        string? builderMethod,
        out IReadOnlyList<EntityPreviewSizeOption> options)
    {
        options = [];
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/", StringComparison.OrdinalIgnoreCase) ||
            !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsSlimeFamilyBuilderMethod(builderMethod))
        {
            return false;
        }

        var defaultSize = ResolveDefaultSize(builderMethod);
        options = SizeOptions
            .Select(o => o with { IsDefault = ParseSizeFromId(o.Id) == defaultSize })
            .ToArray();
        return true;
    }

    public static int ResolveDefaultSize(string? builderMethod) =>
        string.Equals(builderMethod, "MagmaCube", StringComparison.Ordinal) ? 4 : 2;

    public static int ResolveEffectiveSize(string? selectedSizeId, string? builderMethod = null)
    {
        if (TryParseSizeFromId(selectedSizeId, out var parsed))
        {
            return parsed;
        }

        return ResolveDefaultSize(builderMethod);
    }

    public static bool TryParseSizeFromId(string? sizeId, out int size)
    {
        size = 0;
        if (string.IsNullOrWhiteSpace(sizeId) ||
            !sizeId.StartsWith(SizePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(sizeId.AsSpan(SizePrefix.Length), out size) &&
               size is >= MinSize and <= MaxSize;
    }

    private static int ParseSizeFromId(string sizeId) =>
        TryParseSizeFromId(sizeId, out var size) ? size : ResolveDefaultSize(null);
}
