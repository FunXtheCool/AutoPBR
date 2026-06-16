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

    /// <summary>
    /// Bytecode fallback when mappings pins are missing but the class only exposes <c>apply(MeshDefinition)</c>.
    /// </summary>
    public static bool IsMeshDefinitionTransformerOnly(ReadOnlySpan<byte> classBytes, MojangMappingsParser? maps)
    {
        var hasApply = false;
        var hasBodyFactory = false;

        foreach (var (_, desc, isStatic) in JvmClassFileParser.EnumerateMethods(classBytes))
        {
            if (!isStatic)
            {
                continue;
            }

            if (IsLayerDefinitionFactoryDescriptor(desc, maps) ||
                IsStaticMeshDefinitionFactoryDescriptor(desc, maps))
            {
                hasBodyFactory = true;
            }
            else if (IsMeshDefinitionApplyDescriptor(desc, maps))
            {
                hasApply = true;
            }
        }

        return hasApply && !hasBodyFactory;
    }

    /// <summary>
    /// <c>createMesh</c> / <c>createBodyMesh</c> factories return <c>MeshDefinition</c> but are not
    /// <c>apply(MeshDefinition)</c> transformers (<c>PlayerModel</c>, <c>HumanoidModel</c>, …).
    /// </summary>
    private static bool IsStaticMeshDefinitionFactoryDescriptor(string descriptor, MojangMappingsParser? maps) =>
        JvmClassFileParser.IsMeshFactoryDescriptor(descriptor, maps) &&
        !IsMeshDefinitionApplyDescriptor(descriptor, maps);

    private static bool IsLayerDefinitionFactoryDescriptor(string descriptor, MojangMappingsParser? maps)
    {
        if (descriptor.Contains("LayerDefinition", StringComparison.Ordinal))
        {
            return true;
        }

        return maps is not null &&
               JvmClassFileParser.TryGetMethodReturnTypeJarSimple(descriptor, out var ret) &&
               maps.TryIsObfuscatedReturnType(ret, "LayerDefinition");
    }

    private static bool IsMeshDefinitionApplyDescriptor(string descriptor, MojangMappingsParser? maps)
    {
        if (descriptor.Contains("ArmorModelSet", StringComparison.Ordinal))
        {
            return false;
        }

        if (!ReturnsMeshDefinition(descriptor, maps))
        {
            return false;
        }

        var open = descriptor.IndexOf('(');
        var close = descriptor.IndexOf(')');
        if (open < 0 || close <= open)
        {
            return false;
        }

        var parameters = descriptor[(open + 1)..close];
        if (parameters.Contains("MeshDefinition", StringComparison.Ordinal))
        {
            return true;
        }

        return maps is not null &&
               parameters.StartsWith('L') &&
               parameters.EndsWith(';') &&
               parameters.Count(c => c == 'L') == 1;
    }

    private static bool ReturnsMeshDefinition(string descriptor, MojangMappingsParser? maps)
    {
        if (descriptor.Contains("MeshDefinition", StringComparison.Ordinal))
        {
            return true;
        }

        return maps is not null &&
               JvmClassFileParser.TryGetMethodReturnTypeJarSimple(descriptor, out var ret) &&
               maps.TryIsObfuscatedReturnType(ret, "MeshDefinition");
    }
}
