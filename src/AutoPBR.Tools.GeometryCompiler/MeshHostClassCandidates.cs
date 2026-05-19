namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Mojang often splits <c>EntityModel</c> subclasses (fields + <c>setupAnim</c>) from static mesh factories
/// (<c>AdultFooModel.createBodyLayer</c>). Try the official FQN first, then common adult/baby/cold/warm companions in the same package.
/// </summary>
internal static class MeshHostClassCandidates
{
    public static IEnumerable<string> Enumerate(string officialJvmName)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in EnumerateRaw(officialJvmName))
        {
            if (seen.Add(s))
            {
                yield return s;
            }
        }
    }

    private static IEnumerable<string> EnumerateRaw(string officialJvmName)
    {
        yield return officialJvmName;
        var idx = officialJvmName.LastIndexOf('.');
        if (idx < 0)
        {
            yield break;
        }

        var pkg = officialJvmName[..idx];
        var simple = officialJvmName[(idx + 1)..];
        if (!simple.EndsWith("Model", StringComparison.Ordinal))
        {
            yield break;
        }

        var stem = simple[..^"Model".Length];
        if (stem.Length == 0)
        {
            yield break;
        }

        // Mesh factories often live on the concrete class (ZombieModel) while the batch entry is AbstractZombieModel.
        const string abstractPrefix = "Abstract";
        if (stem.StartsWith(abstractPrefix, StringComparison.Ordinal) &&
            stem.Length > abstractPrefix.Length)
        {
            var rest = stem[abstractPrefix.Length..];
            yield return $"{pkg}.{rest}Model";
            yield return $"{pkg}.Adult{rest}Model";
            yield return $"{pkg}.Baby{rest}Model";
        }

        const string statueSuffix = "Statue";
        if (stem.EndsWith(statueSuffix, StringComparison.Ordinal) && stem.Length > statueSuffix.Length)
        {
            yield return $"{pkg}.{stem[..^statueSuffix.Length]}Model";
        }

        const string headSuffix = "Head";
        if (stem.EndsWith(headSuffix, StringComparison.Ordinal) && stem.Length > headSuffix.Length &&
            pkg.Contains(".skull", StringComparison.Ordinal))
        {
            yield return $"{pkg}.SkullModel";
        }

        if (pkg.EndsWith(".animal.feline", StringComparison.Ordinal) &&
            (stem.StartsWith("AdultCat", StringComparison.Ordinal) ||
             stem.StartsWith("BabyCat", StringComparison.Ordinal) ||
             stem.StartsWith("AdultOcelot", StringComparison.Ordinal) ||
             stem.StartsWith("BabyOcelot", StringComparison.Ordinal) ||
             string.Equals(stem, "Cat", StringComparison.Ordinal) ||
             string.Equals(stem, "Ocelot", StringComparison.Ordinal)))
        {
            yield return $"{pkg}.AdultFelineModel";
            yield return $"{pkg}.BabyFelineModel";
        }

        // e.g. VillagerLikeModel -> VillagerModel
        const string likeSuffix = "Like";
        if (stem.EndsWith(likeSuffix, StringComparison.Ordinal) && stem.Length > likeSuffix.Length)
        {
            yield return $"{pkg}.{stem[..^likeSuffix.Length]}Model";
        }

        yield return $"{pkg}.Adult{stem}Model";
        yield return $"{pkg}.Baby{stem}Model";
        yield return $"{pkg}.Cold{stem}Model";
        yield return $"{pkg}.Warm{stem}Model";

        // ColdCowModel / WarmCowModel / ColdChickenModel mesh factories delegate to CowModel.createBaseCowModel, etc.
        if (stem.StartsWith("Cold", StringComparison.Ordinal) && stem.Length > "Cold".Length)
        {
            yield return $"{pkg}.{stem["Cold".Length..]}Model";
        }

        if (stem.StartsWith("Warm", StringComparison.Ordinal) && stem.Length > "Warm".Length)
        {
            yield return $"{pkg}.{stem["Warm".Length..]}Model";
        }
    }

    /// <summary>
    /// Common Mojang pattern: <c>AdultCowModel</c> / <c>BabyChickenModel</c> → <c>AbstractCowModel</c> / <c>AbstractChickenModel</c>.
    /// </summary>
    public static IEnumerable<string> EnumerateAbstractCompanionFqns(string meshHostOfficialOuter)
    {
        var idx = meshHostOfficialOuter.LastIndexOf('.');
        if (idx < 0)
        {
            yield break;
        }

        var pkg = meshHostOfficialOuter[..idx];
        var simple = meshHostOfficialOuter[(idx + 1)..];
        if (!simple.EndsWith("Model", StringComparison.Ordinal))
        {
            yield break;
        }

        var prefixes = new[]
        {
            "Adult", "Baby", "Cold", "Warm", "Zombified", "Dead", "Elder", "Drowned", "Husk", "Stray", "Wither"
        };
        foreach (var p in prefixes)
        {
            if (!simple.StartsWith(p, StringComparison.Ordinal))
            {
                continue;
            }

            var rest = simple[p.Length..];
            if (rest.Length == 0)
            {
                continue;
            }

            yield return $"{pkg}.Abstract{rest}";
        }
    }
}
