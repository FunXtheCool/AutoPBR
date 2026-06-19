namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>
    /// Resolves simple <c>istore</c> constants and quadruped leg-scale ints propagated from
    /// <c>createBodyMesh</c> / <c>createLegs</c> call sites in concatenated mesh factories.
    /// </summary>
    private static Dictionary<int, int> BuildBoxIntLocalConstants(IReadOnlyList<string> lines)
    {
        var map = BuildBoxIntLocalConstantsFromSimpleIstores(lines);
        ApplyMeshFactoryBodySizeFromCallSites(lines, map);
        ApplyQuadrupedLegScaleFromCallSites(lines, map);
        return map;
    }

    internal static IReadOnlyDictionary<int, int> BuildBoxIntLocalConstantsForTests(IReadOnlyList<string> lines) =>
        BuildBoxIntLocalConstants(lines);

    /// <summary>
    /// Drops <c>MeshDefinition</c> construction and <c>getRoot</c> receiver setup. Some factories (FrogModel) then
    /// attach an empty <c>root</c> wrapper before real parts; that block poisons segment lift and is skipped through the
    /// first <c>CubeListBuilder.create</c>. Others (AdultAxolotlModel) ldc the first real part name before create on the
    /// mesh root — keep those lines so the body part id and cuboids are not lost.
    /// </summary>
    private static int FindMeshLiftPrologueSkip(List<string> lines)
    {
        var getRootLine = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsMeshRootGetRootInvokeLine(lines[i]))
            {
                continue;
            }

            getRootLine = i;
            break;
        }

        if (getRootLine < 0)
        {
            return 0;
        }

        var afterRootSetup = getRootLine + 1;
        if (TryFindFirstAstoreLocalSlotAfter(lines, getRootLine, 1, 10, out var astoreLine, out _))
        {
            afterRootSetup = astoreLine + 1;
        }

        var firstCreate = FindFirstCubeListBuilderCreateLine(lines, afterRootSetup);
        if (firstCreate < 0)
        {
            return afterRootSetup;
        }

        if (JavapBytecodeStreamAnalyzer.TryFindLdcStringBeforeLine(lines, firstCreate, out var firstPartName) &&
            string.Equals(firstPartName, "root", StringComparison.Ordinal))
        {
            return firstCreate;
        }

        return afterRootSetup;
    }

    private static int FindFirstCubeListBuilderCreateLine(List<string> lines, int startIdx)
    {
        for (var i = startIdx; i < lines.Count; i++)
        {
            if (lines[i].Contains("CubeListBuilder.create", StringComparison.Ordinal) ||
                IsObfuscatedCubeListBuilderCreateLine(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static Dictionary<int, int> MergeBoxIntLocalConstants(
        IReadOnlyDictionary<int, int> segmentLocals,
        Dictionary<int, int> meshWideLocals)
    {
        var merged = new Dictionary<int, int>(meshWideLocals);
        foreach (var (slot, value) in segmentLocals)
        {
            // createLegs islands seed slot 3 with the quadruped default (6); do not clobber a body height
            // propagated from createBodyMesh on another island (e.g. SheepModel passes 12).
            if (meshWideLocals.TryGetValue(slot, out var wide) && wide > 0 && value == 6 && wide != 6)
            {
                continue;
            }

            merged[slot] = value;
        }

        return merged;
    }

    private static Dictionary<int, double> MergeBoxFloatLocalConstants(
        IReadOnlyDictionary<int, double> meshWideLocals,
        IReadOnlyDictionary<int, double> segmentLocals)
    {
        var merged = new Dictionary<int, double>(meshWideLocals);
        foreach (var (slot, value) in segmentLocals)
        {
            merged[slot] = value;
        }

        return merged;
    }

    /// <summary>
    /// Propagates the first int argument of <c>createBodyMesh</c> from call sites (e.g. <c>bipush 6</c> in
    /// <c>createBodyLayer</c>) into int locals used by delegated <c>createLegs</c> helpers on other islands.
    /// </summary>
    private static void ApplyMeshFactoryBodySizeFromCallSites(IReadOnlyList<string> lines, Dictionary<int, int> map)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains("invokestatic", StringComparison.Ordinal) ||
                !line.Contains("createBodyMesh", StringComparison.Ordinal) ||
                !line.Contains("(I", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryExtractBackwardIntConstant(lines, i - 1, map, out var bodySize) && bodySize > 0)
            {
                map[0] = bodySize;
                map[3] = bodySize;
                continue;
            }

            // SheepModel and similar pass bipush height then iconst booleans before createBodyMesh(IZZ…).
            for (var j = i - 1; j >= Math.Max(0, i - 12); j--)
            {
                if (lines[j].Contains("bipush", StringComparison.Ordinal) &&
                    JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var wideIv) && wideIv > 0)
                {
                    map[0] = (int)Math.Round(wideIv);
                    map[3] = (int)Math.Round(wideIv);
                    break;
                }
            }
        }
    }

    private static Dictionary<int, int> BuildBoxIntLocalConstantsFromSimpleIstores(IReadOnlyList<string> lines)
    {
        var map = new Dictionary<int, int>();
        for (var i = 1; i < lines.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseIstoreLocalSlot(lines[i], out var slot))
            {
                continue;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[i - 1], out var iv))
            {
                map[slot] = (int)Math.Round(iv);
            }
        }

        return map;
    }

    private static void ApplyQuadrupedLegScaleFromCallSites(IReadOnlyList<string> lines, Dictionary<int, int> map)
    {
        ApplyVoidHelperLegScaleParamSlots(lines, map);
        int? bodyMeshScale = null;
        int? legsCallScale = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains("invokestatic", StringComparison.Ordinal))
            {
                continue;
            }

            var isCreateLegs = line.Contains("createLegs", StringComparison.Ordinal);
            var isCreateBodyMesh = line.Contains("createBodyMesh", StringComparison.Ordinal);
            // createLegs:(PartDefinition;ZZI…) — int height is not the first parameter, so "(I" does not appear in javap comments.
            if (!isCreateLegs && !isCreateBodyMesh && !line.Contains("(I", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains("LayerDefinition.create", StringComparison.Ordinal))
            {
                continue;
            }

            if (isCreateBodyMesh &&
                TryExtractBackwardIntConstant(lines, i - 1, map, out var sc) && sc > 0)
            {
                bodyMeshScale = sc;
            }

            if (isCreateLegs &&
                TryExtractBackwardIntConstant(lines, i - 1, map, out var legScale) && legScale > 0)
            {
                legsCallScale = legScale;
            }
        }

        // createLegs int param is the leg-box height source inside void helpers; prefer it over createBodyMesh
        // torso height when both appear in the same concat (order-independent).
        if (legsCallScale is > 0)
        {
            map[0] = legsCallScale.Value;
            map[3] = legsCallScale.Value;
        }
        else if (bodyMeshScale is > 0)
        {
            map[0] = bodyMeshScale.Value;
            map[3] = bodyMeshScale.Value;
        }
        else if ((lines.Any(static l => l.Contains("createLegs", StringComparison.Ordinal)) ||
                  lines.Any(static l => l.Contains("right_hind_leg", StringComparison.Ordinal) ||
                                        l.Contains("left_hind_leg", StringComparison.Ordinal))) &&
                 (!map.TryGetValue(3, out var existing) || existing <= 0))
        {
            // Adult quadruped body height when call-site bipush is on another island (e.g. createBodyLayer only),
            // or inside a void createLegs helper island with no upstream createBodyMesh call in the concat.
            map[3] = 6;
            if (!map.TryGetValue(0, out var slot0) || slot0 <= 0)
            {
                map[0] = 6;
            }
        }
    }

    /// <summary>
    /// Void helpers like <c>createLegs(PartDefinition,ZZI,CubeDeformation)</c> use <c>iload_3</c> for body height in
    /// <c>addBox</c> stacks; seed that slot when the island has no upstream <c>createBodyMesh</c> call in the same concat.
    /// </summary>
    private static void ApplyVoidHelperLegScaleParamSlots(IReadOnlyList<string> lines, Dictionary<int, int> map)
    {
        if (!lines.Any(static l => l.Contains("createLegs", StringComparison.Ordinal)))
        {
            return;
        }

        if (!map.TryGetValue(3, out var h) || h <= 0)
        {
            map[3] = 6;
        }

        if (!map.TryGetValue(0, out var slot0) || slot0 <= 0)
        {
            map[0] = map[3];
        }
    }

    private static bool TryExtractBackwardIntConstant(IReadOnlyList<string> lines, int startIdx,
        Dictionary<int, int> intLocals, out int value)
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

            if (lines[j].Contains("bipush", StringComparison.Ordinal) ||
                lines[j].Contains("sipush", StringComparison.Ordinal))
            {
                if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var wideIv))
                {
                    value = (int)Math.Round(wideIv);
                    return true;
                }
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIntLine(lines[j], out var iv))
            {
                value = (int)Math.Round(iv);
                return true;
            }

            if (JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(lines[j], out var slot) && intLocals.TryGetValue(slot, out var localIv))
            {
                value = localIv;
                return true;
            }

            break;
        }

        return false;
    }
}
