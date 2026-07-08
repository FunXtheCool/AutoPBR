using AutoPBR.Core;
using AutoPBR.Core.Models;

namespace AutoPBR.Preview;

/// <summary>
/// Path-derived rules for flat sprite vs model preview routing.
/// </summary>
public static class PreviewPathPolicy
{
    /// <summary>
    /// True when the texture lives under <c>textures/item/</c> (pack archive path or Explore storage key).
    /// </summary>
    public static bool IsItemTexturePath(string ruleRelativeKeyOrArchivePath)
    {
        if (string.IsNullOrWhiteSpace(ruleRelativeKeyOrArchivePath))
        {
            return false;
        }

        var norm = ruleRelativeKeyOrArchivePath.Replace('\\', '/').TrimStart('/');
        if (norm.Contains("/textures/item/", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("/textures/items/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var segments = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("textures", StringComparison.OrdinalIgnoreCase) &&
                (segments[i + 1].Equals("item", StringComparison.OrdinalIgnoreCase) ||
                 segments[i + 1].Equals("items", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Explore / scanner storage key: \{namespace}\item\{stem} (relative to textures root, no "textures" segment).
        if (segments.Length >= 2 &&
            segments[1].Equals("item", StringComparison.OrdinalIgnoreCase) &&
            !segments[0].Equals("assets", StringComparison.OrdinalIgnoreCase) &&
            !segments[0].Equals("textures", StringComparison.OrdinalIgnoreCase) &&
            !segments[0].Equals("optifine", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Item textures are flat inventory-style sprites unless path flags place them in an exemption bucket.
    /// </summary>
    public static bool IsItemFlatSpriteExempt(IEnumerable<string> effectiveTagIds)
    {
        foreach (var id in effectiveTagIds)
        {
            if (id.Equals(FlagTagResolver.BlockId, StringComparison.OrdinalIgnoreCase) ||
                id.Equals(FlagTagResolver.EntityId, StringComparison.OrdinalIgnoreCase) ||
                id.Equals(FlagTagResolver.ArmorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// When true, 3D preview should use a single flat item plane instead of baked Java model geometry.
    /// </summary>
    public static bool ShouldUseFlatItemPlane(string archiveOrRulePath, bool sprite2DFoliageTarget) =>
        sprite2DFoliageTarget && IsItemTexturePath(archiveOrRulePath);
}
