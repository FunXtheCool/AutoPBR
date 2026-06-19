namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>
    /// Maps local slots that hold a <c>PartDefinition</c> receiver to the mesh part id they represent for nested
    /// <c>addOrReplaceChild</c> calls (humanoid, quadruped, aquatic, etc.). <c>null</c> means the mesh root from <c>getRoot</c>.
    /// </summary>
    /// <remarks>
    /// Concatenated <c>javap -c</c> blocks may reuse local indices across methods; callers that concatenate unrelated methods
    /// should split on <see cref="JavapClassDisassembly.GeometryMeshIslandBoundaryMarker"/> (or equivalent) so each slice
    /// gets a fresh map from this method.
    /// </remarks>
    private static Dictionary<int, ReceiverSlotEntry> BuildReceiverLocalSlotGraph(List<string> lines)
    {
        var map = new Dictionary<int, ReceiverSlotEntry>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (JavapBytecodeStreamAnalyzer.IsJavapMethodCodeHeaderLine(lines[i]))
            {
                map.Clear();
                continue;
            }

            if (IsMeshRootGetRootInvokeLine(lines[i]) &&
                TryFindFirstAstoreLocalSlotAfter(lines, i, 1, 10, out _, out var rootSlot))
            {
                map[rootSlot] = new ReceiverSlotEntry(null, null);
            }

            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[i]))
            {
                continue;
            }

            var head = lines.GetRange(0, i + 1);
            var child = FindPartNameNearAddOrReplaceChild(head, new Dictionary<int, int>()) ??
                        FindLdcPartNameImmediatelyBeforeBinding(lines, i);
            if (string.IsNullOrEmpty(child))
            {
                continue;
            }

            var parentReceiverSlot = TryInferReceiverLocalSlotForBinding(lines, i, child);
            if (TryFindFirstAstoreLocalSlotAfter(lines, i, 1, 256, out _, out var chainSlot))
            {
                map[chainSlot] = new ReceiverSlotEntry(child, parentReceiverSlot);
            }
        }

        return map;
    }

    /// <summary>
    /// Tracks <c>Reference</c>-typed locals holding <c>CubeDeformation</c> assigned from <c>new CubeDeformation(f)</c>,
    /// <c>getstatic … ZERO</c>, or copies <c>aload→astore</c>, so <c>aload</c> before <c>addBox(…CubeDeformation;)</c> can resolve inflate.
    /// </summary>
    private static Dictionary<int, double> BuildCubeDeformationInflateByRefLocalSlot(List<string> lines,
        MojangMappingsParser? maps)
    {
        var map = new Dictionary<int, double>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[i], out var slot))
            {
                continue;
            }

            var winLow = Math.Max(0, i - 24);
            var prev = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, i - 1);
            if (prev.Contains("getstatic", StringComparison.Ordinal) &&
                (prev.Contains("CubeDeformation", StringComparison.Ordinal) ||
                 (maps is not null && IsObfuscatedCubeDeformationGetStatic(prev, maps))) &&
                (prev.Contains("ZERO", StringComparison.Ordinal) || prev.Contains("NONE", StringComparison.Ordinal) ||
                 prev.Contains(".a:L", StringComparison.Ordinal)))
            {
                map[slot] = 0;
                continue;
            }

            if (TryReadUniformCubeDeformationCtorInvokespecialLine(lines, i - 1, winLow, maps, out var inf))
            {
                map[slot] = inf;
            }
        }

        for (var iter = 0; iter < 8; iter++)
        {
            for (var i = 0; i < lines.Count - 1; i++)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[i], out var a) || !JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[i + 1], out var b))
                {
                    continue;
                }

                if (map.TryGetValue(a, out var v))
                {
                    map[b] = v;
                }
            }
        }

        ApplyCubeDeformationExtendAssignments(lines, map, maps);

        return map;
    }

    /// <summary>
    /// <c>aload base; ldc delta; invokevirtual CubeDeformation.extend:(F)...; astore dest</c> → dest inflate = base + delta.
    /// </summary>
    private static void ApplyCubeDeformationExtendAssignments(List<string> lines, Dictionary<int, double> map,
        MojangMappingsParser? maps)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(lines[i], out var destSlot))
            {
                continue;
            }

            if (!TryResolveCubeDeformationExtendInflate(lines, i, map, maps, out var inflate))
            {
                continue;
            }

            map[destSlot] = inflate;
        }
    }

    private static bool TryResolveCubeDeformationExtendInflate(List<string> lines, int astoreLineIdx,
        Dictionary<int, double> map, MojangMappingsParser? maps, out double inflate)
    {
        inflate = 0;
        for (var d = 1; d <= 8 && astoreLineIdx - d >= 0; d++)
        {
            var ln = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(lines, astoreLineIdx - d);
            if (!IsCubeDeformationExtendInvokeLine(ln, maps))
            {
                continue;
            }

            if (!TryParseExtendDeltaBackward(lines, astoreLineIdx - d, out var delta))
            {
                return false;
            }

            for (var e = d + 1; e <= d + 6 && astoreLineIdx - e >= 0; e++)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[astoreLineIdx - e], out var baseSlot))
                {
                    continue;
                }

                if (!map.TryGetValue(baseSlot, out var baseInflate))
                {
                    return false;
                }

                inflate = baseInflate + delta;
                return true;
            }

            return false;
        }

        return false;
    }

    private static bool IsCubeDeformationExtendInvokeLine(string line, MojangMappingsParser? maps)
    {
        if (!line.Contains("invokevirtual", StringComparison.Ordinal) ||
            !line.Contains("extend", StringComparison.Ordinal))
        {
            return false;
        }

        if (line.Contains("CubeDeformation", StringComparison.Ordinal) &&
            line.Contains("(F)", StringComparison.Ordinal))
        {
            return true;
        }

        return maps is not null &&
               maps.TryGetObfuscated("net.minecraft.client.model.geom.builders.CubeDeformation", out var obf) &&
               JavapBytecodeStreamAnalyzer.ObfJavapLineReferencesShortType(line, MojangMappingsParser.GetJavapClassArgForObfuscated(obf)) &&
               line.Contains("extend", StringComparison.Ordinal);
    }

    private static bool TryParseExtendDeltaBackward(List<string> lines, int extendLineIdx, out double delta)
    {
        delta = 0;
        for (var d = 1; d <= 4 && extendLineIdx - d >= 0; d++)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(lines[extendLineIdx - d], out delta))
            {
                return true;
            }
        }

        return false;
    }
}
