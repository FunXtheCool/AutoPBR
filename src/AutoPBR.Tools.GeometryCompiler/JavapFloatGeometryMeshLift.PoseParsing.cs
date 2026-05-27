using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static AutoPBR.Tools.GeometryCompiler.GeometryLiftCoordinateRounding;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>
    /// When javac emits <c>PartPose.ZERO.withScale(f)</c> or similar after <c>offset</c>/<c>offsetAndRotation</c>, lift a
    /// constant scale factor when it is loaded via <c>ldc</c>/<c>fconst_*</c> immediately before <c>withScale</c>.
    /// </summary>
    private static void TryApplyUniformScaleAfterPartPoseInvoke(List<string> seg, int partPoseInvokeLine, int minBound,
        int maxBound, JsonObject pose)
    {
        for (var k = partPoseInvokeLine + 1; k <= maxBound && k < seg.Count; k++)
        {
            if (!seg[k].Contains("withScale:(F)L", StringComparison.Ordinal) &&
                !seg[k].Contains("PartPose.withScale", StringComparison.Ordinal))
            {
                continue;
            }

            for (var j = k - 1; j >= minBound && j >= 0; j--)
            {
                if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(seg[j], out var s))
                {
                    pose["uniformScale"] = Round(s);
                    return;
                }
            }

            return;
        }
    }

    private static bool IsPartPoseRotationInvokeLine(string line) =>
        line.Contains("PartPose.rotation", StringComparison.Ordinal);

    private static int FindMeshBindingLineIndex(List<string> seg)
    {
        for (var i = seg.Count - 1; i >= 0; i--)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[i]))
            {
                return i;
            }
        }

        return seg.Count - 1;
    }

    private static bool IsPartPoseFactoryInvokeLine(string line) =>
        line.Contains("PartPose.ZERO", StringComparison.Ordinal) ||
        line.Contains("PartPose.offsetAndRotation", StringComparison.Ordinal) ||
        (line.Contains("PartPose.offset", StringComparison.Ordinal) &&
         !line.Contains("offsetAndRotation", StringComparison.Ordinal)) ||
        IsPartPoseRotationInvokeLine(line);

    /// <summary>
    /// Reused <c>CubeListBuilder</c> legs (<c>ldc</c> name, <c>aload</c> builder, <c>PartPose</c>, bind) share a template
    /// <c>addBox</c> earlier in the slice — take the last <c>PartPose</c> factory immediately before this binding.
    /// </summary>
    private static bool TryParsePartPoseImmediatelyBeforeBinding(List<string> seg, string? poseClassHint,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out JsonObject pose)
    {
        pose = ZeroPose();
        var bindingIdx = FindMeshBindingLineIndex(seg);
        if (bindingIdx <= 0)
        {
            return false;
        }

        var searchFrom = Math.Max(0, bindingIdx - 64);
        for (var i = bindingIdx - 1; i >= searchFrom; i--)
        {
            if (!IsPartPoseFactoryInvokeLine(seg[i]))
            {
                continue;
            }

            var poseWindowStart = ResolvePoseOperandWindowStart(seg, bindingIdx, i);
            if (TryParsePose(seg, poseWindowStart, i, poseClassHint, poseFloatLocals, poseIntLocals, poseWarnings,
                    out pose))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindPreviousMeshBindingLineIndex(List<string> seg, int beforeLine)
    {
        for (var j = beforeLine - 1; j >= 0; j--)
        {
            if (JavapMeshBytecodeProfiles.IsNamedOrObfuscatedMeshBindingLine(seg[j]))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>
    /// Keeps <c>PartPose</c> operand scans between the bind site and the last factory invoke — for reused
    /// <c>CubeListBuilder</c> legs, do not walk back into the shared template <c>addBox</c> float stack.
    /// </summary>
    private static int ResolvePoseOperandWindowStart(List<string> seg, int bindingIdx, int partPoseInvokeLine)
    {
        if (TryFindReusedBuilderAloadBeforeBinding(seg, bindingIdx, out _, out var templateAstoreLine))
        {
            for (var j = partPoseInvokeLine - 1; j > templateAstoreLine; j--)
            {
                if (!JavapBytecodeStreamAnalyzer.TryParseAloadLocalSlot(seg[j], out _))
                {
                    continue;
                }

                for (var k = j - 1; k >= templateAstoreLine && k > j - 6; k--)
                {
                    if (JavapBytecodeStreamAnalyzer.MatchLdcString(seg[k]).Success)
                    {
                        return j;
                    }
                }
            }

            return Math.Max(templateAstoreLine + 1, partPoseInvokeLine - 12);
        }

        var prevBinding = FindPreviousMeshBindingLineIndex(seg, partPoseInvokeLine);
        return prevBinding >= 0 ? prevBinding + 1 : Math.Max(0, partPoseInvokeLine - 24);
    }

    private static void StampSetupAnimPivot(JsonObject pose)
    {
        if (pose["translation"] is not JsonArray t || t.Count < 3)
        {
            return;
        }

        pose["setupAnimPivot"] = new JsonArray(
            JsonValue.Create(t[0]!.GetValue<double>()),
            JsonValue.Create(t[1]!.GetValue<double>()),
            JsonValue.Create(t[2]!.GetValue<double>()));
    }

    private static void AppendPoseWarnings(JsonObject pose, IReadOnlyList<string> codes)
    {
        if (codes.Count == 0)
        {
            return;
        }

        var w = pose["liftWarnings"] as JsonArray ?? new JsonArray();
        foreach (var c in codes)
        {
            w.Add(c);
        }

        pose["liftWarnings"] = w;
    }

    private static bool TryParsePose(List<string> seg, int from, int to, string? poseClassHint,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out JsonObject pose)
    {
        pose = ZeroPose();
        if (from > to)
        {
            return false;
        }

        if (SegmentContainsLoopBasedPoseMath(seg, from, to))
        {
            poseWarnings.Add("pose_loop_unsupported");
        }

        for (var i = from; i <= to; i++)
        {
            if (seg[i].Contains("PartPose.ZERO", StringComparison.Ordinal))
            {
                pose = ZeroPose();
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (seg[i].Contains("PartPose.offsetAndRotation", StringComparison.Ordinal))
            {
                if (!TryParsePoseFloatOperandsBackward(seg, i - 1, from, 6, poseFloatLocals, poseIntLocals,
                        poseWarnings, out var fl))
                {
                    continue;
                }

                fl.Reverse();
                pose = new JsonObject
                {
                    ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                    ["rotationEulerRad"] = new JsonArray { Round(fl[3]), Round(fl[4]), Round(fl[5]) },
                    ["eulerOrder"] = "XYZ"
                };
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (IsPartPoseRotationInvokeLine(seg[i]))
            {
                if (!TryParsePoseFloatOperandsBackward(seg, i - 1, from, 3, poseFloatLocals, poseIntLocals,
                        poseWarnings, out var rFl))
                {
                    continue;
                }

                rFl.Reverse();
                pose = new JsonObject
                {
                    ["translation"] = new JsonArray { 0d, 0d, 0d },
                    ["rotationEulerRad"] = new JsonArray { Round(rFl[0]), Round(rFl[1]), Round(rFl[2]) },
                    ["eulerOrder"] = "XYZ"
                };
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (seg[i].Contains("PartPose.offset", StringComparison.Ordinal) &&
                !seg[i].Contains("offsetAndRotation", StringComparison.Ordinal))
            {
                if (!TryParsePoseFloatOperandsBackward(seg, i - 1, from, 3, poseFloatLocals, poseIntLocals,
                        poseWarnings, out var fl))
                {
                    continue;
                }

                fl.Reverse();
                pose = new JsonObject
                {
                    ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                    ["rotationEulerRad"] = new JsonArray { 0d, 0d, 0d },
                    ["eulerOrder"] = "XYZ"
                };
                TryApplyUniformScaleAfterPartPoseInvoke(seg, i, from, to, pose);
                return true;
            }

            if (!string.IsNullOrEmpty(poseClassHint) &&
                !string.Equals(poseClassHint, "PartPose", StringComparison.Ordinal))
            {
                _ = TryAnnotateObfuscatedPartPoseFactory(seg[i], poseClassHint, poseWarnings);
                if (TryParseObfuscatedPartPose(seg[i], poseClassHint, from, i, seg, poseFloatLocals, poseIntLocals,
                        poseWarnings, out pose))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseObfuscatedPartPose(string line, string poseShort, int minIdx, int invokeIdx,
        List<string> seg, IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out JsonObject pose)
    {
        pose = ZeroPose();
        if (!line.Contains($"// Method {poseShort}.", StringComparison.Ordinal) &&
            !line.Contains($"// Method {poseShort}/", StringComparison.Ordinal))
        {
            return false;
        }

        _ = TryAnnotateObfuscatedPartPoseFactory(line, poseShort, poseWarnings);

        var six = $"(FFFFFF)L{poseShort};";
        var three = $"(FFF)L{poseShort};";
        if (line.Contains(six, StringComparison.Ordinal))
        {
            if (invokeIdx < 0 ||
                !TryParsePoseFloatOperandsBackward(seg, invokeIdx - 1, minIdx, 6, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var fl))
            {
                return false;
            }

            fl.Reverse();
            pose = new JsonObject
            {
                ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                ["rotationEulerRad"] = new JsonArray { Round(fl[3]), Round(fl[4]), Round(fl[5]) },
                ["eulerOrder"] = "XYZ"
            };
            var cap = Math.Min(invokeIdx + 12, seg.Count - 1);
            TryApplyUniformScaleAfterPartPoseInvoke(seg, invokeIdx, minIdx, cap, pose);
            return true;
        }

        if (line.Contains(three, StringComparison.Ordinal))
        {
            if (invokeIdx < 0 ||
                !TryParsePoseFloatOperandsBackward(seg, invokeIdx - 1, minIdx, 3, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var fl))
            {
                return false;
            }

            fl.Reverse();
            pose = new JsonObject
            {
                ["translation"] = new JsonArray { Round(fl[0]), Round(fl[1]), Round(fl[2]) },
                ["rotationEulerRad"] = new JsonArray { 0d, 0d, 0d },
                ["eulerOrder"] = "XYZ"
            };
            var cap3 = Math.Min(invokeIdx + 12, seg.Count - 1);
            TryApplyUniformScaleAfterPartPoseInvoke(seg, invokeIdx, minIdx, cap3, pose);
            return true;
        }

        if (line.Contains("getstatic", StringComparison.Ordinal) &&
            line.Contains($"Field {poseShort}.a:L{poseShort};", StringComparison.Ordinal))
        {
            pose = ZeroPose();
            var capZ = Math.Min(invokeIdx + 12, seg.Count - 1);
            TryApplyUniformScaleAfterPartPoseInvoke(seg, invokeIdx, minIdx, capZ, pose);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Lifts JVM stack operands backward for vanilla <see cref="PartPose"/> factories: ldc/fconst values,
    /// <c>fneg</c>/<c>fadd</c>/<c>fmul</c>/<c>fsub</c>/<c>fdiv</c> of recursively parsed operands,
    /// <c>fload*</c> resolved from simple <c>fstore</c> constants when available, unknown <c>fload*</c> and
    /// <c>iload</c>+<c>isub</c>+<c>i2f</c>, and <c>dload*</c> placeholders as <c>0</c> when slots are unknown.
    /// </summary>
    private static bool TryParsePoseFloatOperandsBackward(List<string> seg, int start, int minIdx, int want,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out List<double> floats)
    {
        floats = new List<double>();
        var j = start;
        while (floats.Count < want && j >= minIdx)
        {
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth: 0, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var v))
            {
                return false;
            }

            floats.Add(v);
        }

        return floats.Count >= want;
    }

    private static bool TryConsumeOnePoseFloatOperandBackward(List<string> seg, ref int j, int minIdx, int depth,
        IReadOnlyDictionary<int, double> poseFloatLocals, IReadOnlyDictionary<int, int> poseIntLocals,
        ICollection<string> poseWarnings, out double v)
    {
        v = 0;
        if (depth > 12)
        {
            return false;
        }

        while (j >= minIdx && string.IsNullOrWhiteSpace(seg[j]))
        {
            j--;
        }

        if (j < minIdx)
        {
            return false;
        }

        var line = seg[j];
        if (JavapBytecodeStreamAnalyzer.TryParseFloatLine(line, out v))
        {
            j--;
            return true;
        }

        if (TryParseKnownStaticFloatField(line, poseWarnings, out v))
        {
            j--;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.TryParseIntToFloatOperandBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        if (TryParseFloatFromStaticFloatArrayBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        if (TryParseFloatFromStaticMatrixBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        if (TryParseGuardianSpikeHelperInvokeBackward(seg, ref j, minIdx, poseIntLocals, out v))
        {
            return true;
        }

        var mathMatch = JavapBytecodeStreamAnalyzer.MathUnaryFloatInvokeRegex().Match(line);
        if (mathMatch.Success)
        {
            var op = mathMatch.Groups[1].Success && mathMatch.Groups[1].Value.Length > 0
                ? mathMatch.Groups[1].Value
                : mathMatch.Groups[2].Value;
            var isDoubleUnary = line.Contains(":(D)D", StringComparison.Ordinal) ||
                                line.Contains("Mth.", StringComparison.Ordinal);
            j--;
            if (j >= minIdx && JavapBytecodeStreamAnalyzer.JavapD2iInsnRegex().IsMatch(seg[j]))
            {
                j--;
                if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(seg[j], out var angleSlot) &&
                    poseFloatLocals.TryGetValue(angleSlot, out var angleFv))
                {
                    j--;
                    v = op switch
                    {
                        "sin" => Math.Sin((int)angleFv),
                        "cos" => Math.Cos((int)angleFv),
                        _ => 0
                    };
                    return true;
                }
            }

            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var arg))
            {
                poseWarnings.Add("math_non_constant");
                return false;
            }

            v = op switch
            {
                "sin" => Math.Sin(isDoubleUnary ? arg : arg),
                "cos" => Math.Cos(isDoubleUnary ? arg : arg),
                "toRadians" => arg * Math.PI / 180.0,
                "abs" => Math.Abs(arg),
                _ => 0
            };
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapI2bInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals,
                    poseWarnings, out var inner))
            {
                return false;
            }

            v = (sbyte)(byte)(int)inner;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapD2fInsnRegex().IsMatch(line))
        {
            j--;
            return TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth, poseFloatLocals, poseIntLocals,
                poseWarnings, out v);
        }

        if (JavapBytecodeStreamAnalyzer.JavapFnegInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var inner))
            {
                return false;
            }

            v = -inner;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFaddInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            v = lhs + rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFmulInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            v = lhs * rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFsubInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            v = lhs - rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.JavapFdivInsnRegex().IsMatch(line))
        {
            j--;
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var rhs) ||
                !TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth + 1, poseFloatLocals, poseIntLocals, poseWarnings,
                    out var lhs))
            {
                return false;
            }

            if (Math.Abs(rhs) < 1e-15)
            {
                return false;
            }

            v = lhs / rhs;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.TryParseFloadLocalSlot(line, out var fslot))
        {
            j--;
            if (poseFloatLocals.TryGetValue(fslot, out v))
            {
                return true;
            }

            poseWarnings.Add("unknown_fload_zeroed");
            v = 0;
            return true;
        }

        if (JavapBytecodeStreamAnalyzer.IsDynamicDoublePlaceholderLoad(line))
        {
            j--;
            v = 0;
            return true;
        }

        // Skip aload / pop etc. noise without producing a usable scalar.
        var insn = line.TrimStart();
        if (Regex.IsMatch(insn, @"^\d+:\s+(pop|nop|swap|dup)\b", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(insn, @"^\d+:\s+aload_", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(insn, @"^\d+:\s+aload\s+\d+", RegexOptions.CultureInvariant))
        {
            j--;
            return TryConsumeOnePoseFloatOperandBackward(seg, ref j, minIdx, depth, poseFloatLocals, poseIntLocals, poseWarnings, out v);
        }

        return false;
    }

    private static JsonObject ZeroPose() =>
        new()
        {
            ["translation"] = new JsonArray { 0d, 0d, 0d },
            ["rotationEulerRad"] = new JsonArray { 0d, 0d, 0d },
            ["eulerOrder"] = "XYZ"
        };
}
