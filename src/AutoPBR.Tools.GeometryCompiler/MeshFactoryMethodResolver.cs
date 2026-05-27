namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Picks the static mesh-factory entry point for a model class (named or ProGuard jars).
/// </summary>
internal static class MeshFactoryMethodResolver
{
    private static readonly string[] PreferredFactoryMethods =
    [
        "createBodyLayer",
        "createLayer",
        "createSpiderBodyLayer",
        "createOuterBodyLayer",
        "createInnerBodyLayer",
        "createMesh",
        "createCapeLayer",
        "createBabyMesh",
        "createBabyLayer",
        "createBaseChickenModel",
        "createBodyMesh",
        "apply",
        "createSingleBodyLayer",
        "createDoubleBodyRightLayer",
        "createDoubleBodyLeftLayer",
        "createHeadLayer",
        "createHatLayer",
        "createEarsLayer",
        "createTranslucentBodyLayer",
        "createSaddleLayer",
        "createHarnessLayer",
        "createFurLayer",
        "createBasePigModel",
        "createArmorLayer",
    ];

    public static string Resolve(
        MojangMappingsParser? maps,
        string officialJvmName,
        string requested,
        ReadOnlySpan<byte> classBytes)
    {
        if (maps is not null)
        {
            return ProguardMeshFactoryDetection.ResolveFactoryMethod(maps, officialJvmName, requested);
        }

        if (HasMethod(classBytes, requested))
        {
            return requested;
        }

        foreach (var candidate in PreferredFactoryMethods)
        {
            if (HasMethod(classBytes, candidate))
            {
                return candidate;
            }
        }

        return TryGetFirstStaticMeshFactoryMethodName(classBytes, maps) ?? requested;
    }

    private static bool HasMethod(ReadOnlySpan<byte> classBytes, string methodName) =>
        JvmClassFileParser.TryGetMethodCode(classBytes, methodName) is not null;

    private static string? TryGetFirstStaticMeshFactoryMethodName(ReadOnlySpan<byte> classBytes, MojangMappingsParser? maps)
    {
        foreach (var (name, desc, isStatic) in JvmClassFileParser.EnumerateMethods(classBytes))
        {
            if (!isStatic)
            {
                continue;
            }

            if (JvmClassFileParser.IsMeshFactoryDescriptor(desc, maps))
            {
                return name;
            }
        }

        return null;
    }
}
