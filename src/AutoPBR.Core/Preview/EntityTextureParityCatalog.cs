using System.Collections.Frozen;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Loads <c>minecraft_26.1.2_entity_textures.json</c> plus the companion model manifest under <c>Data/minecraft-native/</c>.
/// Catalogued vanilla diffuse paths must never take family fallbacks (see <see cref="EntityModelRuntime"/>).
/// </summary>
public static class EntityTextureParityCatalog
{
    private const string InventoryFileName = "minecraft_26.1.2_entity_textures.json";
    private const string ManifestFileName = "minecraft_26.1.2_entity_texture_model_manifest.json";

    private static readonly object Gate = new();
    private static FrozenSet<string>? _cataloguedPngKeys;
    private static FrozenDictionary<string, EntityTextureParityRule>? _rulesByPathKey;

    /// <summary>
    /// Inventory diffuse paths that also resolve a manifest rule (preview builders must succeed for these in CI).
    /// </summary>
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
            if (ResolveRule(key, string.Empty) is null)
            {
                continue;
            }

            list.Add(key);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    public static bool IsCatalogued(string normalizedEntityTexturePath)
    {
        EnsureLoaded();
        var key = ToPngKey(normalizedEntityTexturePath);
        return key is not null && _cataloguedPngKeys!.Contains(key);
    }

    /// <summary>Resolves the manifest rule for a catalogued diffuse path (longest <c>path_prefix</c> wins).</summary>
    public static EntityTextureParityRule? ResolveRule(string normalizedEntityTexturePath, string fileNameWithoutExtensionStem)
    {
        _ = fileNameWithoutExtensionStem;
        EnsureLoaded();
        var pathKey = ToPathPrefixKey(normalizedEntityTexturePath);
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
                _rulesByPathKey = FrozenDictionary<string, EntityTextureParityRule>.Empty;
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
            var rules = new Dictionary<string, EntityTextureParityRule>(StringComparer.OrdinalIgnoreCase);
            if (manDoc.RootElement.TryGetProperty("rules", out var rulesEl))
            {
                foreach (var e in rulesEl.EnumerateArray())
                {
                    var prefix = e.GetProperty("path_prefix").GetString()?.Trim();
                    var builder = e.GetProperty("builder_method").GetString()?.Trim();
                    if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(builder))
                    {
                        continue;
                    }

                    var deobf = e.TryGetProperty("deobf_model_class", out var d) ? d.GetString()?.Trim() : null;
                    var deobfPre = e.TryGetProperty("deobf_model_class_pre_restructure", out var dp)
                        ? dp.GetString()?.Trim()
                        : null;
                    var notes = e.TryGetProperty("notes", out var n) ? n.GetString()?.Trim() : null;
                    var geoJvm = e.TryGetProperty("geometry_ir_official_jvm", out var gj) ? gj.GetString()?.Trim() : null;
                    var geoJvmBaby = e.TryGetProperty("geometry_ir_official_jvm_baby", out var gjb) ? gjb.GetString()?.Trim() : null;
                    int? tw = null;
                    int? th = null;
                    if (e.TryGetProperty("geometry_ir_texture_width", out var gtw) &&
                        gtw.ValueKind == JsonValueKind.Number &&
                        gtw.TryGetInt32(out var gw) &&
                        gw > 0)
                    {
                        tw = gw;
                    }

                    if (e.TryGetProperty("geometry_ir_texture_height", out var gth) &&
                        gth.ValueKind == JsonValueKind.Number &&
                        gth.TryGetInt32(out var gh) &&
                        gh > 0)
                    {
                        th = gh;
                    }

                    var rule = new EntityTextureParityRule
                    {
                        PathPrefix = prefix,
                        BuilderMethod = builder,
                        DeobfuscatedModelClass = deobf,
                        DeobfuscatedModelClassPreRestructure = deobfPre,
                        Notes = notes,
                        GeometryIrOfficialJvm = geoJvm,
                        GeometryIrOfficialJvmBaby = geoJvmBaby,
                        GeometryIrTextureWidth = tw,
                        GeometryIrTextureHeight = th,
                    };
                    var ruleKey = prefix.Trim().Replace('\\', '/').ToLowerInvariant();
                    rules[ruleKey] = rule;
                }
            }

            var frozenKeys = png.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
            var frozenRules = rules.ToFrozenDictionary();
            _cataloguedPngKeys = frozenKeys;
            _rulesByPathKey = frozenRules;
        }
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
