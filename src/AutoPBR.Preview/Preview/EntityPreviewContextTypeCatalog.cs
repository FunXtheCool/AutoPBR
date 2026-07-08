namespace AutoPBR.Preview;

/// <summary>
/// Hanging-sign attachment variants from <c>HangingSignRenderer.createHangingSignLayer</c>
/// (<c>HangingSignBlock.Attachment</c>: WALL, CEILING, CEILING_MIDDLE in 26.1.2 client.jar).
/// </summary>
public static class EntityPreviewContextTypeCatalog
{
    public const string Prefix = "hanging_sign.attachment.";

    public const string Wall = Prefix + "wall";
    public const string Ceiling = Prefix + "ceiling";
    public const string CeilingMiddle = Prefix + "ceiling_middle";

    public const string HangingSignHandLiftJvm = "net.minecraft.client.model.HangingSignModel";

    public enum HangingSignAttachment
    {
        Wall,
        Ceiling,
        CeilingMiddle,
    }

    private static readonly EntityPreviewContextTypeOption[] HangingSignOptions =
    [
        new(Wall, "Wall", IsDefault: false),
        new(Ceiling, "Ceiling", IsDefault: true),
        new(CeilingMiddle, "Ceiling middle", IsDefault: false),
    ];

    public static bool IsHangingSignBuilderMethod(string? builderMethod) =>
        string.Equals(builderMethod, "HangingSignEntity", StringComparison.OrdinalIgnoreCase);

    public static bool TryGetContextTypeOptions(
        string normalizedAssetPath,
        string? builderMethod,
        out IReadOnlyList<EntityPreviewContextTypeOption> options)
    {
        options = [];
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (!norm.Contains("/textures/entity/signs/hanging/", StringComparison.OrdinalIgnoreCase) ||
            !norm.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsHangingSignBuilderMethod(builderMethod))
        {
            return false;
        }

        options = HangingSignOptions;
        return true;
    }

    public static HangingSignAttachment ResolveEffectiveAttachment(string? contextTypeId) =>
        contextTypeId switch
        {
            Wall => HangingSignAttachment.Wall,
            CeilingMiddle => HangingSignAttachment.CeilingMiddle,
            _ => HangingSignAttachment.Ceiling,
        };

    public static string ResolveHandLiftJvm(string? contextTypeId) =>
        ResolveHandLiftJvm(ResolveEffectiveAttachment(contextTypeId));

    public static string ResolveHandLiftJvm(HangingSignAttachment attachment) =>
        attachment switch
        {
            HangingSignAttachment.Wall => $"{HangingSignHandLiftJvm}#attachment.WALL",
            HangingSignAttachment.CeilingMiddle => $"{HangingSignHandLiftJvm}#attachment.CEILING_MIDDLE",
            _ => HangingSignHandLiftJvm,
        };

    /// <summary>
    /// When the explore context selector picks a non-default attachment, prefer that hand-lift shard before the base JVM.
    /// </summary>
    public static bool TryGetHandLiftJvmOverride(string handLiftJvm, out string contextJvm)
    {
        contextJvm = "";
        if (!string.Equals(handLiftJvm, HangingSignHandLiftJvm, StringComparison.Ordinal))
        {
            return false;
        }

        var attachment = ResolveEffectiveAttachment(EntityPreviewBuildContext.CurrentContextTypeId);
        if (attachment == HangingSignAttachment.Ceiling)
        {
            return false;
        }

        contextJvm = ResolveHandLiftJvm(attachment);
        return true;
    }
}
