namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Collects float constants from synthetic javap-style bytecode lines (pool-accurate ldc comments).
/// Used when geometry lift runs without a javap subprocess.
/// </summary>
internal static class BytecodeFloatProbe
{
    public static IReadOnlyList<float> CollectFromSyntheticBytecode(string syntheticJavap) =>
        JavapMethodFloatProbe.CollectFloats(syntheticJavap);

    public static bool TryCollectFromClassBytes(
        ReadOnlySpan<byte> classFile,
        IReadOnlyList<string> methodNames,
        out IReadOnlyList<float> floats)
    {
        var concat = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(classFile, methodNames, out _);
        if (string.IsNullOrEmpty(concat))
        {
            floats = [];
            return false;
        }

        floats = CollectFromSyntheticBytecode(concat);
        return floats.Count > 0;
    }
}
