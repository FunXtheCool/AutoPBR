using System.Collections.Frozen;
using System.Text.Json;

namespace AutoPBR.Preview.Parity;

/// <summary>
/// Maps Mojang <c>net.minecraft.client.animation.definitions.*Animation</c> bytecode-lifted shards
/// (see <c>docs/generated/animation/&lt;ver&gt;/</c>) to parity catalog <see cref="EntityTextureParityRule.BuilderMethod"/> ids
/// consumed by <see cref="EntityModelRuntime"/>.
/// </summary>
public static class EntityParityAnimationMap
{
    private const string MapFileName = "minecraft_26.1.2_entity_parity_animation_map.json";

    private static readonly object Gate = new();
    private static EntityParityAnimationMapRoot? _root;
    private static FrozenDictionary<string, EntityParityAnimationBinding>? _byAnimation;
    private static FrozenDictionary<string, FrozenSet<EntityParityAnimationBinding>>? _byBuilder;

    /// <summary>Pin label for the companion <c>animation-index-26.1.2.json</c> row set.</summary>
    public static string VersionLabel => EnsureLoaded().VersionLabel;

    /// <summary>Relative to <c>docs/generated/</c> (same layout as geometry shards).</summary>
    public static string AnimationShardRootRelPath => EnsureLoaded().AnimationShardRootRelPath;

    public static IReadOnlyList<EntityParityAnimationBinding> GetBindings()
    {
        EnsureLoaded();
        return _root!.Bindings;
    }

    /// <summary>Bindings whose <see cref="EntityParityAnimationBinding.ParityBuilderMethods"/> includes <paramref name="parityBuilderMethod"/>.</summary>
    public static IReadOnlyList<EntityParityAnimationBinding> GetBindingsForParityBuilder(string parityBuilderMethod)
    {
        EnsureLoaded();
        if (_byBuilder!.TryGetValue(parityBuilderMethod, out var set))
        {
            return set.ToArray();
        }

        return [];
    }

    public static bool TryGetBindingForAnimation(string animationOfficialJvmName, out EntityParityAnimationBinding? binding)
    {
        EnsureLoaded();
        return _byAnimation!.TryGetValue(animationOfficialJvmName, out binding);
    }

    private static EntityParityAnimationMapRoot EnsureLoaded()
    {
        if (_root is not null && _byAnimation is not null && _byBuilder is not null)
        {
            return _root;
        }

        lock (Gate)
        {
            if (_root is not null && _byAnimation is not null && _byBuilder is not null)
            {
                return _root;
            }

            var nativeDir = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
            var path = Path.Combine(nativeDir, MapFileName);
            if (!File.Exists(path))
            {
                _byAnimation = FrozenDictionary<string, EntityParityAnimationBinding>.Empty;
                _byBuilder = FrozenDictionary<string, FrozenSet<EntityParityAnimationBinding>>.Empty;
                _root = new EntityParityAnimationMapRoot(
                    1,
                    "26.1.2",
                    "animation-index-26.1.2.json",
                    "animation/26.1.2",
                    []);
                return _root;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            var ver = root.GetProperty("versionLabel").GetString() ?? "26.1.2";
            var indexName = root.TryGetProperty("animationIndexFileName", out var inEl)
                ? inEl.GetString() ?? "animation-index-26.1.2.json"
                : "animation-index-26.1.2.json";
            var shardRoot = root.TryGetProperty("animationShardRootRelPath", out var srEl)
                ? srEl.GetString() ?? "animation/26.1.2"
                : "animation/26.1.2";
            var list = new List<EntityParityAnimationBinding>();
            foreach (var b in root.GetProperty("bindings").EnumerateArray())
            {
                var anim = b.GetProperty("animationOfficialJvmName").GetString() ?? "";
                var builders = new List<string>();
                foreach (var be in b.GetProperty("parityBuilderMethods").EnumerateArray())
                {
                    var s = be.GetString();
                    if (!string.IsNullOrEmpty(s))
                    {
                        builders.Add(s);
                    }
                }

                bool? restrict = null;
                if (b.TryGetProperty("restrictToBabyTextures", out var r) && r.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    restrict = r.GetBoolean();
                }

                var notes = b.TryGetProperty("notes", out var n) ? n.GetString() : null;
                list.Add(new EntityParityAnimationBinding(anim, builders, restrict, notes));
            }

            var byAnim = new Dictionary<string, EntityParityAnimationBinding>(StringComparer.Ordinal);
            var byBuilder = new Dictionary<string, HashSet<EntityParityAnimationBinding>>(StringComparer.Ordinal);
            foreach (var binding in list)
            {
                byAnim[binding.AnimationOfficialJvmName] = binding;
                foreach (var bm in binding.ParityBuilderMethods)
                {
                    if (!byBuilder.TryGetValue(bm, out var hs))
                    {
                        hs = new HashSet<EntityParityAnimationBinding>();
                        byBuilder[bm] = hs;
                    }

                    hs.Add(binding);
                }
            }

            _byAnimation = byAnim.ToFrozenDictionary(StringComparer.Ordinal);
            _byBuilder = byBuilder.ToFrozenDictionary(
                static kv => kv.Key,
                static kv => kv.Value.ToFrozenSet(),
                StringComparer.Ordinal);
            _root = new EntityParityAnimationMapRoot(
                root.TryGetProperty("schemaVersion", out var sv) && sv.TryGetInt32(out var v) ? v : 1,
                ver,
                indexName,
                shardRoot,
                list);
            return _root;
        }
    }
}

/// <summary>Root object stored in <c>minecraft_26.1.2_entity_parity_animation_map.json</c>.</summary>
public sealed record EntityParityAnimationMapRoot(
    int SchemaVersion,
    string VersionLabel,
    string AnimationIndexFileName,
    string AnimationShardRootRelPath,
    IReadOnlyList<EntityParityAnimationBinding> Bindings);

/// <summary>One animation definition holder class mapped to one or more parity builders.</summary>
public sealed record EntityParityAnimationBinding(
    string AnimationOfficialJvmName,
    IReadOnlyList<string> ParityBuilderMethods,
    bool? RestrictToBabyTextures,
    string? Notes);
