namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Runtime <c>ModelLayers</c> entries whose <c>EntityModel</c> class has no static factory but bake a sibling mesh host
/// (javap <c>LayerDefinitions.createRoots</c>).
/// </summary>
internal static class LayerDefinitionMeshHostMap
{
    internal readonly record struct MeshHostSpec(
        string HostOfficialJvmName,
        string FactoryMethod,
        int TextureWidth,
        int TextureHeight);

    private static readonly Dictionary<string, MeshHostSpec> ByOfficialJvmName =
        new(StringComparer.Ordinal)
        {
            ["net.minecraft.client.model.monster.zombie.AbstractZombieModel"] = new(
                "net.minecraft.client.model.HumanoidModel",
                "createMesh",
                64,
                64),
            ["net.minecraft.client.model.monster.zombie.ZombieModel"] = new(
                "net.minecraft.client.model.HumanoidModel",
                "createMesh",
                64,
                64),
            ["net.minecraft.client.model.monster.zombie.GiantZombieModel"] = new(
                "net.minecraft.client.model.HumanoidModel",
                "createMesh",
                64,
                64),
            // LayerDefinitions.createRoots wraps AdultFelineModel.createBodyMesh with LayerDefinition.create(64, 32).
            ["net.minecraft.client.model.animal.feline.AdultFelineModel"] = new(
                "net.minecraft.client.model.animal.feline.AdultFelineModel",
                "createBodyMesh",
                64,
                32),
        };

    public static bool TryGet(string officialJvmName, out MeshHostSpec spec) =>
        ByOfficialJvmName.TryGetValue(officialJvmName, out spec!);
}
