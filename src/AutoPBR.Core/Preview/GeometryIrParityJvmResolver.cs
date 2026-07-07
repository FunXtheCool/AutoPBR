using System.Text.Json;


namespace AutoPBR.Core.Preview;

/// <summary>
/// Maps entity texture paths to bytecode-lifted geometry IR shards (climate/baby variants share a
/// <see cref="EntityTextureParityRule.DeobfuscatedModelClass"/> in the manifest but have distinct
/// <c>*Model</c> mesh hosts in <c>client.jar</c>).
/// </summary>
internal static class GeometryIrParityJvmResolver
{
    public static bool TryResolveLiftedRoot(
        MinecraftNativeProfile? profile,
        EntityTextureParityRule rule,
        string normalizedAssetPath,
        string stem,
        bool isBaby,
        out string officialJvmName,
        out JsonElement geometryRoot)
    {
        geometryRoot = default;
        officialJvmName = "";

        foreach (var candidate in EnumerateCandidates(rule, normalizedAssetPath, stem, isBaby))
        {
            if (ParityCatalogHandLiftGeometryIrCatalog.TryGetOkRoot(candidate, out geometryRoot))
            {
            if (IsMisLiftedAdultZombieBabyMesh(isBaby, normalizedAssetPath, candidate, geometryRoot) ||
                IsMisLiftedAdultSnifferOnSniffletBaby(isBaby, normalizedAssetPath, candidate, geometryRoot))
            {
                continue;
            }

            officialJvmName = candidate;
            return true;
        }

        if (GeometryIrDocumentLoader.TryLoadLiftedForParityCatalog(profile, candidate, out geometryRoot) &&
            !IsMisLiftedAdultEquineHorseModelShard(candidate, geometryRoot) &&
            !IsAdultEquineJvmRejectedForBaby(isBaby, candidate) &&
            !IsMisLiftedAdultZombieBabyMesh(isBaby, normalizedAssetPath, candidate, geometryRoot) &&
            !IsMisLiftedAdultSnifferOnSniffletBaby(isBaby, normalizedAssetPath, candidate, geometryRoot))
            {
                officialJvmName = candidate;
                return true;
            }
        }

        return false;
    }

    internal static IEnumerable<string> EnumerateCandidates(
        EntityTextureParityRule rule,
        string normalizedAssetPath,
        string stem,
        bool isBaby)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var jvm in EnumerateCandidatesCore(rule, normalizedAssetPath, stem, isBaby))
        {
            foreach (var expanded in GeometryIrBabyDelegateJvmMap.EnumerateResolutionCandidates(jvm))
            {
                if (seen.Add(expanded))
                {
                    yield return expanded;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateCandidatesCore(
        EntityTextureParityRule rule,
        string normalizedAssetPath,
        string stem,
        bool isBaby)
    {
        foreach (var objectEntityJvm in GeometryIrParityObjectEntityJvmMap.EnumerateCandidates(
                     rule.BuilderMethod,
                     normalizedAssetPath))
        {
            yield return objectEntityJvm;
        }

        if (GeometryIrParityHandLiftJvmMap.TryGetHandLiftJvm(rule.BuilderMethod, normalizedAssetPath, out var handLiftJvm))
        {
            if (EntityPreviewContextTypeCatalog.TryGetHandLiftJvmOverride(handLiftJvm, out var contextJvm))
            {
                yield return contextJvm;
            }

            yield return handLiftJvm;
        }

        if (GeometryIrParityEquipmentJvmMap.TryResolveOfficialJvm(
                rule.BuilderMethod,
                normalizedAssetPath,
                isBaby,
                out var equipJvm,
                out var equipJvmBaby))
        {
            if (isBaby && !string.IsNullOrWhiteSpace(equipJvmBaby))
            {
                yield return equipJvmBaby;
            }

            if (!string.IsNullOrWhiteSpace(equipJvm))
            {
                yield return equipJvm;
            }
        }

        if (isBaby && PathImpliesOcelot(normalizedAssetPath, stem))
        {
            yield return "net.minecraft.client.model.animal.feline.BabyOcelotModel";
        }

        if (isBaby && PathImpliesSnifflet(normalizedAssetPath, stem))
        {
            yield return "net.minecraft.client.model.animal.sniffer.SniffletModel";
        }

        if (isBaby && !string.IsNullOrWhiteSpace(rule.GeometryIrOfficialJvmBaby))
        {
            yield return rule.GeometryIrOfficialJvmBaby;
        }

        if (!isBaby && !string.IsNullOrWhiteSpace(rule.GeometryIrOfficialJvm))
        {
            yield return rule.GeometryIrOfficialJvm;
        }

        if (TryParseModelStem(rule.DeobfuscatedModelClass, out var pkg, out var modelStem))
        {
            var normalizedStem = NormalizeModelStem(modelStem);
            if (isBaby)
            {
                if (IsEquineModelPackage(pkg) &&
                    string.Equals(normalizedStem, "Horse", StringComparison.Ordinal))
                {
                    yield return $"{pkg}.BabyHorseModel";
                }
                else if (IsEquineModelPackage(pkg) &&
                         string.Equals(normalizedStem, "Donkey", StringComparison.Ordinal))
                {
                    yield return $"{pkg}.BabyDonkeyModel";
                }
                else
                {
                    yield return $"{pkg}.Baby{normalizedStem}Model";
                }
            }
            else
            {
                if (PathImpliesClimate(normalizedAssetPath, stem, "cold"))
                {
                    yield return $"{pkg}.Cold{normalizedStem}Model";
                    yield return $"{pkg}.{normalizedStem}Model";
                }
                else if (PathImpliesClimate(normalizedAssetPath, stem, "warm"))
                {
                    yield return $"{pkg}.Warm{normalizedStem}Model";
                    yield return $"{pkg}.Adult{normalizedStem}Model";
                    yield return $"{pkg}.{normalizedStem}Model";
                }
                else
                {
                    if (IsEquineModelPackage(pkg) &&
                        string.Equals(normalizedStem, "Horse", StringComparison.Ordinal))
                    {
                        yield return $"{pkg}.AbstractEquineModel";
                    }
                    else
                    {
                        yield return $"{pkg}.Adult{normalizedStem}Model";
                        yield return $"{pkg}.{normalizedStem}Model";
                    }
                }
            }
        }

        if (!isBaby)
        {
            if (!string.IsNullOrWhiteSpace(rule.DeobfuscatedModelClassPreRestructure) &&
                !rule.DeobfuscatedModelClassPreRestructure.Contains("renderer", StringComparison.OrdinalIgnoreCase))
            {
                yield return rule.DeobfuscatedModelClassPreRestructure;
            }

            if (!string.IsNullOrWhiteSpace(rule.DeobfuscatedModelClass) &&
                !rule.DeobfuscatedModelClass.Contains("renderer", StringComparison.OrdinalIgnoreCase))
            {
                yield return rule.DeobfuscatedModelClass;
            }

            yield break;
        }

        // Baby textures must not fall back to adult mesh hosts (e.g. PolarBearModel) when a Baby* shard exists.
        foreach (var adultJvm in new[] { rule.DeobfuscatedModelClassPreRestructure, rule.DeobfuscatedModelClass })
        {
            if (string.IsNullOrWhiteSpace(adultJvm) ||
                adultJvm.Contains("renderer", StringComparison.OrdinalIgnoreCase) ||
                SimpleClassNameContainsBaby(adultJvm))
            {
                continue;
            }

            yield return adultJvm;
        }
    }

    internal static bool SimpleClassNameContainsBaby(string officialJvm)
    {
        var idx = officialJvm.LastIndexOf('.');
        var simple = idx >= 0 ? officialJvm[(idx + 1)..] : officialJvm;
        return simple.Contains("Baby", StringComparison.Ordinal);
    }

    /// <summary>
    /// Variant geometry-index rows that lift <c>createBabyBodyLayer</c> on a shared adult model host
    /// (e.g. <c>NautilusModel.createBabyBodyLayer</c>).
    /// </summary>
    internal static bool IsAlternateBabyBodyLayerFactoryShard(string officialJvm) =>
        officialJvm.EndsWith(".createBabyBodyLayer", StringComparison.Ordinal);

    private static string NormalizeModelStem(string modelStem)
    {
        if (modelStem.StartsWith("Adult", StringComparison.Ordinal) && modelStem.Length > "Adult".Length)
        {
            return modelStem["Adult".Length..];
        }

        if (modelStem.StartsWith("Baby", StringComparison.Ordinal) && modelStem.Length > "Baby".Length)
        {
            return modelStem["Baby".Length..];
        }

        if (modelStem.StartsWith("Cold", StringComparison.Ordinal) && modelStem.Length > "Cold".Length)
        {
            return modelStem["Cold".Length..];
        }

        if (modelStem.StartsWith("Warm", StringComparison.Ordinal) && modelStem.Length > "Warm".Length)
        {
            return modelStem["Warm".Length..];
        }

        return modelStem;
    }

    private static bool PathImpliesOcelot(string normalizedAssetPath, string stem)
    {
        if (stem.StartsWith("ocelot", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = normalizedAssetPath.Replace('\\', '/');
        return path.Contains("/ocelot", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathImpliesSnifflet(string normalizedAssetPath, string stem)
    {
        if (stem.Contains("snifflet", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = normalizedAssetPath.Replace('\\', '/');
        return path.Contains("/sniffer/snifflet", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseModelStem(string? officialJvm, out string package, out string modelStem)
    {
        package = "";
        modelStem = "";
        if (string.IsNullOrWhiteSpace(officialJvm) ||
            officialJvm.Contains("renderer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var idx = officialJvm.LastIndexOf('.');
        if (idx <= 0 || idx >= officialJvm.Length - 1)
        {
            return false;
        }

        var simple = officialJvm[(idx + 1)..];
        if (!simple.EndsWith("Model", StringComparison.Ordinal))
        {
            return false;
        }

        modelStem = simple[..^"Model".Length];
        if (modelStem.Length == 0)
        {
            return false;
        }

        package = officialJvm[..idx];
        return true;
    }

    /// <summary>
    /// Package ends with <c>.equine</c> (no trailing segment); <c>.animal.equine.</c> never matches that suffix.
    /// </summary>
    private static bool IsEquineModelPackage(string package) =>
        package.Contains(".animal.equine", StringComparison.Ordinal);

    /// <summary>
    /// Baby textures must not bind adult mesh hosts when a <c>Baby*</c> shard exists in the index.
    /// </summary>
    private static bool IsAdultEquineJvmRejectedForBaby(bool isBaby, string candidate)
    {
        if (!isBaby ||
            !candidate.Contains(".animal.equine", StringComparison.Ordinal) ||
            SimpleClassNameContainsBaby(candidate))
        {
            return false;
        }

        return candidate.EndsWith(".AbstractEquineModel", StringComparison.Ordinal) ||
               candidate.EndsWith(".HorseModel", StringComparison.Ordinal) ||
               candidate.EndsWith(".DonkeyModel", StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>equine.HorseModel</c> shard was lifted from <c>BabyHorseModel</c> bytecode (body Y≈12.5); adult hosts use <c>AbstractEquineModel</c> (Y≈11).
    /// </summary>
    private static bool IsMisLiftedAdultEquineHorseModelShard(string candidate, JsonElement geometryRoot)
    {
        if (!candidate.EndsWith(".animal.equine.HorseModel", StringComparison.Ordinal) ||
            SimpleClassNameContainsBaby(candidate))
        {
            return false;
        }

        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (TryFindPartBodyTranslationY(root, out var bodyY) && bodyY > 11.75f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPartBodyTranslationY(JsonElement part, out float translationY)
    {
        translationY = 0f;
        if (part.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), "body", StringComparison.Ordinal) &&
            part.TryGetProperty("pose", out var pose) &&
            pose.TryGetProperty("translation", out var tr) &&
            tr.ValueKind == JsonValueKind.Array &&
            tr.GetArrayLength() >= 2 &&
            tr[1].TryGetSingle(out translationY))
        {
            return true;
        }

        if (!part.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (TryFindPartBodyTranslationY(child, out translationY))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adult <c>ModelLayers.ZOMBIE</c> bakes <c>HumanoidModel.createMesh</c>; do not bind baby <c>BabyZombieModel</c> IR on adult skins.
    /// </summary>
    private static bool IsMisLiftedAdultZombieBabyMesh(
        bool isBaby,
        string normalizedAssetPath,
        string candidate,
        JsonElement geometryRoot)
    {
        if (isBaby || !candidate.Contains(".monster.zombie.", StringComparison.Ordinal))
        {
            return false;
        }

        var path = normalizedAssetPath.Replace('\\', '/');
        if (!IsAdultCataloguedZombieFamilyTexture(path))
        {
            return false;
        }

        if (GeometryIrParityJvmResolver.SimpleClassNameContainsBaby(candidate))
        {
            return true;
        }

        if (!candidate.Contains("ZombieModel", StringComparison.Ordinal))
        {
            return false;
        }

        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (TryFindPartBodyTranslationY(root, out var bodyY) && bodyY > 11.75f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAdultCataloguedZombieFamilyTexture(string path) =>
        path.Contains("/textures/entity/zombie/", StringComparison.OrdinalIgnoreCase) &&
        !path.Contains("_baby", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Baby snifflet textures must bind <c>SniffletModel</c> (bone Y=24), not adult <c>SnifferModel</c> (bone Y=5).
    /// </summary>
    private static bool IsMisLiftedAdultSnifferOnSniffletBaby(
        bool isBaby,
        string normalizedAssetPath,
        string candidate,
        JsonElement geometryRoot)
    {
        if (!isBaby || !PathImpliesSnifflet(normalizedAssetPath, Path.GetFileNameWithoutExtension(normalizedAssetPath)))
        {
            return false;
        }

        if (!candidate.Contains(".animal.sniffer.", StringComparison.Ordinal) ||
            candidate.Contains("SniffletModel", StringComparison.Ordinal))
        {
            return false;
        }

        if (TryFindPartBoneTranslationY(geometryRoot, out var boneY) && boneY < 12f)
        {
            return true;
        }

        return candidate.Contains("SnifferModel", StringComparison.Ordinal);
    }

    private static bool TryFindPartBoneTranslationY(JsonElement node, out float translationY)
    {
        translationY = 0f;
        if (node.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (node.TryGetProperty("id", out var idProp) &&
            string.Equals(idProp.GetString(), "bone", StringComparison.Ordinal) &&
            node.TryGetProperty("pose", out var pose) &&
            pose.TryGetProperty("translation", out var tr) &&
            tr.ValueKind == JsonValueKind.Array &&
            tr.GetArrayLength() >= 2)
        {
            translationY = (float)tr[1].GetDouble();
            return true;
        }

        if (!node.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var child in children.EnumerateArray())
        {
            if (TryFindPartBoneTranslationY(child, out translationY))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathImpliesClimate(string normalizedAssetPath, string stem, string token)
    {
        if (stem.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = normalizedAssetPath.Replace('\\', '/');
        return path.Contains($"/{token}_", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"_{token}_", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"_{token}.", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith($"_{token}", StringComparison.OrdinalIgnoreCase);
    }
}
