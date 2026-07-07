using System.Text.Json;

using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// One pass over <c>minecraft_26.1.2_entity_textures.json</c> PNG entries: catalog + manifest + mesh build + route classification.
/// </summary>
public sealed class EntityTextureParityJsonCatalogTests
{
    private static readonly Lazy<string[]> PngPaths = new(ReadInventoryPngPaths, isThreadSafe: true);

    private static readonly MinecraftNativeProfile Profile2612 = new("26.1.2", "unused", new Version(26, 1, 2));

    public static IEnumerable<object[]> GetCataloguedPngPaths() => PngPaths.Value.Select(p => new object[] { p });

    private static string[] ReadInventoryPngPaths()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "minecraft_26.1.2_entity_textures.json");
        if (!File.Exists(path))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("files", out var files))
        {
            return [];
        }

        var list = new List<string>();
        foreach (var e in files.EnumerateArray())
        {
            if (!e.TryGetProperty("path", out var p))
            {
                continue;
            }

            var s = p.GetString();
            if (string.IsNullOrEmpty(s) || !s.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add(s.Replace('\\', '/'));
        }

        return list.ToArray();
    }

    [Theory]
    [MemberData(nameof(GetCataloguedPngPaths))]
    public void CataloguedPngParityMeshAndRoute(string texturePath)
    {
        if (PngPaths.Value.Length == 0)
        {
            return;
        }

        var norm = texturePath.TrimStart('/');
        Assert.True(EntityTextureParityCatalog.IsCatalogued(norm));
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(norm, stem);
        Assert.NotNull(rule);

        var route = EntityModelRuntime.ClassifyEntityTextureRoute(
            norm,
            Profile2612,
            idlePhase01: 0.33f,
            animationTimeSeconds: 0.41f);
        Assert.Equal(EntityPreviewRouteKind.ParityCatalogGeometryIr, route);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(norm, Profile2612, idlePhase01: 0.33f, animationTimeSeconds: 0.41f, out var merged));
        Assert.NotNull(merged);
        Assert.True(merged.Elements.Count > 0);
    }

    [Fact]
    public void ManifestEveryRuleCarriesPostAndPreRestructureDeobfHints()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "minecraft_26.1.2_entity_texture_model_manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!doc.RootElement.TryGetProperty("rules", out var rules))
        {
            return;
        }

        var i = 0;
        foreach (var e in rules.EnumerateArray())
        {
            i++;
            var prefix = e.GetProperty("path_prefix").GetString();
            Assert.False(string.IsNullOrWhiteSpace(prefix), $"rule #{i}: path_prefix");

            var post = e.TryGetProperty("deobf_model_class", out var d) ? d.GetString() : null;
            Assert.False(string.IsNullOrWhiteSpace(post), $"rule #{i} ({prefix}): deobf_model_class");

            var pre = e.TryGetProperty("deobf_model_class_pre_restructure", out var p) ? p.GetString() : null;
            Assert.False(string.IsNullOrWhiteSpace(pre), $"rule #{i} ({prefix}): deobf_model_class_pre_restructure");
        }
    }

    [Fact]
    public void ManifestEveryBuilderMethodIsImplementedInParityDispatch()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "minecraft_26.1.2_entity_texture_model_manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!doc.RootElement.TryGetProperty("rules", out var rules))
        {
            return;
        }

        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in rules.EnumerateArray())
        {
            var prefix = e.GetProperty("path_prefix").GetString();
            var builder = e.GetProperty("builder_method").GetString();
            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(builder))
            {
                continue;
            }

            if (!seen.ContainsKey(builder))
            {
                seen[builder] = prefix + ".png";
            }
        }

        var runtime = EntityModelRuntimeFactory.Create();
        foreach (var (builder, samplePath) in seen)
        {
            Assert.True(
                runtime.TryBuildStaticMesh(samplePath, Profile2612, 0.31f, 0.42f, out var merged),
                $"Dispatch missing or failed for builder {builder} (sample {samplePath})");
            Assert.True(merged.Elements.Count > 0, builder);
        }
    }

    [Fact]
    public void ManifestModelClassesResolveAgainstGenerated2612ModelIndex()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "minecraft_26.1.2_entity_texture_model_manifest.json");
        var indexPath = Path.Combine(AppContext.BaseDirectory, "docs", "generated", "minecraft-client-model-index-26.1.2.json");
        if (!File.Exists(manifestPath) || !File.Exists(indexPath))
        {
            return;
        }

        using var indexDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var knownModels = new HashSet<string>(StringComparer.Ordinal);
        if (indexDoc.RootElement.TryGetProperty("classes", out var classes))
        {
            foreach (var c in classes.EnumerateArray())
            {
                var modelClass = c.TryGetProperty("officialJvmName", out var n) ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(modelClass))
                {
                    knownModels.Add(modelClass!);
                }
            }
        }

        using var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!manifestDoc.RootElement.TryGetProperty("rules", out var rules))
        {
            return;
        }

        // Renderer-driven meshes are intentionally mapped to renderer classes and are outside the model/animation index prefixes.
        var allowedRendererClasses = new HashSet<string>(StringComparer.Ordinal)
        {
            "net.minecraft.client.renderer.blockentity.BeaconRenderer",
            "net.minecraft.client.renderer.blockentity.BedRenderer",
            "net.minecraft.client.renderer.blockentity.ConduitRenderer",
            "net.minecraft.client.renderer.entity.DragonFireballRenderer",
            "net.minecraft.client.renderer.blockentity.TheEndGatewayRenderer",
            "net.minecraft.client.renderer.blockentity.TheEndPortalRenderer",
            "net.minecraft.client.renderer.entity.ExperienceOrbRenderer",
            "net.minecraft.client.renderer.entity.FishingHookRenderer",
            "net.minecraft.client.renderer.blockentity.HangingSignRenderer",
            "net.minecraft.client.renderer.blockentity.StandingSignRenderer",
            "net.minecraft.client.renderer.entity.layers.EquipmentLayerRenderer",
            "net.minecraft.client.renderer.blockentity.DecoratedPotRenderer",
            "net.minecraft.client.renderer.entity.GuardianRenderer",
        };

        var i = 0;
        foreach (var e in rules.EnumerateArray())
        {
            i++;
            var prefix = e.GetProperty("path_prefix").GetString();
            var post = e.TryGetProperty("deobf_model_class", out var d) ? d.GetString() : null;
            Assert.False(string.IsNullOrWhiteSpace(post), $"rule #{i} ({prefix}): deobf_model_class");
            var cls = post!;
            Assert.True(
                knownModels.Contains(cls) || allowedRendererClasses.Contains(cls),
                $"rule #{i} ({prefix}) points to '{cls}', which is not in generated 26.1.2 model index and is not an allowed renderer-backed mesh class.");
        }
    }

    [Fact]
    public void ManifestAnimatedBuilderFamiliesResolveExpectedAnimationDefinitionsFromIndex()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "minecraft_26.1.2_entity_texture_model_manifest.json");
        var indexPath = Path.Combine(AppContext.BaseDirectory, "docs", "generated", "minecraft-client-model-index-26.1.2.json");
        if (!File.Exists(manifestPath) || !File.Exists(indexPath))
        {
            return;
        }

        using var indexDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var knownClasses = new HashSet<string>(StringComparer.Ordinal);
        if (indexDoc.RootElement.TryGetProperty("classes", out var classes))
        {
            foreach (var c in classes.EnumerateArray())
            {
                var cls = c.TryGetProperty("officialJvmName", out var n) ? n.GetString() : null;
                if (!string.IsNullOrWhiteSpace(cls))
                {
                    knownClasses.Add(cls!);
                }
            }
        }

        using var manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!manifestDoc.RootElement.TryGetProperty("rules", out var rules))
        {
            return;
        }

        var buildersInManifest = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in rules.EnumerateArray())
        {
            var b = e.TryGetProperty("builder_method", out var bm) ? bm.GetString() : null;
            if (!string.IsNullOrWhiteSpace(b))
            {
                buildersInManifest.Add(b!);
            }
        }

        var expectedByBuilder = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Armadillo"] = ["net.minecraft.client.animation.definitions.ArmadilloAnimation", "net.minecraft.client.animation.definitions.BabyArmadilloAnimation"],
            ["Bat"] = ["net.minecraft.client.animation.definitions.BatAnimation"],
            ["Breeze"] = ["net.minecraft.client.animation.definitions.BreezeAnimation"],
            ["Camel"] = ["net.minecraft.client.animation.definitions.CamelAnimation", "net.minecraft.client.animation.definitions.CamelBabyAnimation"],
            ["CopperGolem"] = ["net.minecraft.client.animation.definitions.CopperGolemAnimation"],
            ["Creaking"] = ["net.minecraft.client.animation.definitions.CreakingAnimation"],
            ["Frog"] = ["net.minecraft.client.animation.definitions.FrogAnimation"],
            ["NautilusMob"] = ["net.minecraft.client.animation.definitions.NautilusAnimation"],
            ["Rabbit"] = ["net.minecraft.client.animation.definitions.RabbitAnimation", "net.minecraft.client.animation.definitions.BabyRabbitAnimation"],
            ["Sniffer"] = ["net.minecraft.client.animation.definitions.SnifferAnimation"],
            ["Warden"] = ["net.minecraft.client.animation.definitions.WardenAnimation"],
        };

        var checkedBuilders = 0;
        foreach (var (builder, expectedClasses) in expectedByBuilder)
        {
            if (!buildersInManifest.Contains(builder))
            {
                continue;
            }

            checkedBuilders++;
            foreach (var expectedClass in expectedClasses)
            {
                Assert.True(
                    knownClasses.Contains(expectedClass),
                    $"Builder '{builder}' expects animation definition '{expectedClass}', but it was not found in generated 26.1.2 model index.");
            }
        }

        Assert.True(checkedBuilders > 0, "No animated builders from the expectation map were present in the manifest.");
    }

    [Fact]
    public void ModelIndex2612AnimationDefinitionClassesExposeJavapBytecodeSidecars()
    {
        var indexPath = Path.Combine(AppContext.BaseDirectory, "docs", "generated", "minecraft-client-model-index-26.1.2.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        using var indexDoc = JsonDocument.Parse(File.ReadAllText(indexPath));
        var root = indexDoc.RootElement;
        if (root.TryGetProperty("skipJavap", out var skip) && skip.ValueKind == JsonValueKind.True)
        {
            return;
        }

        Assert.True(
            root.TryGetProperty("animationBytecodeSidecarCount", out var cnt) && cnt.GetInt32() >= 16,
            "Expected animation definition javap -c sidecars (see Generate-MinecraftClientModelIndex.ps1).");

        JsonElement? nautilusRow = null;
        if (root.TryGetProperty("classes", out var classes))
        {
            foreach (var c in classes.EnumerateArray())
            {
                var name = c.TryGetProperty("officialJvmName", out var n) ? n.GetString() : null;
                if (string.Equals(name, "net.minecraft.client.animation.definitions.NautilusAnimation", StringComparison.Ordinal))
                {
                    nautilusRow = c;
                    break;
                }
            }
        }

        Assert.True(nautilusRow.HasValue, "NautilusAnimation row missing from model index.");
        var row = nautilusRow!.Value;
        Assert.True(row.TryGetProperty("javapBytecodeCRelPath", out var rel), "NautilusAnimation should set javapBytecodeCRelPath.");
        var sidecar = Path.Combine(Path.GetDirectoryName(indexPath)!, rel.GetString()!);
        Assert.True(File.Exists(sidecar), $"Sidecar missing: {sidecar}");
        var text = File.ReadAllText(sidecar);
        Assert.Contains("static", text, StringComparison.Ordinal);
        Assert.Contains("NautilusAnimation", text, StringComparison.Ordinal);
        Assert.Contains("scaleVec", text, StringComparison.Ordinal);
    }
}
