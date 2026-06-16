using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Copies lifted geometry IR from a mesh-host sibling when the index row is constructor-only at runtime.
/// </summary>
internal static class GeometryIrDelegatedMeshCopy
{
    private static readonly Dictionary<string, (string SourceJvm, string Rationale)> Delegates =
        new(StringComparer.Ordinal)
        {
            ["net.minecraft.client.model.monster.zombie.GiantZombieModel"] = (
                "net.minecraft.client.model.monster.zombie.ZombieModel",
                "GiantZombieModel is a ModelPart wrapper; IR delegated from ZombieModel (HumanoidModel.createMesh layer)."),
            ["net.minecraft.client.model.monster.guardian.GuardianParticleModel"] = (
                "net.minecraft.client.model.monster.guardian.GuardianModel",
                "GuardianParticleModel reuses guardian body mesh at runtime; IR delegated from GuardianModel.createBodyLayer."),
            ["net.minecraft.client.model.object.statue.CopperGolemStatueModel"] = (
                "net.minecraft.client.model.animal.golem.CopperGolemModel",
                "CopperGolemStatueModel is a statue render wrapper; IR delegated from CopperGolemModel.createBodyLayer."),
            ["net.minecraft.client.model.animal.rabbit.RabbitModel"] = (
                "net.minecraft.client.model.animal.rabbit.AdultRabbitModel",
                "RabbitModel is setupAnim-only at runtime; IR delegated from AdultRabbitModel.createBodyLayer."),
            ["net.minecraft.client.model.animal.feline.AdultCatModel"] = (
                "net.minecraft.client.model.animal.feline.AdultFelineModel",
                "AdultCatModel is setupAnim-only at runtime; IR delegated from AdultFelineModel.createBodyMesh."),
            ["net.minecraft.client.model.animal.feline.BabyCatModel"] = (
                "net.minecraft.client.model.animal.feline.BabyFelineModel",
                "BabyCatModel is setupAnim-only at runtime; IR delegated from BabyFelineModel.createBodyLayer."),
            ["net.minecraft.client.model.animal.feline.AdultOcelotModel"] = (
                "net.minecraft.client.model.animal.feline.AdultFelineModel",
                "AdultOcelotModel is setupAnim-only at runtime; IR delegated from AdultFelineModel.createBodyMesh."),
            ["net.minecraft.client.model.animal.feline.BabyOcelotModel"] = (
                "net.minecraft.client.model.animal.feline.BabyFelineModel",
                "BabyOcelotModel is setupAnim-only at runtime; IR delegated from BabyFelineModel.createBodyLayer."),
        };

    public static bool HasDelegate(string officialJvmName) =>
        Delegates.ContainsKey(officialJvmName);

    public static bool TryApply(
        string outDir,
        string versionLabel,
        string officialJvmName,
        JsonObject shard,
        out string note)
    {
        note = string.Empty;
        if (!Delegates.TryGetValue(officialJvmName, out var spec))
        {
            return false;
        }

        var sourcePath = Path.Combine(outDir, "geometry", versionLabel, $"{spec.SourceJvm}.json");
        if (!File.Exists(sourcePath))
        {
            return false;
        }

        JsonObject source;
        try
        {
            source = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
        }
        catch
        {
            return false;
        }

        if (!string.Equals(source["extractionStatus"]?.GetValue<string>(), "ok", StringComparison.Ordinal) ||
            source["roots"] is not JsonArray sourceRoots ||
            CountAllCuboids(sourceRoots) == 0)
        {
            return false;
        }

        shard["roots"] = JsonNode.Parse(sourceRoots.ToJsonString())!;
        shard["extractionStatus"] = "ok";
        shard["delegatedFromOfficialJvmName"] = spec.SourceJvm;
        if (source["factoryMethod"] is JsonValue factory)
        {
            shard["factoryMethod"] = factory.GetValue<string>();
        }

        if (source["textureWidth"] is JsonNode texW)
        {
            shard["textureWidth"] = JsonNode.Parse(texW.ToJsonString());
        }

        if (source["textureHeight"] is JsonNode texH)
        {
            shard["textureHeight"] = JsonNode.Parse(texH.ToJsonString());
        }

        var delegationDepth = source["liftSummary"]?["delegationDepth"]?.GetValue<int>() ?? 0;
        shard["liftSummary"] = GeometryIrLiftSummaryBuilder.BuildFromRoots(
            shard["roots"]!.AsArray(),
            delegationDepth + 1);
        shard["extractionNotes"] = new JsonArray(
            spec.Rationale,
            $"Delegated mesh copy from {spec.SourceJvm}.");
        note = spec.Rationale;
        return true;
    }

    private static int CountAllCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var node in roots)
        {
            if (node is not JsonObject part)
            {
                continue;
            }

            if (part["cuboids"] is JsonArray c)
            {
                n += c.Count;
            }

            if (part["children"] is JsonArray kids)
            {
                n += CountAllCuboids(kids);
            }
        }

        return n;
    }
}
