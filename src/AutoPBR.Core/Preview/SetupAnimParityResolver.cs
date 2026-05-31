namespace AutoPBR.Core.Preview;

/// <summary>Maps parity catalog builders and manifest model classes to setup-anim shard JVM names.</summary>
internal static class SetupAnimParityResolver
{
    private static readonly Dictionary<string, string> BuilderToModelJvm = new(StringComparer.Ordinal)
    {
        ["Pig"] = "net.minecraft.client.model.animal.pig.PigModel",
        ["Cow"] = "net.minecraft.client.model.animal.cow.CowModel",
        ["Sheep"] = "net.minecraft.client.model.animal.sheep.SheepModel",
        ["Wolf"] = "net.minecraft.client.model.animal.wolf.WolfModel",
        ["Fox"] = "net.minecraft.client.model.animal.fox.AdultFoxModel",
        ["Goat"] = "net.minecraft.client.model.animal.goat.GoatModel",
        ["Cat"] = "net.minecraft.client.model.animal.feline.AdultCatModel",
        ["Ocelot"] = "net.minecraft.client.model.animal.feline.AdultOcelotModel",
        ["Rabbit"] = "net.minecraft.client.model.animal.rabbit.AdultRabbitModel",
        ["Panda"] = "net.minecraft.client.model.animal.panda.PandaModel",
        ["Chicken"] = "net.minecraft.client.model.animal.chicken.ChickenModel",
        ["Horse"] = "net.minecraft.client.model.animal.equine.AbstractEquineModel",
        ["DonkeyMuleHorse"] = "net.minecraft.client.model.animal.equine.DonkeyModel",
        ["HumanoidZombie"] = "net.minecraft.client.model.monster.zombie.ZombieModel",
        ["Zombie"] = "net.minecraft.client.model.monster.zombie.ZombieModel",
        ["Skeleton"] = "net.minecraft.client.model.monster.skeleton.SkeletonModel",
        ["Creeper"] = "net.minecraft.client.model.monster.creeper.CreeperModel",
    };

    public static string ResolveModelJvm(string? builderMethod, string? deobfuscatedModelClass) =>
        ResolveModelJvmForPreview(builderMethod, deobfuscatedModelClass, isBaby: false, geometryIrOfficialJvm: null);

    /// <summary>
    /// Resolves a setupAnim shard JVM for geometry IR preview (baby peers, manifest class, builder map).
    /// </summary>
    public static string ResolveModelJvmForPreview(
        string? builderMethod,
        string? deobfuscatedModelClass,
        bool isBaby,
        string? geometryIrOfficialJvm)
    {
        foreach (var candidate in EnumerateCandidates(builderMethod, deobfuscatedModelClass, isBaby, geometryIrOfficialJvm))
        {
            if (SetupAnimDocumentLoader.TryLoadOk(candidate, out _))
            {
                return candidate;
            }
        }

        return "net.minecraft.client.model.QuadrupedModel";
    }

    private static IEnumerable<string> EnumerateCandidates(
        string? builderMethod,
        string? deobfuscatedModelClass,
        bool isBaby,
        string? geometryIrOfficialJvm)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        static IEnumerable<string> YieldUnique(HashSet<string> sink, params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v) && sink.Add(v))
                {
                    yield return v;
                }
            }
        }

        if (isBaby && TryParseModelStem(deobfuscatedModelClass, out var pkg, out var stem))
        {
            foreach (var jvm in YieldUnique(seen, $"{pkg}.Baby{NormalizeModelStem(stem)}Model"))
            {
                yield return jvm;
            }
        }

        foreach (var jvm in YieldUnique(seen, geometryIrOfficialJvm, deobfuscatedModelClass))
        {
            yield return jvm;
        }

        if (!string.IsNullOrWhiteSpace(builderMethod) &&
            BuilderToModelJvm.TryGetValue(builderMethod, out var mapped))
        {
            foreach (var jvm in YieldUnique(seen, mapped))
            {
                yield return jvm;
            }
        }
    }

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

        return modelStem;
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
}
