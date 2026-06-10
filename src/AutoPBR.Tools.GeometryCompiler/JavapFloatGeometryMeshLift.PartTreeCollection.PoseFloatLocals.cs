using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>
    /// <c>ldc</c>/<c>fconst_*</c>/<c>fneg</c> + <c>fstore</c> patterns so <c>PartPose</c> stacks can resolve <c>fload</c> to a constant.
    /// </summary>
    private static Dictionary<int, double> BuildPoseFloatLocalConstantsFromSimpleFstores(List<string> lines)
    {
        var map = new Dictionary<int, double>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseFstoreLocalSlot(lines[i], out var slot))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[i - 1], out var fv))
            {
                map[slot] = fv;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.JavapI2fInsnRegex().IsMatch(lines[i - 1]) && i >= 2 && JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[i - 2], out var iv))
            {
                map[slot] = iv;
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.JavapFnegInsnRegex().IsMatch(lines[i - 1]) && i >= 2 && JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[i - 2], out var inner))
            {
                map[slot] = -inner;
            }
        }

        PropagatePoseFloatLocalCopies(lines, map);
        ApplyMeshFactoryFloatParamFromCallSites(lines, map);
        ApplyMeshFactoryFloatParamDefaultForCreateMesh(lines, map);
        return map;
    }

    /// <summary>
    /// <c>createMesh(CubeDeformation, float)</c> uses parameter slot 1 in <c>PartPose.offset</c> via <c>fload_1</c>.
    /// Default <c>0</c> matches <c>GeometryReferenceBake.invokeWithDefaults</c> (reference_java bakes).
    /// </summary>
    private static void ApplyMeshFactoryFloatParamDefaultForCreateMesh(IReadOnlyList<string> lines, Dictionary<int, double> map)
    {
        if (map.ContainsKey(1))
        {
            return;
        }

        if (!UsesMeshFactoryFloatParamInPartPoseOffsets(lines))
        {
            return;
        }

        map[1] = 0.0;
    }

    private static bool UsesMeshFactoryFloatParamInPartPoseOffsets(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains("PartPose.offset", StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = i - 1; j >= 0 && j > i - 32; j--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[j], out var slot) && slot == 1)
                {
                    return true;
                }
            }
        }

        for (var i = 0; i < lines.Count; i++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[i], out var slot) && slot == 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves <c>fload</c> slots used as the float argument to <c>createMesh(CubeDeformation, float)</c> from call sites in the same concat.
    /// </summary>
    private static void ApplyMeshFactoryFloatParamFromCallSites(List<string> lines, Dictionary<int, double> map)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("createMesh", StringComparison.Ordinal) ||
                (!line.Contains(";F)", StringComparison.Ordinal) && !line.Contains("(F)", StringComparison.Ordinal)))
            {
                continue;
            }

            if (!TryExtractBackwardFloatConstant(lines, i - 1, map, out var meshHeight))
            {
                continue;
            }

            map[1] = meshHeight;
        }
    }

    private static void PropagatePoseFloatLocalCopies(List<string> lines, Dictionary<int, double> map)
    {
        for (var pass = 0; pass < 4; pass++)
        {
            var changed = false;
            for (var i = 1; i < lines.Count; i++)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseFstoreLocalSlot(lines[i], out var dst) ||
                    !JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[i - 1], out var src) ||
                    !map.TryGetValue(src, out var v))
                {
                    continue;
                }

                if (!map.TryGetValue(dst, out var existing) || Math.Abs(existing - v) > 1e-9)
                {
                    map[dst] = v;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }
    }

    private static bool TryExtractBackwardFloatConstant(List<string> lines, int startIdx,
        Dictionary<int, double> floatLocals, out double value)
    {
        value = 0;
        for (var j = startIdx; j >= 0; j--)
        {
            if (lines[j].Contains("invokestatic", StringComparison.Ordinal) ||
                lines[j].Contains("invokevirtual", StringComparison.Ordinal) ||
                lines[j].Contains("invokespecial", StringComparison.Ordinal))
            {
                break;
            }

            if (JavapBytecodeStreamAnalyzer.IsBackwardStackNoiseLine(lines[j]) ||
                lines[j].Contains("iconst_", StringComparison.Ordinal))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[j], out var fv))
            {
                value = fv;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.JavapI2fInsnRegex().IsMatch(lines[j]) && j >= 1 && JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j - 1], out var iv))
            {
                value = iv;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(lines[j], out var slot) && floatLocals.TryGetValue(slot, out var loaded))
            {
                value = loaded;
                return true;
            }
        }

        return false;
    }
}
