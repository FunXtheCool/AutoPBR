using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>
    /// Scans backward for <c>new CubeDeformation</c> + <c>dup</c> + float + <c>invokespecial &lt;init&gt;(F)V</c> (named javap only).
    /// </summary>
    private static bool TryReadUniformCubeDeformationCtorInvokespecialLine(List<string> seg, int searchFromInclusive,
        int minLineInclusive, MojangMappingsParser? maps, out double inflate)
    {
        inflate = 0;
        var winLow = minLineInclusive;
        for (var ctorIdx = searchFromInclusive; ctorIdx >= winLow; ctorIdx--)
        {
            var ml = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, ctorIdx);
            if (!ml.Contains("invokespecial", StringComparison.Ordinal) ||
                !ml.Contains("<init>", StringComparison.Ordinal) ||
                !ml.Contains("(F)V", StringComparison.Ordinal))
            {
                continue;
            }

            var isNamedDef = ml.Contains("CubeDeformation", StringComparison.Ordinal);
            var isObfDef = !isNamedDef && maps is not null &&
                           maps.TryGetObfuscated("net.minecraft.client.model.geom.builders.CubeDeformation", out var obf) &&
                           JavapBytecodeStreamAnalyzer.ObfJavapLineReferencesShortType(ml, MojangMappingsParser.GetJavapClassArgForObfuscated(obf));
            if (!isNamedDef && !isObfDef)
            {
                continue;
            }

            var floatIdx = -1;
            var dupIdx = -1;
            var newIdx = -1;
            for (var j = ctorIdx - 1; j >= winLow; j--)
            {
                var jl = seg[j];
                if (floatIdx < 0 && JavapBytecodeStreamAnalyzer.TryParseFloatLine(jl, out _))
                {
                    floatIdx = j;
                    continue;
                }

                if (floatIdx >= 0 && dupIdx < 0 && jl.Contains("dup", StringComparison.Ordinal))
                {
                    dupIdx = j;
                    continue;
                }

                if (newIdx < 0 && jl.Contains("new", StringComparison.Ordinal))
                {
                    newIdx = j;
                    if (dupIdx >= 0)
                    {
                        break;
                    }
                }
            }

            if (floatIdx >= 0 && newIdx >= 0 && JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[floatIdx], out inflate))
            {
                var newLine = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, newIdx);
                if (newLine.Contains("CubeDeformation", StringComparison.Ordinal) || isObfDef)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// <c>inflate</c> from inline <c>new CubeDeformation(f)</c> before <c>addBox</c>, or from an <c>aload</c> slot
    /// mapped by <see cref="BuildCubeDeformationInflateByRefLocalSlot"/>.
    /// </summary>
    private static bool TryResolveUniformCubeDeformationInflate(List<string> seg, int addBoxInvokeLineIdx,
        int minLineInclusive, IReadOnlyDictionary<int, double> cubeDeformationInflateByRefSlot,
        MojangMappingsParser? maps, out double inflate)
    {
        inflate = 0;
        if (addBoxInvokeLineIdx <= 0)
        {
            return false;
        }

        var boxLine = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, addBoxInvokeLineIdx);
        if (!ContainsCubeDeformationDescriptor(boxLine, maps))
        {
            return false;
        }

        var preInv = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, addBoxInvokeLineIdx - 1).TrimStart();
        if (Regex.IsMatch(preInv, @"^\d+:\s+aload_", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(preInv, @"^\d+:\s+aload\s+\d+", RegexOptions.CultureInvariant))
        {
            return JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[addBoxInvokeLineIdx - 1], out var slot) &&
                   cubeDeformationInflateByRefSlot.TryGetValue(slot, out inflate);
        }

        var winLow = Math.Max(minLineInclusive, addBoxInvokeLineIdx - 18);
        return TryReadUniformCubeDeformationCtorInvokespecialLine(seg, addBoxInvokeLineIdx - 1, winLow, maps, out inflate);
    }

    private static bool IsObfuscatedCubeDeformationGetStatic(string getStaticLine, MojangMappingsParser maps)
    {
        if (!getStaticLine.Contains("getstatic", StringComparison.Ordinal))
        {
            return false;
        }

        if (!maps.TryGetObfuscated("net.minecraft.client.model.geom.builders.CubeDeformation", out var obf))
        {
            return false;
        }

        var shortName = MojangMappingsParser.GetJavapClassArgForObfuscated(obf);
        return getStaticLine.Contains($"L{shortName};", StringComparison.Ordinal);
    }

    private static bool ContainsCubeDeformationDescriptor(string mergedInvokeLine, MojangMappingsParser? maps)
    {
        if (mergedInvokeLine.Contains("CubeDeformation", StringComparison.Ordinal))
        {
            return true;
        }

        if (maps is null)
        {
            return false;
        }

        if (!maps.TryGetObfuscated(
                "net.minecraft.client.model.geom.builders.CubeDeformation",
                out var obf))
        {
            return false;
        }

        var shortName = MojangMappingsParser.GetJavapClassArgForObfuscated(obf);
        return mergedInvokeLine.Contains($"L{shortName};", StringComparison.Ordinal);
    }
}
