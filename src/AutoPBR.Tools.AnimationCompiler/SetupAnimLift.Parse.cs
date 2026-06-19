using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
    private static bool TryLiftPartPeerCopy(
        List<string> lines,
        int putIdx,
        string property,
        out string targetPartField,
        out JsonObject peerExpr)
    {
        targetPartField = "";
        peerExpr = new JsonObject();
        if (property is not ("x" or "y" or "z" or "xRot" or "yRot" or "zRot"))
        {
            return false;
        }

        var propPut = ModelPartPutfieldRegex.Match(lines[putIdx]);
        if (!propPut.Success || !string.Equals(propPut.Groups[1].Value, property, StringComparison.Ordinal))
        {
            return false;
        }

        string? sourcePart = null;
        string? destPart = null;
        var sawPropertyRead = false;
        for (var j = putIdx - 1; j >= Math.Max(0, putIdx - 12); j--)
        {
            var line = lines[j];
            if (line.Contains($"ModelPart.{property}:", StringComparison.Ordinal) &&
                line.Contains("getfield", StringComparison.Ordinal))
            {
                sawPropertyRead = true;
                continue;
            }

            if (!sawPropertyRead ||
                !line.Contains("getfield", StringComparison.Ordinal) ||
                !line.Contains(":Lnet/minecraft/client/model/geom/ModelPart;", StringComparison.Ordinal))
            {
                continue;
            }

            var m = ModelPartFieldGetRegex.Match(line);
            if (!m.Success)
            {
                continue;
            }

            if (sourcePart is null)
            {
                sourcePart = m.Groups[1].Value;
                continue;
            }

            destPart = m.Groups[1].Value;
            break;
        }

        if (sourcePart is null || destPart is null || string.Equals(sourcePart, destPart, StringComparison.Ordinal))
        {
            return false;
        }

        targetPartField = destPart;
        peerExpr = SetupAnimExpressionLift.PartPeerNode(sourcePart, property);
        return true;
    }

    private static string? FindPartFieldForPutfield(List<string> lines, int putIdx)
    {
        for (var j = putIdx - 1; j >= Math.Max(0, putIdx - 40); j--)
        {
            var line = lines[j];
            if (line.Contains("getfield", StringComparison.Ordinal) &&
                line.Contains("net/minecraft/client/model/geom/ModelPart.", StringComparison.Ordinal) &&
                !line.Contains(":Lnet/minecraft/client/model/geom/ModelPart;", StringComparison.Ordinal))
            {
                continue;
            }

            if (!line.Contains("getfield", StringComparison.Ordinal) ||
                !line.Contains(":Lnet/minecraft/client/model/geom/ModelPart;", StringComparison.Ordinal))
            {
                continue;
            }

            var m = ModelPartFieldGetRegex.Match(line);
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }

        return null;
    }

    private static readonly Regex ModelPartArrayGetfieldRegex = new(
        @"getfield\s+#\d+\s+//\s+Field\s+(\w+):\[Lnet/minecraft/client/model/geom/ModelPart;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static void TryUnrollModelPartArrayLoops(
        List<string> lines,
        string officialJvmName,
        JsonArray assignments,
        List<string> notes,
        IReadOnlyDictionary<string, float> modelAccessors)
    {
        for (var scan = 0; scan < lines.Count; scan++)
        {
            var arrayMatch = ModelPartArrayGetfieldRegex.Match(lines[scan]);
            if (!arrayMatch.Success)
            {
                continue;
            }

            var arrayField = arrayMatch.Groups[1].Value;
            if (!TryResolveArrayPartPrefix(officialJvmName, arrayField, out var prefix, out var count))
            {
                continue;
            }

            if (!TryFindForLoopBody(lines, scan, out var bodyStart, out var bodyEnd, out var iloadPattern, out var indexStart, out var indexEndExclusive))
            {
                continue;
            }

            var puts = new List<(int Idx, string Prop)>();
            for (var i = bodyStart; i <= bodyEnd; i++)
            {
                if (!lines[i].Contains("putfield", StringComparison.Ordinal))
                {
                    continue;
                }

                var pm = ModelPartPutfieldRegex.Match(lines[i]);
                if (!pm.Success || !IsSupportedProperty(pm.Groups[1].Value))
                {
                    continue;
                }

                puts.Add((i, pm.Groups[1].Value));
            }

            var endExclusive = indexEndExclusive == int.MaxValue ? count : Math.Min(indexEndExclusive, count);
            foreach (var (putIdx, prop) in puts)
            {
                var slice = lines.GetRange(bodyStart, putIdx - bodyStart + 1);
                for (var elem = indexStart; elem < endExclusive; elem++)
                {
                    var synthetic = RewriteLoopIndexOps(slice, iloadPattern, elem);
                    if (!SetupAnimExpressionLift.TryLiftAssignmentExpr(
                            synthetic,
                            synthetic.Count - 1,
                            out var expr,
                            out var exprNotes,
                            modelAccessors))
                    {
                        notes.AddRange(exprNotes.Select(n => $"Array {arrayField}[{elem}] {prop}: {n}"));
                        continue;
                    }

                    assignments.Add(new JsonObject
                    {
                        ["partField"] = $"{prefix}{elem}",
                        ["property"] = prop,
                        ["expr"] = expr?.DeepClone()
                    });
                }

                notes.RemoveAll(n =>
                    n.Contains($"Could not resolve model part field for putfield at line {putIdx} ({prop})", StringComparison.Ordinal));
            }
        }
    }

    private static bool TryResolveArrayPartPrefix(
        string officialJvmName,
        string arrayField,
        out string prefix,
        out int count)
    {
        prefix = "";
        count = 0;
        switch (arrayField)
        {
            case "tentacles":
                prefix = "tentacle";
                count = 8;
                return true;
            case "bodyParts" when officialJvmName.Contains("silverfish", StringComparison.Ordinal):
                prefix = "segment";
                count = 7;
                return true;
            case "bodyParts":
                prefix = "segment";
                count = 4;
                return true;
            case "bodyCubes" when officialJvmName.Contains("MagmaCubeModel", StringComparison.Ordinal):
                prefix = "cube";
                count = 8;
                return true;
            case "bodyCubes":
                prefix = "segment";
                count = 8;
                return true;
            case "boxes":
                prefix = "box";
                count = 2;
                return true;
            case "bodyLayers":
                prefix = "layer";
                count = 3;
                return true;
            case "upperBodyParts":
                prefix = "part";
                count = 12;
                return true;
            default:
                return false;
        }
    }

    private static bool TryFindForLoopBody(
        List<string> lines,
        int arrayGetfieldIdx,
        out int bodyStart,
        out int bodyEnd,
        out string iloadPattern,
        out int indexStart,
        out int indexEndExclusive)
    {
        bodyStart = 0;
        bodyEnd = 0;
        iloadPattern = "";
        indexStart = 0;
        indexEndExclusive = 0;

        var loopVar = "";
        var foundIstore = false;
        var bestDist = int.MaxValue;
        var searchStart = Math.Max(0, arrayGetfieldIdx - 20);
        var searchEnd = Math.Min(lines.Count, arrayGetfieldIdx + 30);
        for (var j = searchStart; j < searchEnd; j++)
        {
            if (!lines[j].Contains("istore", StringComparison.Ordinal) || j == 0)
            {
                continue;
            }

            var prevIconst = TryParseIntConst(lines[j - 1]);
            if (prevIconst is null)
            {
                continue;
            }

            var slot = ParseIstoreSlot(lines[j]);
            if (string.IsNullOrEmpty(slot))
            {
                continue;
            }

            var dist = Math.Abs(j - arrayGetfieldIdx);
            if (!foundIstore || dist < bestDist)
            {
                loopVar = slot;
                indexStart = prevIconst.Value;
                bestDist = dist;
                foundIstore = true;
            }
        }

        if (!foundIstore)
        {
            loopVar = "";
        }

        if (string.IsNullOrEmpty(loopVar))
        {
            return false;
        }

        iloadPattern = loopVar.StartsWith('_')
            ? "iload" + loopVar
            : "iload         " + loopVar;

        var headerIdx = -1;
        for (var j = arrayGetfieldIdx; j < Math.Min(lines.Count, arrayGetfieldIdx + 35); j++)
        {
            if (!TryMatchLoopHeader(lines, j, iloadPattern, out var bound))
            {
                continue;
            }

            headerIdx = j;
            indexEndExclusive = bound ?? int.MaxValue;
            break;
        }

        if (headerIdx < 0)
        {
            for (var j = arrayGetfieldIdx - 1; j >= Math.Max(0, arrayGetfieldIdx - 12); j--)
            {
                if (!TryMatchLoopHeader(lines, j, iloadPattern, out var bound))
                {
                    continue;
                }

                headerIdx = j;
                indexEndExclusive = bound ?? int.MaxValue;
                break;
            }
        }

        if (headerIdx < 0)
        {
            return false;
        }

        static bool TryMatchLoopHeader(
            List<string> lines,
            int iloadIdx,
            string iloadPattern,
            out int? bound)
        {
            bound = null;
            if (!lines[iloadIdx].Contains(iloadPattern, StringComparison.Ordinal))
            {
                return false;
            }

            for (var k = iloadIdx + 1; k < Math.Min(lines.Count, iloadIdx + 6); k++)
            {
                if (!lines[k].Contains("if_icmpge", StringComparison.Ordinal))
                {
                    continue;
                }

                bound = TryParseIntConst(lines[k - 1]);
                return true;
            }

            return false;
        }

        bodyStart = headerIdx + 2;
        for (var j = bodyStart; j < lines.Count; j++)
        {
            if (lines[j].Contains("iinc", StringComparison.Ordinal) &&
                (lines[j].Contains(loopVar, StringComparison.Ordinal) || lines[j].Contains(iloadPattern, StringComparison.Ordinal)))
            {
                bodyEnd = j - 1;
                return true;
            }
        }

        return false;
    }

    private static string ParseIstoreSlot(string line)
    {
        if (line.TrimEnd().EndsWith("istore_2", StringComparison.Ordinal))
        {
            return "_2";
        }

        if (line.TrimEnd().EndsWith("istore_3", StringComparison.Ordinal))
        {
            return "_3";
        }

        var m = Regex.Match(line, @"istore\s+(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        return m.Success ? m.Groups[1].Value : "";
    }

    private static int? TryParseIntConst(string line)
    {
        if (line.TrimEnd().EndsWith("iconst_0", StringComparison.Ordinal))
        {
            return 0;
        }

        if (line.TrimEnd().EndsWith("iconst_1", StringComparison.Ordinal))
        {
            return 1;
        }

        if (line.TrimEnd().EndsWith("iconst_2", StringComparison.Ordinal))
        {
            return 2;
        }

        if (line.TrimEnd().EndsWith("iconst_3", StringComparison.Ordinal))
        {
            return 3;
        }

        if (line.TrimEnd().EndsWith("iconst_4", StringComparison.Ordinal))
        {
            return 4;
        }

        if (line.TrimEnd().EndsWith("iconst_8", StringComparison.Ordinal))
        {
            return 8;
        }

        var m = Regex.Match(line, @"bipush\s+(\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : null;
    }

    private static List<string> RewriteLoopIndexOps(List<string> lines, string iloadPattern, int index)
    {
        var result = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!line.Contains(iloadPattern, StringComparison.Ordinal))
            {
                result.Add(line);
                continue;
            }

            if (i + 3 < lines.Count &&
                TryParseIconstMultiplier(lines[i + 1], out var mul) &&
                lines[i + 2].TrimEnd().EndsWith("imul", StringComparison.Ordinal) &&
                lines[i + 3].TrimEnd().EndsWith("i2f", StringComparison.Ordinal))
            {
                result.Add($"      ldc           #0                  // float {index * mul}f");
                i += 3;
                continue;
            }

            if (i + 1 < lines.Count && lines[i + 1].TrimEnd().EndsWith("i2f", StringComparison.Ordinal))
            {
                result.Add($"      ldc           #0                  // float {index}f");
                i += 1;
                continue;
            }

            result.Add($"      ldc           #0                  // float {index}f");
        }

        return result;
    }

    private static List<string> MergeInstructionContinuation(List<string> lines)
    {
        var merged = new List<string>();
        foreach (var line in lines)
        {
            if (merged.Count > 0 && !InstructionStartRegex.IsMatch(line) && InstructionStartRegex.IsMatch(merged[^1]))
            {
                merged[^1] = merged[^1] + line.TrimStart();
            }
            else
            {
                merged.Add(line);
            }
        }

        return merged;
    }
}
