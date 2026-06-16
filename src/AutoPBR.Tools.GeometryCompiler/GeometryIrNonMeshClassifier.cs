namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Classifies client model classes that never expose a static part-tree factory for geometry IR lift.
/// </summary>
internal static class GeometryIrNonMeshClassifier
{
    internal static bool TryClassify(
        string officialJvmName,
        ReadOnlySpan<byte> classBytes,
        MojangMappingsParser? maps,
        out string skipKind,
        out string note)
    {
        skipKind = string.Empty;
        note = string.Empty;

        if (officialJvmName.Contains('$', StringComparison.Ordinal))
        {
            skipKind = "inner_class";
            note =
                $"Inner or synthetic nested type on {officialJvmName}; no standalone static mesh factory.";
            return true;
        }

        if (JvmClassFileParser.IsInterface(classBytes))
        {
            skipKind = "interface";
            note = $"Interface {officialJvmName}; mesh factories live on concrete model classes.";
            return true;
        }

        if (ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(maps, officialJvmName) ||
            ProguardMeshFactoryDetection.IsMeshDefinitionTransformerOnly(classBytes, maps))
        {
            skipKind = "mesh_transformer";
            note =
                $"MeshDefinition transformer on {officialJvmName} (apply(MeshDefinition)); transform is applied inline in parent createBodyLayer factories, not a standalone part tree.";
            return true;
        }

        if (IsRecordUtility(officialJvmName))
        {
            skipKind = "record_utility";
            note = $"Record/utility type {officialJvmName}; not a mesh host.";
            return true;
        }

        if (IsEnumOrStateType(officialJvmName))
        {
            skipKind = "enum_or_state";
            note = $"Enum/state helper on {officialJvmName}; not a mesh host.";
            return true;
        }

        if (IsRuntimeModelPartWrapper(officialJvmName))
        {
            return false;
        }

        if (!ProguardMeshFactoryDetection.HasResolvableMeshFactory(maps, officialJvmName, classBytes))
        {
            skipKind = "no_mesh_factory";
            note =
                $"No static mesh factory on {officialJvmName}; structural lift skipped (interface, inner class, or non-mesh type).";
            return true;
        }

        return false;
    }

    private static bool IsRecordUtility(string officialJvmName) =>
        officialJvmName is
            "net.minecraft.client.model.AdultAndBabyModelPair" or
            "net.minecraft.client.model.AnimationUtils" or
            "net.minecraft.client.model.effects.SpearAnimations";

    private static bool IsEnumOrStateType(string officialJvmName) =>
        officialJvmName.EndsWith("$State", StringComparison.Ordinal) ||
        officialJvmName.EndsWith("$Pose", StringComparison.Ordinal) ||
        officialJvmName.EndsWith("$UseParams", StringComparison.Ordinal);

    private static bool IsRuntimeModelPartWrapper(string officialJvmName) =>
        GeometryIrDelegatedMeshCopy.HasDelegate(officialJvmName) ||
        LayerDefinitionMeshHostMap.TryGet(officialJvmName, out _) ||
        GeometryIrVariantLiftMap.TryGet(officialJvmName, out _);
}
