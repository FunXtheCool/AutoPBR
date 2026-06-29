using System.Collections.Frozen;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Loads <c>minecraft_26.1.2_block_textures.json</c> plus the companion block model manifest under <c>Data/minecraft-native/</c>.
/// </summary>
public static class BlockTextureParityCatalog
{
    private const string InventoryFileName = "minecraft_26.1.2_block_textures.json";
    private const string ManifestFileName = "minecraft_26.1.2_block_texture_model_manifest.json";

    private static readonly object Gate = new();
    private static FrozenSet<string>? _cataloguedPngKeys;
    private static FrozenDictionary<string, BlockTextureParityRule>? _rulesByPathKey;

    public static IReadOnlyList<string> GetCataloguedDiffusePathsWithManifestRules()
    {
        EnsureLoaded();
        if (_cataloguedPngKeys is null || _cataloguedPngKeys.Count == 0)
        {
            return [];
        }

        var list = new List<string>(_cataloguedPngKeys.Count);
        foreach (var key in _cataloguedPngKeys)
        {
            if (ResolveRule(key) is null)
            {
                continue;
            }

            list.Add(key);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public static IReadOnlyList<string> GetSynthesizableCubePreviewPaths()
    {
        var list = new List<string>();
        foreach (var path in GetCataloguedDiffusePathsWithManifestRules())
        {
            var rule = ResolveRule(path);
            if (rule is not null && rule.CanSynthesizeCubePreview())
            {
                list.Add(path);
            }
        }

        return list;
    }

    public static bool IsCatalogued(string normalizedBlockTexturePath)
    {
        EnsureLoaded();
        var key = ToPngKey(normalizedBlockTexturePath);
        return key is not null && _cataloguedPngKeys!.Contains(key);
    }

    public static BlockTextureParityRule? ResolveRule(string normalizedBlockTexturePath)
    {
        EnsureLoaded();
        var pathKey = ToPathPrefixKey(normalizedBlockTexturePath);
        if (pathKey is null)
        {
            return null;
        }

        return _rulesByPathKey!.GetValueOrDefault(pathKey);
    }

    private static void EnsureLoaded()
    {
        if (_cataloguedPngKeys is not null && _rulesByPathKey is not null)
        {
            return;
        }

        lock (Gate)
        {
            if (_cataloguedPngKeys is not null && _rulesByPathKey is not null)
            {
                return;
            }

            var nativeDir = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
            var invPath = Path.Combine(nativeDir, InventoryFileName);
            var manifestPath = Path.Combine(nativeDir, ManifestFileName);
            if (!File.Exists(invPath) || !File.Exists(manifestPath))
            {
                _cataloguedPngKeys = FrozenSet<string>.Empty;
                _rulesByPathKey = FrozenDictionary<string, BlockTextureParityRule>.Empty;
                return;
            }

            using var invDoc = JsonDocument.Parse(File.ReadAllText(invPath));
            var png = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (invDoc.RootElement.TryGetProperty("files", out var files))
            {
                foreach (var e in files.EnumerateArray())
                {
                    if (!e.TryGetProperty("path", out var p))
                    {
                        continue;
                    }

                    var path = p.GetString();
                    if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var k = ToPngKey(path);
                    if (k is not null)
                    {
                        png.Add(k);
                    }
                }
            }

            using var manDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var rules = new Dictionary<string, BlockTextureParityRule>(StringComparer.OrdinalIgnoreCase);
            if (manDoc.RootElement.TryGetProperty("rules", out var rulesEl))
            {
                foreach (var e in rulesEl.EnumerateArray())
                {
                    var prefix = e.GetProperty("path_prefix").GetString()?.Trim();
                    var familyId = e.TryGetProperty("family_id", out var f) ? f.GetString()?.Trim() : null;
                    var shapeRaw = e.TryGetProperty("preview_shape", out var s) ? s.GetString()?.Trim() : null;
                    if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(familyId) ||
                        string.IsNullOrEmpty(shapeRaw) ||
                        !TryParsePreviewShape(shapeRaw, out var shape))
                    {
                        continue;
                    }

                    Dictionary<string, string>? slots = null;
                    if (e.TryGetProperty("texture_slots", out var slotsEl) &&
                        slotsEl.ValueKind == JsonValueKind.Object)
                    {
                        slots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in slotsEl.EnumerateObject())
                        {
                            var stem = prop.Value.GetString()?.Trim();
                            if (string.IsNullOrEmpty(stem))
                            {
                                continue;
                            }

                            slots[prop.Name] = stem;
                        }
                    }

                    var rule = new BlockTextureParityRule
                    {
                        PathPrefix = prefix,
                        FamilyId = familyId,
                        PreviewShape = shape,
                        TextureSlots = slots,
                    };
                    var ruleKey = prefix.Trim().Replace('\\', '/').ToLowerInvariant();
                    rules[ruleKey] = rule;
                }
            }

            _cataloguedPngKeys = png.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            _rulesByPathKey = rules.ToFrozenDictionary();
        }
    }

    private static bool TryParsePreviewShape(string raw, out BlockTextureParityPreviewShape shape)
    {
        if (Enum.TryParse(raw, ignoreCase: true, out shape))
        {
            return true;
        }

        shape = default;
        return false;
    }

    private static string? ToPngKey(string path)
    {
        var n = path.Replace('\\', '/').TrimStart('/');
        if (!n.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return n.ToLowerInvariant();
    }

    private static string? ToPathPrefixKey(string path)
    {
        var k = ToPngKey(path);
        if (k is null)
        {
            return null;
        }

        return k[..^4];
    }
}
