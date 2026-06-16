namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Synthetic geometry-index JVM rows that lift a real class with an alternate static factory
/// (e.g. parched skeleton <c>createSingleModelDualBodyLayer</c>).
/// </summary>
internal static class GeometryIrVariantLiftMap
{
    internal readonly record struct VariantLiftSpec(string SourceOfficialJvmName, string FactoryMethod);

    private static readonly Dictionary<string, VariantLiftSpec> ByVariantOfficialJvmName =
        new(StringComparer.Ordinal)
        {
            ["net.minecraft.client.model.monster.skeleton.SkeletonModel.createSingleModelDualBodyLayer"] = new(
                "net.minecraft.client.model.monster.skeleton.SkeletonModel",
                "createSingleModelDualBodyLayer"),
        };

    public static bool TryGet(string variantOfficialJvmName, out VariantLiftSpec spec) =>
        ByVariantOfficialJvmName.TryGetValue(variantOfficialJvmName, out spec!);
}
