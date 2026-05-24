using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

/// <summary>
/// Lifts <c>static {}</c> / <c>&lt;clinit&gt;</c> from Mojang-style <c>javap -c</c> for <c>*Animation</c> definition classes
/// into JSON-friendly <see cref="JsonObject"/> trees (named 26.1.2 bytecode).
/// </summary>
internal static class AnimationClinitLift
{
    private static readonly Regex InstructionStartRegex = new(@"^\s+\d+:", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex AnimationDefinitionFieldRegex = new(
        @"public\s+static\s+final\s+.*?AnimationDefinition\s+(\w+)\s*;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex PutstaticAnimationDefinitionRegex = new(
        @"putstatic\s+#\d+\s+//\s+Field\s+(\w+):Lnet/minecraft/client/animation/AnimationDefinition;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex LdcStringRegex = new(@"//\s*String\s+(\S+)", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex LdcFloatRegex = new(
        @"//\s*float\s+(-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)f",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));
    private static readonly Regex LdcDoubleRegex = new(
        @"//\s*double\s+(-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)d",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));
    private static readonly Regex TargetsRegex = new(@"Targets\.(\w+):", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex InterpolationsRegex = new(@"Interpolations\.(\w+):", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static bool TryLift(string javapStdout, out JsonArray definitions, out List<string> notes)
    {
        definitions = [];
        notes = [];
        var clinit = ExtractStaticInitializerCode(javapStdout);
        if (string.IsNullOrEmpty(clinit))
        {
            notes.Add("No static initializer (static {} or <clinit>) Code block found in javap output.");
            return false;
        }

        var declaredFields = AnimationDefinitionFieldRegex.Matches(javapStdout).Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal).ToList();
        var putMatches = new List<Match>();
        foreach (Match m in PutstaticAnimationDefinitionRegex.Matches(clinit))
        {
            putMatches.Add(m);
        }

        if (putMatches.Count == 0)
        {
            notes.Add("No putstatic assignments to AnimationDefinition fields in <clinit> bytecode.");
            return false;
        }

        if (declaredFields.Count > 0 && putMatches.Count != declaredFields.Count)
        {
            notes.Add(
                $"putstatic count ({putMatches.Count}) differs from declared AnimationDefinition field count ({declaredFields.Count}); extraction may be incomplete.");
        }

        for (var pi = 0; pi < putMatches.Count; pi++)
        {
            var start = pi == 0 ? 0 : putMatches[pi - 1].Index + putMatches[pi - 1].Length;
            var end = putMatches[pi].Index + putMatches[pi].Length;
            var segment = clinit[start..end];
            var fieldName = putMatches[pi].Groups[1].Value;
            var def = ParseDefinitionSegment(segment, fieldName, notes);
            definitions.Add(def);
        }

        return definitions.Count > 0;
    }

    public static string? ExtractStaticInitializerCode(string javapStdout)
    {
        var idx = javapStdout.IndexOf("  static {};", StringComparison.Ordinal);
        if (idx < 0)
        {
            idx = javapStdout.IndexOf("<clinit>()", StringComparison.Ordinal);
        }

        if (idx < 0)
        {
            return null;
        }

        return ExtractCodeBlockAfterDeclarationIndex(javapStdout, idx);
    }

    private static string? ExtractCodeBlockAfterDeclarationIndex(string javapC, int declarationIdx)
    {
        var codeMark = javapC.IndexOf("    Code:", declarationIdx, StringComparison.Ordinal);
        if (codeMark < 0)
        {
            return null;
        }

        var after = codeMark + "    Code:".Length;
        var end = javapC.IndexOf("\n    }", after, StringComparison.Ordinal);
        return end < 0 ? javapC[codeMark..] : javapC[codeMark..end];
    }

    private static JsonObject ParseDefinitionSegment(string segment, string fieldName, List<string> notes)
    {
        var lines = MergeInstructionContinuation(segment.Split('\n').Select(l => l.TrimEnd('\r')).ToList());
        var def = new JsonObject
        {
            ["fieldName"] = fieldName,
            ["channels"] = new JsonArray()
        };
        if (TryExtractWithLengthSeconds(lines, out var lenSeconds))
        {
            def["lengthSeconds"] = lenSeconds;
        }
        else
        {
            notes.Add($"Definition {fieldName}: could not resolve AnimationDefinition.Builder.withLength leading float.");
        }

        var addIdx = new List<int>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains("addAnimation:(Ljava/lang/String;", StringComparison.Ordinal))
            {
                addIdx.Add(i);
            }
        }

        var channels = (JsonArray)def["channels"]!;
        for (var a = 0; a < addIdx.Count; a++)
        {
            var partNameSearchFrom = a > 0 ? addIdx[a - 1] + 1 : 0;
            var partName = FindPartNameForAddAnimation(lines, addIdx[a], partNameSearchFrom);
            if (string.IsNullOrEmpty(partName))
            {
                notes.Add($"Definition {fieldName}: addAnimation at line {addIdx[a]} missing preceding ldc String part name.");
                continue;
            }

            var prevAddLine = a > 0 ? addIdx[a - 1] : -1;
            var windowStart = Math.Max(0, prevAddLine + 1);
            var targetLine = FindLastTargetsLineIndex(lines, addIdx[a] - 1, windowStart);
            if (targetLine < 0)
            {
                notes.Add($"Definition {fieldName}: channel for part {partName} missing AnimationChannel$Targets.");
                continue;
            }

            var target = TargetsRegex.Match(lines[targetLine]).Groups[1].Value;

            var ch = new JsonObject
            {
                ["partName"] = partName,
                ["target"] = target,
                ["keyframes"] = new JsonArray()
            };
            var kfArr = (JsonArray)ch["keyframes"]!;
            ParseKeyframesInRange(lines, windowStart, addIdx[a] + 1, kfArr, notes, fieldName, partName);
            ch["interpolation"] = SummarizeChannelInterpolation(kfArr);
            if (kfArr.Count == 0)
            {
                notes.Add(
                    $"Definition {fieldName} part {partName} {target}: channel listed in bytecode but no keyframes lifted (see docs/runtime-ir-preview-plan.md Part B).");
            }

            channels.Add(ch);
        }

        if (channels.Count == 0 && def.ContainsKey("lengthSeconds"))
        {
            notes.Add(
                $"Definition {fieldName}: has length but no addAnimation channels lifted (array-channel or obfuscated bytecode).");
        }

        return def;
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

    private static bool TryExtractWithLengthSeconds(List<string> lines, out float lengthSeconds)
    {
        lengthSeconds = 0f;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains("withLength:(F)", StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = Math.Max(0, i - 8); j < i; j++)
            {
                if (TryParseFloatPush(lines[j], out lengthSeconds))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ScanBackwardLdcString(List<string> lines, int start, int minIdx)
    {
        for (var i = start; i >= minIdx; i--)
        {
            var m = LdcStringRegex.Match(lines[i]);
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves the model part name for <c>addAnimation(String, AnimationChannel)</c>.
    /// Mojang bytecode always loads the part <c>ldc</c> immediately before <c>new AnimationChannel</c>,
    /// but channels with many keyframes can span far more than 120 instructions before <c>addAnimation</c>.
    /// </summary>
    private static string? FindPartNameForAddAnimation(List<string> lines, int addLineIdx, int searchFrom)
    {
        for (var i = addLineIdx - 1; i >= searchFrom; i--)
        {
            var line = lines[i];
            if (!line.Contains("new", StringComparison.Ordinal) ||
                !line.Contains("AnimationChannel", StringComparison.Ordinal) ||
                line.Contains("Keyframe", StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = i - 1; j >= searchFrom && j >= i - 4; j--)
            {
                var m = LdcStringRegex.Match(lines[j]);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
        }

        return ScanBackwardLdcString(lines, addLineIdx - 1, searchFrom);
    }

    private static int FindLastTargetsLineIndex(List<string> lines, int start, int minIdx)
    {
        for (var i = start; i > minIdx; i--)
        {
            if (TargetsRegex.IsMatch(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static void ParseKeyframesInRange(List<string> lines, int from, int toExclusive, JsonArray kfArr,
        List<string> notes, string fieldName, string partName)
    {
        for (var i = from; i < toExclusive; i++)
        {
            if (lines[i].Contains("anewarray", StringComparison.Ordinal) ||
                lines[i].Contains("AnimationChannel", StringComparison.Ordinal))
            {
                continue;
            }

            if (!lines[i].Contains("Keyframe", StringComparison.Ordinal) ||
                !lines[i].Contains("new", StringComparison.Ordinal))
            {
                continue;
            }

            var end = i + 1;
            while (end < toExclusive && !IsKeyframeConstructorInvoke(lines[end]))
            {
                end++;
            }

            if (end >= toExclusive)
            {
                continue;
            }

            if (!TryParseKeyframeBlock(lines, i, end + 1, out var kf))
            {
                notes.Add(
                    $"Definition {fieldName} part {partName}: keyframe block near line {i} skipped (unrecognized vec/time layout).");
                i = end;
                continue;
            }

            kfArr.Add(kf);
            i = end;
        }
    }

    private static bool IsKeyframeConstructorInvoke(string line) =>
        line.Contains("invokespecial", StringComparison.Ordinal) &&
        line.Contains("Keyframe.\"<init>\"", StringComparison.Ordinal);

    private static bool TryParseKeyframeBlock(List<string> lines, int start, int endExclusive, out JsonObject kf)
    {
        kf = null!;
        string? vecKind = null;
        string? interp = null;
        var numerics = new List<double>();
        var vecLineIdx = -1;

        for (var j = start; j < endExclusive; j++)
        {
            var im = InterpolationsRegex.Match(lines[j]);
            if (im.Success)
            {
                interp = im.Groups[1].Value;
            }

            if (vecLineIdx < 0 && TryParseVecKind(lines[j], out vecKind))
            {
                vecLineIdx = j;
                continue;
            }

            if (vecLineIdx < 0)
            {
                if (TryParseFloatPush(lines[j], out var ff))
                {
                    numerics.Add(ff);
                }
                else if (TryParseDoublePush(lines[j], out var dd))
                {
                    numerics.Add(dd);
                }
            }
        }

        if (vecKind is null || numerics.Count < 4)
        {
            return false;
        }

        var time = (float)numerics[0];
        var x = (float)numerics[^3];
        var y = (float)numerics[^2];
        var z = (float)numerics[^1];
        kf = new JsonObject
        {
            ["timeSeconds"] = Round(time),
            ["x"] = Round(x),
            ["y"] = Round(y),
            ["z"] = Round(z),
            ["vectorKind"] = vecKind,
            ["interpolation"] = interp ?? "UNKNOWN"
        };
        return true;
    }

    private static bool TryParseVecKind(string line, out string? kind)
    {
        kind = null;
        if (line.Contains("degreeVec:(FFF)", StringComparison.Ordinal) ||
            line.Contains("degreeVec:(DDD)", StringComparison.Ordinal))
        {
            kind = "degrees";
            return true;
        }

        if (line.Contains("posVec:(FFF)", StringComparison.Ordinal) ||
            line.Contains("posVec:(DDD)", StringComparison.Ordinal))
        {
            kind = "position";
            return true;
        }

        if (line.Contains("scaleVec:(FFF)", StringComparison.Ordinal) ||
            line.Contains("scaleVec:(DDD)", StringComparison.Ordinal))
        {
            kind = "scale";
            return true;
        }

        return false;
    }

    private static bool TryParseFloatPush(string line, out float f)
    {
        f = 0f;
        var m = LdcFloatRegex.Match(line);
        if (m.Success)
        {
            return float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
        }

        if (line.Contains("fconst_0", StringComparison.Ordinal))
        {
            f = 0f;
            return true;
        }

        if (line.Contains("fconst_1", StringComparison.Ordinal))
        {
            f = 1f;
            return true;
        }

        if (line.Contains("fconst_2", StringComparison.Ordinal))
        {
            f = 2f;
            return true;
        }

        var bip = Regex.Match(line, @"\b(bipush|sipush)\s+(-?\d+)", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (bip.Success && int.TryParse(bip.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
        {
            f = iv;
            return true;
        }

        if (line.Contains("iconst_m1", StringComparison.Ordinal))
        {
            f = -1f;
            return true;
        }

        for (var k = 0; k <= 5; k++)
        {
            if (line.Contains($"iconst_{k}", StringComparison.Ordinal))
            {
                f = k;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseDoublePush(string line, out double d)
    {
        d = 0d;
        var m = LdcDoubleRegex.Match(line);
        if (m.Success)
        {
            return double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }

        if (line.Contains("dconst_0", StringComparison.Ordinal))
        {
            d = 0d;
            return true;
        }

        if (line.Contains("dconst_1", StringComparison.Ordinal))
        {
            d = 1d;
            return true;
        }

        return false;
    }

    private static double Round(float v) => Math.Round(v, 6);

    private static string SummarizeChannelInterpolation(JsonArray kfArr)
    {
        if (kfArr.Count == 0)
        {
            return "NONE";
        }

        string? first = null;
        foreach (var n in kfArr)
        {
            if (n is not JsonObject o || o["interpolation"] is not JsonValue v)
            {
                continue;
            }

            var s = v.GetValue<string>();
            first ??= s;
            if (!string.Equals(first, s, StringComparison.Ordinal))
            {
                return "MIXED";
            }
        }

        return first ?? "UNKNOWN";
    }

    /// <summary>Returns true when any definition has empty channels or a channel with zero keyframes.</summary>
    internal static bool HasIncompleteChannels(JsonArray definitions)
    {
        foreach (var n in definitions)
        {
            if (n is not JsonObject def)
            {
                continue;
            }

            if (def["channels"] is not JsonArray chans || chans.Count == 0)
            {
                if (def.ContainsKey("lengthSeconds"))
                {
                    return true;
                }

                continue;
            }

            foreach (var ch in chans)
            {
                if (ch is JsonObject co && co["keyframes"] is JsonArray { Count: 0 })
                {
                    return true;
                }
            }
        }

        return false;
    }
}
