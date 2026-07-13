using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>Named or obfuscated <c>getRoot</c> returning a part tree root.</summary>
    private static bool IsMeshRootGetRootInvokeLine(string line)
    {
        if (!line.Contains("getRoot:", StringComparison.Ordinal))
        {
            return false;
        }

        // Named jars: MeshDefinition / PartDefinition appear in the javap comment. Avoid a bare "invokevirtual" match —
        // it can correlate with unrelated callsites in the same method and corrupt local-slot tracking.
        return line.Contains("MeshDefinition", StringComparison.Ordinal) ||
               line.Contains("PartDefinition", StringComparison.Ordinal) ||
               line.Contains("/PartDefinition;", StringComparison.Ordinal);
    }

    /// <summary>Skips blank lines and <c>pop</c> between a key instruction and the first <c>astore</c>.</summary>
    private static bool TryFindFirstAstoreLocalSlotAfter(List<string> lines, int startIdx, int minOffset,
        int maxOffset, out int astoreLineIdx, out int slot)
    {
        astoreLineIdx = -1;
        slot = 0;
        for (var d = minOffset; d <= maxOffset && startIdx + d < lines.Count; d++)
        {
            var ln = lines[startIdx + d];
            if (string.IsNullOrWhiteSpace(ln))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.IsJavapMethodCodeHeaderLine(ln))
            {
                return false;
            }

            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(ln))
            {
                return false;
            }

            if (JavapBytecodeStreamAnalyzer.IsJavapPopLine(ln))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseAstoreLocalSlot(ln, out slot))
            {
                astoreLineIdx = startIdx + d;
                return true;
            }
        }

        return false;
    }

    private static string? FindLdcPartNameImmediatelyBeforeBinding(List<string> lines, int bindingIdx)
    {
        if (bindingIdx < 0 || bindingIdx >= lines.Count ||
            !JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[bindingIdx]))
        {
            return null;
        }

        return TryResolvePartNameForBinding(lines, bindingIdx, new Dictionary<int, int>(), out var name) ? name : null;
    }

    private static int? TryInferReceiverLocalSlotForBinding(
        List<string> lines,
        int bindingIdx,
        string partName,
        IReadOnlyDictionary<int, ReceiverSlotEntry>? knownReceiverSlots = null)
    {
        if (knownReceiverSlots is { Count: > 0 })
        {
            var segmentStart = 0;
            for (var i = bindingIdx - 1; i >= 0; i--)
            {
                if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[i]))
                {
                    continue;
                }

                segmentStart = i + 1;
                break;
            }

            // The PartDefinition receiver is the first known part local pushed for this binding. This also
            // covers computed names such as PartNames.tentacle(i), where no ldc String follows the receiver.
            for (var i = segmentStart; i < bindingIdx; i++)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[i], out var slot) &&
                    knownReceiverSlots.ContainsKey(slot))
                {
                    return slot;
                }
            }
        }

        for (var j = bindingIdx - 1; j >= 0 && j > bindingIdx - 16; j--)
        {
            var sm = JavapBytecodeStreamAnalyzer.MatchLdcString(lines[j]);
            if (!sm.Success || !string.Equals(sm.Groups[1].Value, partName, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryFindPartDefinitionReceiverAloadLineBeforeIndex(lines, j, bindingIdx, out var recvLine) &&
                JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[recvLine], out var loc))
            {
                return loc;
            }

            return null;
        }

        if (TryFindPartDefinitionReceiverAloadLineBeforeIndex(lines, bindingIdx, bindingIdx, out var fallbackLine) &&
            JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[fallbackLine], out var fallback))
        {
            return fallback;
        }

        return null;
    }

    private static bool TryFindPartDefinitionReceiverAloadLineBeforeIndex(List<string> lines, int beforeIdx,
        int bindingIdx, out int aloadLineIdx)
    {
        aloadLineIdx = 0;
        var start = Math.Max(0, beforeIdx - 40);
        for (var j = beforeIdx - 1; j >= start; j--)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(lines[j], out _))
            {
                continue;
            }

            if (!IsPartDefinitionReceiverAloadBeforeBinding(lines, j, bindingIdx))
            {
                continue;
            }

            aloadLineIdx = j;
            return true;
        }

        return false;
    }

    private static void AttachLiftedPartToForest(JsonArray rootForest,
        List<string> lines, int bindingLineIdx, JsonObject partNode,
        string partName, List<PendingPartAttach> pendingAttaches)
    {
        var graphAtBind = BuildReceiverLocalSlotGraph(lines.GetRange(0, Math.Min(bindingLineIdx + 1, lines.Count)));
        var recv = TryInferReceiverLocalSlotForBinding(lines, bindingLineIdx, partName, graphAtBind);
        var parentPartId = recv is { } r
            ? ResolveParentPartIdFromReceiverSlot(r, graphAtBind)
            : null;

        var keepFlatRootLeg = IsTinyBabyQuadrupedLeg(partName, partNode) ||
            IsGenericQuadrupedHelperLegContext(lines, partName);
        if (keepFlatRootLeg && IsStandardQuadrupedLegPartName(partName))
        {
            parentPartId = null;
        }

        var standardLegParent = ShouldAttachStandardQuadrupedLegUnderBody(lines, partName, partNode)
            ? "body"
            : null;
        if (string.IsNullOrEmpty(parentPartId) &&
            !keepFlatRootLeg &&
            GeometryLiftForestMerge.KnownNestedChildToParent.TryGetValue(partName, out var knownParent) &&
            GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, knownParent, out _))
        {
            parentPartId = knownParent;
        }
        else if (string.IsNullOrEmpty(parentPartId) &&
                 standardLegParent is not null &&
                 GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, standardLegParent, out _))
        {
            parentPartId = standardLegParent;
        }
        else if (string.IsNullOrEmpty(parentPartId) &&
                 !keepFlatRootLeg &&
                 standardLegParent is not null &&
                 HasFuturePartBinding(lines, bindingLineIdx, standardLegParent))
        {
            pendingAttaches.Add(new PendingPartAttach(standardLegParent, partNode, partName));
            return;
        }

        if (string.IsNullOrEmpty(parentPartId))
        {
            rootForest.Add(partNode);
            return;
        }

        if (!GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, parentPartId, out var parentObj) ||
            parentObj is null)
        {
            pendingAttaches.Add(new PendingPartAttach(parentPartId, partNode, partName));
            return;
        }

        GeometryLiftForestMerge.AttachPartUnderParent(parentObj, partNode, partName);
    }

    private static bool IsStandardQuadrupedLegPartName(string partName) =>
        partName is "right_hind_leg" or "left_hind_leg" or "right_front_leg" or "left_front_leg";

    private static bool HasFuturePartBinding(List<string> lines, int bindingLineIdx, string partName)
    {
        for (var i = bindingLineIdx + 1; i < lines.Count; i++)
        {
            if (!JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(lines[i]))
            {
                continue;
            }

            var head = lines.GetRange(0, i + 1);
            var child = FindPartNameNearAddOrReplaceChild(head, new Dictionary<int, int>()) ??
                        FindLdcPartNameImmediatelyBeforeBinding(lines, i);
            if (string.Equals(child, partName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGenericQuadrupedHelperLegContext(List<string> lines, string partName) =>
        IsStandardQuadrupedLegPartName(partName) &&
        lines.Any(static l =>
            l.Contains("QuadrupedModel.createBodyMesh", StringComparison.Ordinal) ||
            l.Contains("QuadrupedModel.createLegs", StringComparison.Ordinal));

    private static bool ShouldAttachStandardQuadrupedLegUnderBody(List<string> lines, string partName,
        JsonObject partNode) =>
        IsStandardQuadrupedLegPartName(partName) &&
        !IsTinyBabyQuadrupedLeg(partName, partNode) &&
        !lines.Any(static l => l.Contains("QuadrupedModel.createLegs", StringComparison.Ordinal));

    private static bool IsTinyBabyQuadrupedLeg(string partName, JsonObject partNode)
    {
        if (!IsStandardQuadrupedLegPartName(partName) ||
            partNode["cuboids"] is not JsonArray { Count: > 0 } cuboids ||
            cuboids[0] is not JsonObject cuboid ||
            cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count < 2 ||
            to.Count < 2)
        {
            return false;
        }

        var height = to[1]?.GetValue<double>() - from[1]?.GetValue<double>();
        return height is >= 0 and <= 3.0;
    }

    /// <summary>
    /// The <c>aload</c> receiver local for <c>addOrReplaceChild</c> holds the parent <c>PartDefinition</c>;
    /// return that slot's mesh part id (null for <c>getRoot()</c>).
    /// </summary>
    private static string? ResolveParentPartIdFromReceiverSlot(int receiverSlot,
        Dictionary<int, ReceiverSlotEntry> receiverSlotGraph)
    {
        if (!receiverSlotGraph.TryGetValue(receiverSlot, out var entry))
        {
            return null;
        }

        return entry.PartId;
    }

    private static void FlushPendingPartAttaches(JsonArray rootForest, List<PendingPartAttach> pending,
        bool hoistOrphansToRoot = false)
    {
        var progress = true;
        while (progress && pending.Count > 0)
        {
            progress = false;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var p = pending[i];
                if (!GeometryLiftForestMerge.TryFindPartObjectByIdInForest(rootForest, p.ParentPartId, out var parentObj) ||
                    parentObj is null)
                {
                    continue;
                }

                GeometryLiftForestMerge.AttachPartUnderParent(parentObj, p.PartNode, p.PartName);
                pending.RemoveAt(i);
                progress = true;
            }
        }

        if (!hoistOrphansToRoot)
        {
            return;
        }

        foreach (var p in pending)
        {
            rootForest.Add(p.PartNode);
        }

        pending.Clear();
    }
}
