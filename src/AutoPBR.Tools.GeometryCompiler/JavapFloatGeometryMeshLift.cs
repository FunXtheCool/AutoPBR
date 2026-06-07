using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Lifts a flat part tree from Mojang 26.x-style <c>javap -c</c> for static mesh factories (<c>MeshDefinition</c> and
/// <c>LayerDefinition</c> static methods once concatenated): <c>CubeListBuilder.texOffs</c>,
/// <c>addBox(FFFFFF)</c> / extended <c>addBox(FFFFFFL…CubeDeformation;FF)</c> / <c>addBox(String,FFFFFF)</c>,
/// texCrop-style <c>addBox(String,FFFIII…)</c>, direction-mask overload via <c>java/util/Set</c> (full box approximation),
/// and <c>PartPose</c> before <c>PartDefinition.addOrReplaceChild</c>.
/// </summary>
internal static partial class JavapFloatGeometryMeshLift
{
    [ThreadStatic]
    private static MojangMappingsParser? _maps;

    [ThreadStatic]
    private static int _delegationDepth;
    private static int _invokeStaticPartNameBindingOrdinal;

    /// <summary>
    /// Max bytecode lines to scan backward from <c>addOrReplaceChild</c> for a trailing <c>ldc</c> part id
    /// (PartPose / float / int stack operands only — not across <c>addBox</c> or builder calls).
    /// Calibrated against the full geometry-compiler test suite (10 lines matches 24; 9 fails).
    /// </summary>
    private const int PartNameLdcImmediateLookbackLines = 10;

    public static bool TryLift(string meshFactoryJavap, out JsonArray roots, out List<string> notes,
        MojangMappingsParser? maps = null, int delegationDepth = 0,
        IReadOnlyDictionary<string, int[][]>? staticIntMatrices = null,
        IReadOnlyDictionary<string, float[]>? staticFloatArrays = null)
    {
        roots = new JsonArray();
        notes = new List<string>();
        try
        {
            _delegationDepth = delegationDepth;
            _maps = maps;
            _staticIntMatrices = staticIntMatrices;
            _staticFloatArrays = staticFloatArrays;
            if (string.IsNullOrWhiteSpace(meshFactoryJavap) ||
                !JavapMeshBytecodeProfiles.ContainsMeshSignals(meshFactoryJavap))
            {
                return false;
            }

            meshFactoryJavap = meshFactoryJavap.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            meshFactoryJavap = BytecodeMeshResolution.NormalizeMeshIslandBoundaries(meshFactoryJavap);

            JsonArray rootChildren;
        if (meshFactoryJavap.Contains(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker,
                StringComparison.Ordinal))
        {
            var islands = meshFactoryJavap
                .Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            var meshWideLines = JavapBytecodeStreamAnalyzer.FoldJavapWrappedBytecodeLines(
                meshFactoryJavap.Split('\n').Select(l => l.TrimEnd('\r')).ToList());
            rootChildren = new JsonArray();
            foreach (var t in islands)
            {
                if (t.Length == 0 || !JavapMeshBytecodeProfiles.ContainsMeshSignals(t))
                {
                    continue;
                }

                var useMeshWideScope = JavapLiftPartTreeBuilder.IslandUsesMeshWideConstantScope(t);
                var islandMeshWide = useMeshWideScope ? meshWideLines : null;
                var prevMatrices = _staticIntMatrices;
                var prevFloatArrays = _staticFloatArrays;
                if (!useMeshWideScope)
                {
                    _staticIntMatrices = null;
                    _staticFloatArrays = null;
                }

                try
                {
                    if (!JavapLiftPartTreeBuilder.TryCollectLiftedRootChildren(t, notes, out var islandKids, islandMeshWide) ||
                        islandKids.Count == 0)
                    {
                        continue;
                    }

                    foreach (var n in islandKids)
                    {
                        GeometryLiftJsonMerge.MergeRootChildLastWinsByPartId(rootChildren, n);
                    }
                }
                finally
                {
                    _staticIntMatrices = prevMatrices;
                    _staticFloatArrays = prevFloatArrays;
                }
            }

            GeometryLiftForestMerge.ApplyMultiIslandPostMerge(rootChildren);
        }
        else
        {
            if (!JavapLiftPartTreeBuilder.TryCollectLiftedRootChildren(meshFactoryJavap, notes, out rootChildren))
            {
                return false;
            }
        }

        if (rootChildren.Count == 0)
        {
            return false;
        }

            roots = GeometryLiftOutputAssembly.WrapSyntheticRoot(rootChildren);
            roots = GeometryIrLiftTreeRepair.Apply(roots, hoistStandardQuadrupedLegsToRoot: false);
            return true;
        }
        finally
        {
            _staticIntMatrices = null;
            _staticFloatArrays = null;
            _maps = null;
            _delegationDepth = 0;
        }
    }

    /// <summary>
    /// <c>javap -c</c> wraps long comments so <c>PartDefinition.addOrReplaceChild</c> can be split across two physical lines.
    /// Fold those continuations into the previous line so mesh binding / pose / addBox detection sees whole tokens.
    /// </summary>
    internal static List<string> FoldJavapWrappedBytecodeLinesForTests(List<string> lines) =>
        JavapBytecodeStreamAnalyzer.FoldJavapWrappedBytecodeLines(lines);

    internal static string MergeJavapCommentContinuationForTests(List<string> seg, int invokeLineIdx) =>
        JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, invokeLineIdx);

}
