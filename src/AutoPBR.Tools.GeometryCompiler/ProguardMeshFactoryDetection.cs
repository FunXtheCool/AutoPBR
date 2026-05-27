namespace AutoPBR.Tools.GeometryCompiler;

internal static class ProguardMeshFactoryDetection
{
    public static bool HasResolvableMeshFactory(
        MojangMappingsParser? maps,
        string officialJvmName,
        ReadOnlySpan<byte> classBytes)
    {
        if (maps is not null)
        {
            foreach (var _ in maps.EnumerateMeshFactoryPins(officialJvmName))
            {
                return true;
            }
        }

        return JvmClassFileParser.HasStaticMeshFactoryMethod(classBytes, maps);
    }

    public static string ResolveFactoryMethod(MojangMappingsParser? maps, string officialJvmName, string requested)
    {
        if (maps is null)
        {
            return requested;
        }

        var pins = maps.EnumerateMeshFactoryPins(officialJvmName).ToList();
        if (pins.Any(p => string.Equals(p.NamedMethod, requested, StringComparison.Ordinal)))
        {
            return requested;
        }

        if (string.Equals(requested, "createBodyLayer", StringComparison.Ordinal))
        {
            if (pins.Any(p => string.Equals(p.NamedMethod, "createMesh", StringComparison.Ordinal)))
            {
                return "createMesh";
            }

            if (pins.Any(p => string.Equals(p.NamedMethod, "createBodyLayer", StringComparison.Ordinal)))
            {
                return "createBodyLayer";
            }

            if (pins.Any(p => string.Equals(p.NamedMethod, "apply", StringComparison.Ordinal)))
            {
                return "apply";
            }
        }

        return requested;
    }

    /// <summary>
    /// Types like <c>BabyModelTransform</c> only expose <c>MeshDefinition apply(MeshDefinition)</c> — pose/scale transforms, not part trees.
    /// </summary>
    public static bool IsMeshDefinitionTransformerOnly(MojangMappingsParser? maps, string officialJvmName)
    {
        if (maps is null)
        {
            return false;
        }

        var pins = maps.EnumerateMeshFactoryPins(officialJvmName).ToList();
        if (pins.Count == 0)
        {
            return false;
        }

        var hasApply = pins.Any(p => string.Equals(p.NamedMethod, "apply", StringComparison.Ordinal));
        var hasBodyFactory = pins.Any(p =>
            string.Equals(p.NamedMethod, "createBodyLayer", StringComparison.Ordinal) ||
            string.Equals(p.NamedMethod, "createMesh", StringComparison.Ordinal) ||
            string.Equals(p.NamedMethod, "createBodyMesh", StringComparison.Ordinal));

        return hasApply && !hasBodyFactory;
    }
}
