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

        return JvmClassFileParser.HasStaticMeshFactoryMethod(classBytes);
    }

    public static string ResolveFactoryMethod(MojangMappingsParser? maps, string officialJvmName, string requested)
    {
        if (maps is null)
        {
            return requested;
        }

        if (maps.EnumerateMeshFactoryPins(officialJvmName).Any(p =>
                string.Equals(p.NamedMethod, requested, StringComparison.Ordinal)))
        {
            return requested;
        }

        if (maps.EnumerateMeshFactoryPins(officialJvmName).Any(p =>
                string.Equals(p.NamedMethod, "createMesh", StringComparison.Ordinal)))
        {
            return "createMesh";
        }

        return requested;
    }
}
