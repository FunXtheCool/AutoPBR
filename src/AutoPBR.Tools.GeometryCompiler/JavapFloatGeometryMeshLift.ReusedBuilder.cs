namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>
    /// Mojang reuses mirrored leg builders (<c>aload_2</c> / <c>aload_3</c>) without <c>addBox</c> in the binding slice;
    /// extend the slice backward to the matching <c>astore</c> template block.
    /// </summary>
    private static bool TryExpandSliceForReusedCubeListBuilder(List<string> lines, int bindingLineIdx, ref int sliceStart)
    {
        if (!TryFindAloadLocalSlotBeforeBinding(lines, bindingLineIdx, out var builderSlot))
        {
            return false;
        }

        if (!TryFindLastAstoreForLocalSlot(lines, bindingLineIdx, builderSlot, out var astoreLine))
        {
            return false;
        }

        var templateStart = astoreLine;
        for (var j = astoreLine - 1; j >= 0; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[j], out _))
            {
                templateStart = j + 1;
                break;
            }

            if (lines[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(lines[j]))
            {
                templateStart = j;
                break;
            }
        }

        if (templateStart >= sliceStart)
        {
            return false;
        }

        sliceStart = templateStart;
        return true;
    }

    private static bool TryFindAloadLocalSlotBeforeBinding(List<string> lines, int bindingLineIdx, out int slot)
    {
        slot = 0;
        for (var j = bindingLineIdx - 1; j >= 0 && j > bindingLineIdx - 16; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[j], out slot))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Detects quadruped leg-style reuse: <c>ldc</c> part name, <c>aload</c> stored fluent builder, no
    /// <c>CubeListBuilder.create</c> between that <c>aload</c> and <c>addOrReplaceChild</c>.
    /// Skips <c>aload</c> of <c>CubeDeformation</c> operands immediately before masked <c>addBox</c> calls.
    /// </summary>
    private static bool TryFindReusedBuilderAloadBeforeBinding(List<string> seg, int bindingIdx, out int builderSlot,
        out int astoreLine)
    {
        builderSlot = 0;
        astoreLine = 0;
        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 16; j--)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out builderSlot))
            {
                continue;
            }

            if (IsPartDefinitionReceiverAloadBeforeBinding(seg, j, bindingIdx))
            {
                continue;
            }

            if (SegmentHasCubeListBuilderCreateAfterAload(seg, builderSlot, bindingIdx))
            {
                continue;
            }

            var hasLdcNameBeforeAload = false;
            for (var k = j - 1; k >= 0 && k > j - 5; k--)
            {
                if (JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]).Success)
                {
                    hasLdcNameBeforeAload = true;
                    break;
                }
            }

            if (!hasLdcNameBeforeAload)
            {
                continue;
            }

            if (!TryFindLastAstoreForLocalSlot(seg, bindingIdx, builderSlot, out astoreLine))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// True for <c>aload_0</c>-style mesh receivers (<c>aload</c> then immediate <c>ldc</c> part name), not fluent builders.
    /// </summary>
    private static bool IsQuadrupedLegPartName(string partName) =>
        partName.Contains("leg", StringComparison.Ordinal);

    private static bool IsPartDefinitionReceiverAloadBeforeBinding(List<string> seg, int aloadLineIdx, int bindingIdx)
    {
        for (var k = aloadLineIdx + 1; k < bindingIdx && k < aloadLineIdx + 4; k++)
        {
            if (JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, k).Contains("PartPose", StringComparison.Ordinal))
            {
                return false;
            }

            if (JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]).Success)
            {
                return true;
            }
        }

        return false;
    }

    private static void FilterAddBoxIndicesForReusedBuilderTemplate(List<string> seg, List<int> addBoxIndices)
    {
        var bindingIdx = seg.Count - 1;
        if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[bindingIdx]))
        {
            for (var i = seg.Count - 1; i >= 0; i--)
            {
                if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
                {
                    bindingIdx = i;
                    break;
                }
            }
        }

        if (!TryFindReusedBuilderAloadBeforeBinding(seg, bindingIdx, out var builderSlot, out _))
        {
            return;
        }

        if (addBoxIndices.Count <= 1)
        {
            return;
        }

        var templateAstoreSlots = new HashSet<int>();
        foreach (var boxLine in addBoxIndices)
        {
            if (TryGetAstoreSlotAfterAddBox(seg, boxLine, out var astoreSlot))
            {
                templateAstoreSlots.Add(astoreSlot);
            }
        }

        if (templateAstoreSlots.Count <= 1)
        {
            return;
        }

        addBoxIndices.RemoveAll(i => !AddBoxBelongsToBuilderLocalSlot(seg, i, builderSlot));
    }

    private static bool TryGetAstoreSlotAfterAddBox(List<string> seg, int addBoxLine, out int astoreSlot)
    {
        astoreSlot = 0;
        for (var j = addBoxLine + 1; j < seg.Count && j <= addBoxLine + 32; j++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(seg[j], out astoreSlot))
            {
                return true;
            }

            if (seg[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(seg[j]))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Cow/quadruped legs store mirrored templates in <c>astore_2</c>/<c>astore_3</c>; a binding slice can contain both templates.
    /// </summary>
    private static bool AddBoxBelongsToBuilderLocalSlot(List<string> seg, int addBoxLine, int builderSlot) =>
        TryGetAstoreSlotAfterAddBox(seg, addBoxLine, out var astoreSlot) && astoreSlot == builderSlot;

    private static bool TryFindLastAstoreForLocalSlot(List<string> lines, int beforeLineIdx, int slot, out int astoreLine)
    {
        astoreLine = 0;
        for (var j = beforeLineIdx - 1; j >= 0; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[j], out var s) && s == slot)
            {
                astoreLine = j;
                return true;
            }
        }

        return false;
    }

    private static bool SegmentHasCubeListBuilderCreateAfterAload(List<string> seg, int aloadSlot, int bindingIdx)
    {
        var aloadLine = -1;
        for (var j = bindingIdx - 1; j >= 0; j--)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out var s) && s == aloadSlot)
            {
                aloadLine = j;
                break;
            }
        }

        if (aloadLine < 0)
        {
            return false;
        }

        for (var j = aloadLine + 1; j < bindingIdx; j++)
        {
            if (seg[j].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(seg[j]))
            {
                return true;
            }
        }

        return false;
    }

}
