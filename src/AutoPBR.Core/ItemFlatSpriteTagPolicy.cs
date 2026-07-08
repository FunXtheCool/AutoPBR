namespace AutoPBR.Core;

internal static class ItemFlatSpriteTagPolicy
{
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
}
