using AutoPBR.Core;
using AutoPBR.Core.Models;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AutoPBR.App.Models;

/// <summary>Small Minecraft-style texture per built-in tag id for Explore, with emoji fallback for unknown ids.</summary>
public static class MaterialTagGlyphs
{
    private static readonly object BitmapLock = new();
    private static readonly Dictionary<string, Bitmap?> BitmapCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>16×16 block-style texture for known tag ids; null falls back to <see cref="ForTagId"/>.</summary>
    public static Bitmap? BitmapForTag(string id, TagRuleKind kind = TagRuleKind.Material)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var key = $"{(int)kind}:{id}";
        lock (BitmapLock)
        {
            if (BitmapCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var file = ResolveTextureFile(id, kind);
        if (file is null)
        {
            lock (BitmapLock)
            {
                BitmapCache[key] = null;
            }

            return null;
        }

        try
        {
            var uri = new Uri($"avares://AutoPBR.App/Assets/MaterialTags/{file}");
            if (!AssetLoader.Exists(uri))
            {
                lock (BitmapLock)
                {
                    BitmapCache[key] = null;
                }

                return null;
            }

            using var stream = AssetLoader.Open(uri);
            var bmp = new Bitmap(stream);
            lock (BitmapLock)
            {
                BitmapCache[key] = bmp;
            }

            return bmp;
        }
        catch
        {
            lock (BitmapLock)
            {
                BitmapCache[key] = null;
            }

            return null;
        }
    }

    private static string? ResolveTextureFile(string id, TagRuleKind kind)
    {
        var n = id.Trim().ToLowerInvariant();
        if (kind == TagRuleKind.Flag)
        {
            return n switch
            {
                FlagTagResolver.BlockId => "flag_block.png",
                FlagTagResolver.ItemId => "flag_item.png",
                FlagTagResolver.EntityId => "flag_entity.png",
                FlagTagResolver.ArmorId => "flag_armor.png",
                FlagTagResolver.UvWrapId => "flag_uv_wrap.png",
                FlagTagResolver.OreId => "flag_ore.png",
                FlagTagResolver.WeightedId => "flag_weighted.png",
                FlagTagResolver.UnweightedId => "flag_unweighted.png",
                FlagTagResolver.Sprite2DId => "flag_sprite_2d.png",
                _ => null
            };
        }

        return n switch
        {
            "brick" => "material_brick.png",
            "wood" => "material_wood.png",
            "metal" => "material_metal.png",
            "gem" => "material_gem.png",
            "organic" => "material_plant.png",
            "plant" => "material_plant.png",
            "glass" => "material_glass.png",
            "stone" => "material_stone.png",
            "unknown" => "material_unknown.png",
            _ => null
        };
    }

    public static string ForTagId(string id, TagRuleKind kind = TagRuleKind.Material)
    {
        if (string.IsNullOrEmpty(id))
        {
            return "\u25CF";
        }

        if (kind == TagRuleKind.Flag)
        {
            return id.Trim().ToLowerInvariant() switch
            {
                FlagTagResolver.BlockId => "\U0001F7E6",
                FlagTagResolver.ItemId => "\U0001F4E6",
                FlagTagResolver.EntityId => "\U0001F47E",
                FlagTagResolver.ArmorId => "\U0001F6E1",
                FlagTagResolver.UvWrapId => "\U0001F4E2",
                FlagTagResolver.OreId => "\u26CF",
                FlagTagResolver.WeightedId => "\u2696",
                FlagTagResolver.UnweightedId => "\u25EF",
                FlagTagResolver.Sprite2DId => "\U0001F5BC",
                _ => "\u2691"
            };
        }

        return id.Trim().ToLowerInvariant() switch
        {
            "brick" => "\U0001F9F1",
            "wood" => "\U0001FAB5",
            "metal" => "\u2699",
            "gem" => "\U0001F48E",
            "organic" => "\U0001F343",
            "plant" => "\U0001F343",
            "glass" => "\U0001F9CA",
            "stone" => "\U0001FAA8",
            "unknown" => "\u2753",
            _ => "\u25CF"
        };
    }
}
