using System.Collections.Frozen;
using System.Text.Json;

using JetBrains.Annotations;

namespace AutoPBR.Preview.GeometryIr;

/// <summary>
/// Loads <c>Data/minecraft-native/minecraft_26.1.2_geometry_ir_parity_policy.json</c> (motion tier per <c>builder_method</c>).
/// Parity catalog always attempts bytecode-lifted geometry IR first; this file only selects optional preview pose passes.
/// </summary>
public static class GeometryIrParityPolicy
{
    private const string PolicyFileName = "minecraft_26.1.2_geometry_ir_parity_policy.json";

    private static readonly object Gate = new();
    private static bool _loaded;
    private static GeometryIrParityTier _defaultTier = GeometryIrParityTier.PreferIr;
    private static FrozenDictionary<string, GeometryIrParityTier>? _byBuilderMethod;

    /// <summary>Default when a <c>builder_method</c> is missing from the policy map.</summary>
    public static GeometryIrParityTier DefaultTier
    {
        get
        {
            lock (Gate)
            {
                EnsureLoadedUnderLock();
                return _defaultTier;
            }
        }
    }

    /// <summary>Resolved tier for a parity manifest <c>builder_method</c>.</summary>
    public static GeometryIrParityTier GetTier(string builderMethod)
    {
        lock (Gate)
        {
            EnsureLoadedUnderLock();
            if (string.IsNullOrEmpty(builderMethod))
            {
                return _defaultTier;
            }

            return _byBuilderMethod!.GetValueOrDefault(builderMethod, _defaultTier);
        }
    }

    /// <summary>For unit tests: replace policy table (call <see cref="ResetForTests"/> to restore file-backed load).</summary>
    [UsedImplicitly(ImplicitUseKindFlags.Access)]
    internal static void LoadFromJsonForTests(string json)
    {
        lock (Gate)
        {
            ParsePolicyDocument(JsonDocument.Parse(json).RootElement);
            _loaded = true;
        }
    }

    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _loaded = false;
            _byBuilderMethod = null;
            _defaultTier = GeometryIrParityTier.PreferIr;
        }
    }

    private static void EnsureLoadedUnderLock()
    {
        if (_loaded)
        {
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", PolicyFileName);
        if (!File.Exists(path))
        {
            _byBuilderMethod = FrozenDictionary<string, GeometryIrParityTier>.Empty;
            _defaultTier = GeometryIrParityTier.PreferIr;
            _loaded = true;
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        ParsePolicyDocument(doc.RootElement);
        _loaded = true;
    }

    private static void ParsePolicyDocument(JsonElement root)
    {
        var def = GeometryIrParityTier.PreferIr;
        if (root.TryGetProperty("default_tier", out var defEl))
        {
            def = ParseTierString(defEl.GetString()) ?? GeometryIrParityTier.PreferIr;
        }

        _defaultTier = def;
        var map = new Dictionary<string, GeometryIrParityTier>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("by_builder_method", out var by) && by.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in by.EnumerateObject())
            {
                var tier = ParseTierString(p.Value.GetString());
                if (tier is { } t)
                {
                    map[p.Name.Trim()] = t;
                }
            }
        }

        _byBuilderMethod = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static GeometryIrParityTier? ParseTierString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim();
        if (s.Equals("prefer_ir", StringComparison.OrdinalIgnoreCase))
        {
            return GeometryIrParityTier.PreferIr;
        }

        if (s.Equals("ir_geometry_preview_anim", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("ir_geometry_hand_pose", StringComparison.OrdinalIgnoreCase))
        {
            return GeometryIrParityTier.IrGeometryPreviewAnim;
        }

        if (s.Equals("hand_only", StringComparison.OrdinalIgnoreCase))
        {
            return GeometryIrParityTier.PreferIr;
        }

        return Enum.TryParse<GeometryIrParityTier>(s, ignoreCase: true, out var e) ? e : null;
    }
}
