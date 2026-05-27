using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    internal static string? TryResolveBindingPartNameForDiagnostics(List<string> lines, int bindingIdx) =>
        TryResolvePartNameForBinding(lines, bindingIdx, new Dictionary<int, int>(), out var name) ? name : null;

    /// <summary>
    /// Resolves the child id for the nearest <c>PartDefinition.addOrReplaceChild</c> in a lifted segment.
    /// </summary>
    private static string? FindPartNameNearAddOrReplaceChild(List<string> seg,
        IReadOnlyDictionary<int, int> boxIntLocals)
    {
        for (var i = seg.Count - 1; i >= 0; i--)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
            {
                continue;
            }

            if (TryResolvePartNameForBinding(seg, i, boxIntLocals, out var name))
            {
                return name;
            }
        }

        return null;
    }

    private static bool TryInferLoopPartNameFromSegment(List<string> seg, int bindingIdx, int loopSlot, int iteration,
        out string? name)
    {
        name = null;
        for (var j = bindingIdx - 1; j >= 0; j--)
        {
            var line = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("(I)Ljava/lang/String;", StringComparison.Ordinal))
            {
                continue;
            }

            var m = MatchIndexedPartNameFactory(line);
            if (!m.Success)
            {
                continue;
            }

            for (var k = j - 1; k >= 0 && k > j - 16; k--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(seg[k], out var slot) && slot == loopSlot)
                {
                    name = InferIndexedPartNameFromFactoryMethod(m.Groups[1].Value, iteration, seg);
                    return !string.IsNullOrEmpty(name);
                }
            }
        }

        return false;
    }

    private static bool TryParseInvokeStaticIndexedPartNameBeforeBinding(List<string> seg, int bindingIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out string? name)
    {
        name = null;
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 64; j--)
        {
            var line = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("Ljava/lang/String;", StringComparison.Ordinal) ||
                !MatchIndexedPartNameFactory(line).Success)
            {
                continue;
            }

            var method = MatchIndexedPartNameFactory(line).Groups[1].Value;
            if (TryParseIntOperandImmediatelyBefore(seg, j, boxIntLocals, out var index))
            {
                name = InferIndexedPartNameFromFactoryMethod(method, index, seg);
                return !string.IsNullOrEmpty(name);
            }

            if (method.Contains("Name", StringComparison.Ordinal))
            {
                index = _invokeStaticPartNameBindingOrdinal++;
                name = InferIndexedPartNameFromFactoryMethod(method, index, seg);
                return !string.IsNullOrEmpty(name);
            }
        }

        return false;
    }

    private static Match MatchIndexedPartNameFactory(string line) =>
        Regex.Match(line, @"(?:[\w$/]+\.)?(\w+):\(I\)Ljava/lang/String;", RegexOptions.CultureInvariant);

    private static bool TryParseIntOperandImmediatelyBefore(List<string> seg, int invokeLineIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out int value)
    {
        value = 0;
        for (var j = invokeLineIdx - 1; j >= 0 && j > invokeLineIdx - 64; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[j], out var iv))
            {
                value = (int)iv;
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(seg[j], out var slot))
            {
                if (boxIntLocals.TryGetValue(slot, out value))
                {
                    return true;
                }

                if (TryResolveIntLocal(seg, j, slot, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryResolveIntLocal(List<string> seg, int useLineIdx, int slot, out int value)
    {
        value = 0;
        for (var j = useLineIdx - 1; j >= 0 && j > useLineIdx - 40; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseIstoreLocalSlot(seg[j], out var stored) && stored == slot)
            {
                for (var k = j - 1; k >= 0 && k > j - 6; k--)
                {
                    if (JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[k], out var iv))
                    {
                        value = (int)iv;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string InferIndexedPartNameFromFactoryMethod(string methodName, int index, List<string>? seg = null)
    {
        if (string.Equals(methodName, "getSegmentName", StringComparison.Ordinal) &&
            seg is not null &&
            seg.Any(static l => l.Contains("MagmaCubeModel", StringComparison.Ordinal)))
        {
            return $"cube{index}";
        }

        if (methodName.Contains("Tentacle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(methodName, "tentacle", StringComparison.Ordinal))
        {
            return $"tentacle{index}";
        }

        if (methodName.Contains("Neck", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(methodName, "neckName", StringComparison.Ordinal))
        {
            return $"neck{index}";
        }

        if (string.Equals(methodName, "tailName", StringComparison.Ordinal))
        {
            return $"tail{index}";
        }

        if (string.Equals(methodName, "boxName", StringComparison.Ordinal))
        {
            return $"box{index}";
        }

        if (methodName.Contains("Cube", StringComparison.OrdinalIgnoreCase))
        {
            return $"cube{index}";
        }

        if (methodName.Contains("Segment", StringComparison.OrdinalIgnoreCase))
        {
            return $"segment{index}";
        }

        if (methodName.Contains("Layer", StringComparison.OrdinalIgnoreCase))
        {
            return $"layer{index}";
        }

        if (string.Equals(methodName, "getPartName", StringComparison.Ordinal))
        {
            return $"part{index}";
        }

        if (string.Equals(methodName, "createSpikeName", StringComparison.Ordinal))
        {
            return $"spike{index}";
        }

        if (methodName.Length <= 2 && methodName.All(char.IsLetter))
        {
            return $"segment{index}";
        }

        return $"{methodName}_{index}";
    }

    private static bool TryResolvePartNameForBinding(List<string> seg, int bindingIdx,
        IReadOnlyDictionary<int, int> boxIntLocals, out string? name)
    {
        if (TryParseInvokeStaticIndexedPartNameBeforeBinding(seg, bindingIdx, boxIntLocals, out name))
        {
            return true;
        }

        if (TryFindPartNameLdcImmediatelyBeforeBinding(seg, bindingIdx, out name))
        {
            return true;
        }

        if (TryFindPartNameBeforeCubeListBuilderCreate(seg, bindingIdx, out name))
        {
            return true;
        }

        // Quadruped legs often reuse a mirrored CubeListBuilder: ldc childName, aload builder, PartPose, addOrReplaceChild.
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 32; j--)
        {
            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
            if (!sm.Success)
            {
                continue;
            }

            for (var k = j + 1; k <= bindingIdx && k < j + 10; k++)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[k], out _))
                {
                    name = sm.Groups[1].Value;
                    return true;
                }
            }
        }

        name = null;
        return false;
    }

    private static bool SegmentHasExplicitLdcPartNameBeforeBinding(List<string> seg, int bindingIdx)
    {
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 48; j--)
        {
            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
            if (!sm.Success)
            {
                continue;
            }

            for (var k = j + 1; k <= bindingIdx && k < j + 12; k++)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[k], out _) ||
                    seg[k].Contains("PartPose", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Skip loop unroll when the segment already names parts via ldc (quadruped legs, binding-site ids, etc.).
    /// </summary>
    private static bool SegmentHasExplicitPartNamingForLoopUnroll(List<string> seg)
    {
        for (var i = 0; i < seg.Count; i++)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
            {
                continue;
            }

            if (TryFindPartNameLdcImmediatelyBeforeBinding(seg, i, out _) ||
                TryFindPartNameBeforeCubeListBuilderCreate(seg, i, out _) ||
                SegmentHasExplicitLdcPartNameBeforeBinding(seg, i))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// javac often pushes the mesh child name ldc immediately before <c>addOrReplaceChild</c> (no aload between).
    /// </summary>
    private static bool TryFindPartNameLdcImmediatelyBeforeBinding(List<string> seg, int bindingIdx, out string? name)
    {
        name = null;
        if (bindingIdx <= 0)
        {
            return false;
        }

        var minIdx = Math.Max(0, bindingIdx - PartNameLdcImmediateLookbackLines);
        for (var j = bindingIdx - 1; j >= minIdx; j--)
        {
            if (IsNamedOrObfuscatedMeshBindingNoiseBetweenPartNameAndInvoke(seg[j]))
            {
                continue;
            }

            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
            if (!sm.Success)
            {
                if (seg[j].Contains("PartPose", StringComparison.Ordinal) ||
                    seg[j].Contains("invokestatic", StringComparison.Ordinal) ||
                    JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out _) ||
                    JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[j], out _) ||
                    JavapBytecodeStreamAnalyzer.TryParseIntLine(seg[j], out _))
                {
                    continue;
                }

                break;
            }

            name = sm.Groups[1].Value;
            return true;
        }

        return false;
    }

    private static bool IsNamedOrObfuscatedMeshBindingNoiseBetweenPartNameAndInvoke(string line) =>
        JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(line, out _) ||
        line.Contains("dup", StringComparison.Ordinal) ||
        line.Contains("swap", StringComparison.Ordinal);

    private static bool TryFindPartNameBeforeCubeListBuilderCreate(List<string> seg, int maxLineIdx, out string? name)
    {
        name = null;
        for (var j = maxLineIdx - 1; j >= 0; j--)
        {
            if (!seg[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) &&
                !IsObfuscatedCubeListBuilderCreateLine(seg[j]))
            {
                continue;
            }

            for (var k = j - 1; k >= 0; k--)
            {
                var m = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]);
                if (m.Success)
                {
                    name = m.Groups[1].Value;
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    private static string? FindFirstPartName(List<string> seg)
    {
        for (var i = 0; i < seg.Count; i++)
        {
            if (!seg[i].Contains("CubeListBuilder.create", StringComparison.Ordinal) &&
                !IsObfuscatedCubeListBuilderCreateLine(seg[i]))
            {
                continue;
            }

            for (var j = i - 1; j >= 0; j--)
            {
                var m = JavapBytecodeStreamAnalyzer.MatchLdcString(seg[j]);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
        }

        return null;
    }
}
