using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal enum DirectionMaskParseResult
{
    NotDirectionMask,
    EmptySet,
    ParsedFaces,
    UnparsedSet
}

internal static partial class JavapFloatGeometryMeshLift
{
    private static readonly string[] VanillaDirectionConstants =
    [
        "DOWN", "UP", "NORTH", "SOUTH", "WEST", "EAST"
    ];

    [GeneratedRegex(
        @"getstatic\s+#\d+\s+//\s*Field\s+[\w$./]*Direction\.(\w+):",
        RegexOptions.CultureInvariant)]
    private static partial Regex DirectionGetStaticRegex();

    [GeneratedRegex(
        @"invokestatic\s+#\d+\s+//\s*(?:InterfaceMethod\s+)?(?:java/util/)?Set\.of:",
        RegexOptions.CultureInvariant)]
    private static partial Regex SetOfInvokeRegex();

    [GeneratedRegex(
        @"invokestatic\s+#\d+\s+//\s*Method\s+java/util/EnumSet\.of:",
        RegexOptions.CultureInvariant)]
    private static partial Regex EnumSetOfInvokeRegex();

    [GeneratedRegex(
        @"invokestatic\s+#\d+\s+//\s*Method\s+java/util/Collections\.emptySet:",
        RegexOptions.CultureInvariant)]
    private static partial Regex CollectionsEmptySetRegex();

    [GeneratedRegex(
        @"invokestatic\s+#\d+\s+//\s*Method\s+[\w$./]*Util\.allOfEnumExcept:",
        RegexOptions.CultureInvariant)]
    private static partial Regex UtilAllOfEnumExceptRegex();

    private static DirectionMaskParseResult TryParseDirectionFaceMaskBackward(
        List<string> seg,
        int startIdx,
        int minIdx,
        MojangMappingsParser? maps,
        out List<string>? faceMask)
    {
        faceMask = null;
        var allExceptInSegment = TryParseAllOfEnumExceptMaskBackward(seg, startIdx, minIdx, maps, out faceMask);
        if (allExceptInSegment.HasValue)
        {
            return allExceptInSegment.Value;
        }

        faceMask = null;
        var j = startIdx;
        while (j >= minIdx && string.IsNullOrWhiteSpace(seg[j]))
        {
            j--;
        }

        if (j < minIdx)
        {
            return DirectionMaskParseResult.NotDirectionMask;
        }

        var line = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
        if (CollectionsEmptySetRegex().IsMatch(line))
        {
            faceMask = [];
            return DirectionMaskParseResult.EmptySet;
        }

        if (UtilAllOfEnumExceptRegex().IsMatch(line) ||
            line.Contains("Util.allOfEnumExcept", StringComparison.Ordinal))
        {
            for (var k = j - 1; k >= minIdx; k--)
            {
                if (TryParseDirectionConstantLine(JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, k), maps, out var excluded))
                {
                    faceMask = AllDirectionFacesExcept(excluded);
                    return faceMask.Count == 0
                        ? DirectionMaskParseResult.EmptySet
                        : DirectionMaskParseResult.ParsedFaces;
                }
            }

            return DirectionMaskParseResult.UnparsedSet;
        }

        if (!SetOfInvokeRegex().IsMatch(line) &&
            !EnumSetOfInvokeRegex().IsMatch(line) &&
            !line.Contains("java/util/Set.of", StringComparison.Ordinal) &&
            !line.Contains("java/util/EnumSet.of", StringComparison.Ordinal))
        {
            var allExcept = TryParseAllOfEnumExceptMaskBackward(seg, startIdx, minIdx, maps, out faceMask);
            return allExcept ?? DirectionMaskParseResult.UnparsedSet;
        }

        var faces = new List<string>();
        j--;
        while (j >= minIdx)
        {
            var scan = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, j);
            if (SetOfInvokeRegex().IsMatch(scan) ||
                EnumSetOfInvokeRegex().IsMatch(scan) ||
                scan.Contains("java/util/Set.of", StringComparison.Ordinal) ||
                scan.Contains("java/util/EnumSet.of", StringComparison.Ordinal))
            {
                break;
            }

            if (TryParseDirectionConstantLine(scan, maps, out var faceName))
            {
                faces.Add(faceName);
                j--;
                continue;
            }

            if (Regex.IsMatch(scan.TrimStart(), @"^\d+:\s+(dup|swap|nop|pop)\b", RegexOptions.CultureInvariant))
            {
                j--;
                continue;
            }

            break;
        }

        if (faces.Count == 0)
        {
            var allExcept = TryParseAllOfEnumExceptMaskBackward(seg, startIdx, minIdx, maps, out faceMask);
            return allExcept ?? DirectionMaskParseResult.UnparsedSet;
        }

        faceMask = faces;
        return DirectionMaskParseResult.ParsedFaces;
    }

    private static DirectionMaskParseResult? TryParseAllOfEnumExceptMaskBackward(
        List<string> seg,
        int startIdx,
        int minIdx,
        MojangMappingsParser? maps,
        out List<string>? faceMask)
    {
        faceMask = null;
        for (var k = startIdx; k >= minIdx; k--)
        {
            var scan = JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, k);
            if (!UtilAllOfEnumExceptRegex().IsMatch(scan) &&
                !scan.Contains("Util.allOfEnumExcept", StringComparison.Ordinal))
            {
                continue;
            }

            for (var d = k - 1; d >= minIdx && d > k - 48; d--)
            {
                if (!TryParseDirectionConstantLine(JavapBytecodeStreamAnalyzer.MergeJavapCommentContinuation(seg, d), maps, out var excluded))
                {
                    continue;
                }

                faceMask = AllDirectionFacesExcept(excluded);
                return faceMask.Count == 0
                    ? DirectionMaskParseResult.EmptySet
                    : DirectionMaskParseResult.ParsedFaces;
            }
        }

        return null;
    }

    private static bool TryParseDirectionConstantLine(string line, MojangMappingsParser? _, out string faceName)
    {
        faceName = "";
        var m = DirectionGetStaticRegex().Match(line);
        if (!m.Success)
        {
            return false;
        }

        var constName = m.Groups[1].Value;
        if (!TryDirectionConstantToFace(constName, out faceName))
        {
            return false;
        }

        return true;
    }

    private static bool TryDirectionConstantToFace(string constName, out string faceName)
    {
        faceName = constName.ToUpperInvariant() switch
        {
            "NORTH" => "north",
            "SOUTH" => "south",
            "EAST" => "east",
            "WEST" => "west",
            "UP" => "up",
            "DOWN" => "down",
            _ => ""
        };
        return faceName.Length > 0;
    }

    internal static bool TryParseDirectionConstantLineForTests(string line, out string faceName) =>
        TryParseDirectionConstantLine(line, null, out faceName);

    internal static DirectionMaskParseResult TryParseDirectionFaceMaskBackwardForTests(
        List<string> seg,
        int addBoxLineIdx,
        int prevAddBoxLineIdx,
        out List<string>? faceMask) =>
        TryParseDirectionFaceMaskForAddBox(seg, addBoxLineIdx, prevAddBoxLineIdx, meshWideLines: null, maps: null,
            out faceMask);

    internal static DirectionMaskParseResult TryParseDirectionFaceMaskForAddBoxForTests(
        List<string> seg,
        int addBoxLineIdx,
        int prevAddBoxLineIdx,
        IReadOnlyList<string>? meshWideLines,
        out List<string>? faceMask) =>
        TryParseDirectionFaceMaskForAddBox(seg, addBoxLineIdx, prevAddBoxLineIdx, meshWideLines, maps: null,
            out faceMask);

    private static DirectionMaskParseResult TryParseDirectionFaceMaskForAddBox(
        List<string> seg,
        int addBoxLineIdx,
        int prevAddBoxLineIdx,
        IReadOnlyList<string>? meshWideLines,
        MojangMappingsParser? maps,
        out List<string>? faceMask)
    {
        var minIdx = prevAddBoxLineIdx >= 0 ? prevAddBoxLineIdx + 1 : 0;
        if (meshWideLines is { Count: > 0 })
        {
            var globalIdx = FindMeshWideLineIndex(meshWideLines, seg[addBoxLineIdx]);
            if (globalIdx >= 0)
            {
                var wideSeg = meshWideLines as List<string> ?? meshWideLines.ToList();
                var wideResult = TryParseDirectionFaceMaskBackward(wideSeg, globalIdx - 1, minIdx: 0, maps, out faceMask);
                if (wideResult is DirectionMaskParseResult.ParsedFaces or DirectionMaskParseResult.EmptySet)
                {
                    return wideResult;
                }
            }
        }

        return TryParseDirectionFaceMaskBackward(seg, addBoxLineIdx - 1, minIdx, maps, out faceMask);
    }

    internal static int FindMeshWideLineIndexForTests(IReadOnlyList<string> meshWideLines, string islandLine) =>
        FindMeshWideLineIndex(meshWideLines, islandLine);

    private static int FindMeshWideLineIndex(IReadOnlyList<string> meshWideLines, string islandLine)
    {
        var needle = islandLine.Trim();
        for (var i = meshWideLines.Count - 1; i >= 0; i--)
        {
            if (string.Equals(meshWideLines[i].Trim(), needle, StringComparison.Ordinal))
            {
                return i;
            }
        }

        if (!TryExtractBytecodeInsnSuffix(needle, out var insn))
        {
            return -1;
        }

        if (JavapBytecodeStreamAnalyzer.TryParseLineBytecodeOffset(needle, out var islandOffset))
        {
            for (var i = 0; i < meshWideLines.Count; i++)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseLineBytecodeOffset(meshWideLines[i], out var wideOffset) &&
                    wideOffset == islandOffset &&
                    TryExtractBytecodeInsnSuffix(meshWideLines[i].Trim(), out var wideInsn) &&
                    string.Equals(wideInsn, insn, StringComparison.Ordinal))
                {
                    return i;
                }
            }
        }

        for (var i = meshWideLines.Count - 1; i >= 0; i--)
        {
            if (TryExtractBytecodeInsnSuffix(meshWideLines[i].Trim(), out var wideInsn) &&
                string.Equals(wideInsn, insn, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryExtractBytecodeInsnSuffix(string line, out string insn)
    {
        insn = "";
        var colon = line.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        insn = line[(colon + 1)..].Trim();
        return insn.Length > 0;
    }

    private static List<string> AllDirectionFacesExcept(string excludedFace)
    {
        var list = new List<string>();
        foreach (var name in VanillaDirectionConstants)
        {
            if (TryDirectionConstantToFace(name, out var face) &&
                face.Length > 0 &&
                !string.Equals(face, excludedFace, StringComparison.Ordinal))
            {
                list.Add(face);
            }
        }

        return list;
    }
}
