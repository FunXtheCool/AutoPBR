using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Core.Preview;

public static partial class GeometryJavapPoseOracle
{
    private sealed partial class Parser
    {
        private static string? ExtractMethodText(string javapText, string methodName)
        {
            var lines = FoldWrappedLines(javapText.Split('\n'));
            var methodStart = FindMethodCodeStart(lines, methodName);
            if (methodStart < 0)
            {
                return null;
            }

            var methodEnd = lines.Count;
            for (var i = methodStart + 1; i < lines.Count; i++)
            {
                if (IsMethodBoundaryLine(lines[i]))
                {
                    methodEnd = i;
                    break;
                }
            }

            return string.Join('\n', lines.Skip(methodStart).Take(methodEnd - methodStart));
        }

        private static int FindMethodCodeStart(List<string> lines, string methodName)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (!lines[i].Contains(methodName + "(", StringComparison.Ordinal) ||
                    !lines[i].Contains("static", StringComparison.Ordinal))
                {
                    continue;
                }

                for (var j = i + 1; j < lines.Count && j < i + 6; j++)
                {
                    if (lines[j].TrimEnd().EndsWith("Code:", StringComparison.Ordinal))
                    {
                        return j + 1;
                    }
                }
            }

            return -1;
        }

        private static bool IsMethodBoundaryLine(string line) =>
            Regex.IsMatch(line, @"^\s{2}(public |protected |private |static )", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(line, @"^\s{2}\}$", RegexOptions.CultureInvariant);

        private static bool IsMeshBindingLine(string line) =>
            line.Contains("PartDefinition.addOrReplaceChild", StringComparison.Ordinal) ||
            line.Contains("PartDefinition.addChild", StringComparison.Ordinal);

        private static bool TryParseBindingAt(
            List<string> lines,
            int bindIdx,
            out string partId,
            out PartPose pose,
            MeshParamContext ctx)
        {
            partId = "";
            pose = new PartPose(0, 0, 0, 0, 0, 0);
            var searchFrom = Math.Max(0, bindIdx - 128);
            string? name = null;
            var poseInvoke = -1;
            for (var i = bindIdx - 1; i >= searchFrom; i--)
            {
                if (poseInvoke < 0)
                {
                    if (lines[i].Contains("PartPose.offsetAndRotation", StringComparison.Ordinal) ||
                        (lines[i].Contains("PartPose.offset", StringComparison.Ordinal) &&
                         !lines[i].Contains("offsetAndRotation", StringComparison.Ordinal)) ||
                        lines[i].Contains("PartPose.ZERO", StringComparison.Ordinal) ||
                        lines[i].Contains("PartPose.rotation", StringComparison.Ordinal))
                    {
                        poseInvoke = i;
                    }
                }

                var sm = LdcStringRegex.Match(lines[i]);
                if (!sm.Success)
                {
                    continue;
                }

                var candidate = sm.Groups[1].Value;
                if (IsKnownNamedCuboidOnly(candidate, ctx))
                {
                    continue;
                }

                if (!IsPartRootNameCandidate(lines, i, bindIdx))
                {
                    continue;
                }

                name = candidate;
                break;
            }

            if (string.IsNullOrEmpty(name) || poseInvoke < 0)
            {
                return false;
            }

            if (!TryParsePoseInvoke(lines, poseInvoke, searchFrom, out pose, ctx))
            {
                return false;
            }

            partId = name;
            return true;
        }

        /// <summary>Skips <c>ldc</c> names that are arguments to <c>addBox(String, …)</c> nested inside a part builder.</summary>
        private static bool IsKnownNamedCuboidOnly(string candidate, MeshParamContext ctx) =>
            string.Equals(candidate, "belly", StringComparison.Ordinal) ||
            string.Equals(candidate, "ear", StringComparison.Ordinal) ||
            string.Equals(candidate, "ear1", StringComparison.Ordinal) ||
            string.Equals(candidate, "ear2", StringComparison.Ordinal) ||
            string.Equals(candidate, "goatee", StringComparison.Ordinal) ||
            string.Equals(candidate, "main", StringComparison.Ordinal) ||
            string.Equals(candidate, "neck", StringComparison.Ordinal) ||
            (ctx.SkipDecorativeNose && string.Equals(candidate, "nose", StringComparison.Ordinal)) ||
            string.Equals(candidate, "nostril", StringComparison.Ordinal);

        /// <summary>Skips <c>ldc</c> names that are arguments to <c>addBox(String, …)</c> nested inside a part builder.</summary>
        private static bool TryGetLdcPartName(List<string> lines, int ldcIdx, out string partName)
        {
            partName = "";
            var sm = LdcStringRegex.Match(lines[ldcIdx]);
            if (!sm.Success)
            {
                return false;
            }

            partName = sm.Groups[1].Value;
            return true;
        }

        /// <summary>
        /// Part roots are bound via <c>CubeListBuilder.create()</c> after the part name <c>ldc</c>, or by reusing a
        /// shared builder local (e.g. Creeper legs after <c>astore_3</c>).
        /// </summary>
        private static bool IsPartRootNameCandidate(List<string> lines, int ldcIdx, int bindIdx)
        {
            for (var i = ldcIdx + 1; i < Math.Min(bindIdx, ldcIdx + 8); i++)
            {
                if (lines[i].Contains("CubeListBuilder.create", StringComparison.Ordinal))
                {
                    return true;
                }

                if (SharedLegBuilderLoadRegex.IsMatch(lines[i]))
                {
                    return true;
                }

                if (TryGetLdcPartName(lines, ldcIdx, out var partName) &&
                    partName.Contains("_leg", StringComparison.Ordinal) &&
                    (lines[i].Contains("aload_2", StringComparison.Ordinal) ||
                     lines[i].Contains("aload_3", StringComparison.Ordinal) ||
                     Regex.IsMatch(lines[i], @"aload\s+[23]\b", RegexOptions.CultureInvariant)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParsePoseInvoke(
            List<string> lines,
            int invokeIdx,
            int minIdx,
            out PartPose pose,
            MeshParamContext ctx)
        {
            pose = new PartPose(0, 0, 0, 0, 0, 0);
            if (lines[invokeIdx].Contains("PartPose.ZERO", StringComparison.Ordinal))
            {
                pose = new PartPose(0, 0, 0, 0, 0, 0);
                return true;
            }

            var operandCount = lines[invokeIdx].Contains("PartPose.offsetAndRotation", StringComparison.Ordinal) ? 6
                : lines[invokeIdx].Contains("PartPose.rotation", StringComparison.Ordinal) ? 3
                : lines[invokeIdx].Contains("PartPose.offset", StringComparison.Ordinal) ? 3
                : 0;
            if (operandCount == 0)
            {
                return false;
            }

            if (!TryParseFloatOperandsBackward(lines, invokeIdx - 1, minIdx, operandCount, out var floats, ctx))
            {
                return false;
            }

            floats.Reverse();
            if (operandCount == 6)
            {
                pose = new PartPose(floats[0], floats[1], floats[2], floats[3], floats[4], floats[5]);
            }
            else if (lines[invokeIdx].Contains("PartPose.rotation", StringComparison.Ordinal))
            {
                pose = new PartPose(0, 0, 0, floats[0], floats[1], floats[2]);
            }
            else
            {
                pose = new PartPose(floats[0], floats[1], floats[2], 0, 0, 0);
            }

            return true;
        }

        private static bool TryParseFloatOperandsBackward(
            List<string> lines,
            int startIdx,
            int minIdx,
            int count,
            out List<double> values,
            MeshParamContext ctx)
        {
            values = new List<double>(count);
            var j = startIdx;
            while (values.Count < count && j >= minIdx)
            {
                if (IsNonConstantFloatMath(lines[j]) && !IsParametricIntToFloatLine(lines, j))
                {
                    values.Clear();
                    break;
                }

                if (lines[j].Contains("fneg", StringComparison.Ordinal))
                {
                    j--;
                    if (j >= minIdx && TryParseFloatLine(lines[j], out var negated))
                    {
                        values.Add(-negated);
                    }

                    j--;
                    continue;
                }

                if (TryParseParametricFloatBackward(lines, j, minIdx, ctx, out var parametric))
                {
                    values.Add(parametric);
                    j = SkipParametricIntToFloatBlock(lines, j, minIdx);
                    continue;
                }

                if (TryParseFloatLine(lines[j], out var v))
                {
                    values.Add(v);
                }

                j--;
            }

            return values.Count == count;
        }

        private static bool IsParametricIntToFloatLine(List<string> lines, int idx) =>
            lines[idx].Contains("i2f", StringComparison.Ordinal) ||
            lines[idx].Contains("isub", StringComparison.Ordinal) ||
            Regex.IsMatch(lines[idx], @"\biload_\d+\b", RegexOptions.CultureInvariant);

        private static int SkipParametricIntToFloatBlock(List<string> lines, int i2fIdx, int minIdx)
        {
            var j = i2fIdx - 1;
            while (j >= minIdx && IsParametricIntToFloatLine(lines, j))
            {
                j--;
            }

            return j;
        }

        private static bool TryParseParametricFloatBackward(
            List<string> lines,
            int startIdx,
            int minIdx,
            MeshParamContext ctx,
            out double value)
        {
            value = 0;
            if (!lines[startIdx].Contains("i2f", StringComparison.Ordinal))
            {
                return false;
            }

            if (startIdx - 1 < minIdx || !lines[startIdx - 1].Contains("isub", StringComparison.Ordinal))
            {
                return false;
            }

            if (startIdx - 2 < minIdx || !LocalLoadRegex.IsMatch(lines[startIdx - 2]))
            {
                return false;
            }

            for (var j = startIdx - 3; j >= minIdx && j >= startIdx - 8; j--)
            {
                if (TryParseBipushLine(lines[j], out var constant))
                {
                    value = constant - ctx.MeshAge;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseBipushLine(string line, out int value)
        {
            value = 0;
            var m = Regex.Match(line, @"^\s*\d+:\s+bipush\s+(-?\d+)", RegexOptions.CultureInvariant);
            if (!m.Success)
            {
                m = Regex.Match(line, @"^\s*\d+:\s+sipush\s+(-?\d+)", RegexOptions.CultureInvariant);
            }

            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsNonConstantFloatMath(string line) =>
            line.Contains("fadd", StringComparison.Ordinal) ||
            line.Contains("fmul", StringComparison.Ordinal) ||
            line.Contains("fsub", StringComparison.Ordinal) ||
            line.Contains("fmotion", StringComparison.Ordinal) ||
            line.Contains("fdiv", StringComparison.Ordinal) ||
            line.Contains("fload", StringComparison.Ordinal);

        private static bool TryParseFloatLine(string line, out double value)
        {
            value = 0;
            var fm = LdcFloatRegex.Match(line);
            if (fm.Success &&
                double.TryParse(fm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (line.Contains("fconst_0", StringComparison.Ordinal))
            {
                value = 0;
                return true;
            }

            if (line.Contains("fconst_1", StringComparison.Ordinal))
            {
                value = 1;
                return true;
            }

            if (line.Contains("fconst_2", StringComparison.Ordinal))
            {
                value = 2;
                return true;
            }

            return false;
        }

        private static List<string> FoldWrappedLines(IEnumerable<string> raw)
        {
            var folded = new List<string>();
            foreach (var line in raw)
            {
                if (folded.Count > 0 &&
                    !Regex.IsMatch(line, @"^\s*\d+:\s+", RegexOptions.CultureInvariant) &&
                    (line.Contains("PartPose", StringComparison.Ordinal) ||
                     line.Contains("addOrReplaceChild", StringComparison.Ordinal) ||
                     line.Contains("addChild", StringComparison.Ordinal)))
                {
                    folded[^1] = folded[^1] + " " + line.Trim();
                }
                else
                {
                    folded.Add(line);
                }
            }

            return folded;
        }
    }
}
