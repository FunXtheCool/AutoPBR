using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Independent pose oracle: <c>PartPose.offset</c> / <c>offsetAndRotation</c> from javap vs lifted IR shards.
/// Breaks IR ↔ <c>reference_java</c> circular validation for assembly parity (Agent 2C).
/// </summary>
public static class GeometryJavapPoseOracle
{
    public const double DefaultPoseTolerance = 0.05;

    public sealed record PartPose(
        double Tx,
        double Ty,
        double Tz,
        double Rx,
        double Ry,
        double Rz);

    public sealed record CompareResult(bool IsMatch, string? Message, int OraclePartCount, int IrPartCount);

    public sealed class Context
    {
        private readonly ConcurrentDictionary<string, Lazy<ResolveOutcome>> _disasmCache = new(StringComparer.Ordinal);

        public Context(
            string repoRoot,
            string versionLabel,
            IReadOnlySet<string> pilotJvmNames,
            string? clientJarPath,
            string? javapExecutable)
        {
            RepoRoot = repoRoot;
            VersionLabel = versionLabel;
            PilotJvmNames = pilotJvmNames;
            ClientJarPath = clientJarPath;
            JavapExecutable = javapExecutable;
            SnapshotDirectory = Path.Combine(repoRoot, "tools", "minecraft-parity", versionLabel, "javap-snapshots");
        }

        public string RepoRoot { get; }
        public string VersionLabel { get; }
        public IReadOnlySet<string> PilotJvmNames { get; }
        public string? ClientJarPath { get; }
        public string? JavapExecutable { get; }
        public string SnapshotDirectory { get; }

        public static Context? TryCreate(string repoRoot, string versionLabel)
        {
            var pilots = GeometryAssemblyParityPilots.Load(repoRoot, versionLabel);
            if (pilots.Count == 0)
            {
                return null;
            }

            var jar = Path.Combine(repoRoot, "tools", "minecraft-parity", versionLabel, "client.jar");
            if (!File.Exists(jar))
            {
                jar = null;
            }

            return new Context(repoRoot, versionLabel, pilots, jar, FindJavapExecutable());
        }

        public bool IsPilot(string officialJvmName) => PilotJvmNames.Contains(officialJvmName);

        public bool TryGetExpectedPoses(string officialJvmName, out IReadOnlyDictionary<string, PartPose> poses, out string? sourceNote)
        {
            poses = new Dictionary<string, PartPose>(StringComparer.Ordinal);
            sourceNote = null;
            if (!IsPilot(officialJvmName))
            {
                return false;
            }

            var lazy = _disasmCache.GetOrAdd(officialJvmName, jvm => new Lazy<ResolveOutcome>(
                () => ResolveJavapTextForJvm(jvm),
                LazyThreadSafetyMode.ExecutionAndPublication));

            var outcome = lazy.Value;
            sourceNote = outcome.SourceNote;
            if (!outcome.Ok || outcome.JavapText is null)
            {
                return false;
            }

            poses = Parser.ParseBindings(outcome.JavapText);
            return poses.Count > 0;
        }

        private ResolveOutcome ResolveJavapTextForJvm(string officialJvmName)
        {
            var shortName = officialJvmName[(officialJvmName.LastIndexOf('.') + 1)..];
            var snapshotPath = Path.Combine(SnapshotDirectory, $"{shortName}.createBodyLayer.javap.txt");
            if (File.Exists(snapshotPath))
            {
                return new ResolveOutcome(true, File.ReadAllText(snapshotPath), "javap snapshot");
            }

            if (string.IsNullOrEmpty(ClientJarPath) || string.IsNullOrEmpty(JavapExecutable))
            {
                return new ResolveOutcome(false, null, "no snapshot and client.jar or javap unavailable");
            }

            if (!GeometryJavapDisassembly.TryDisassemble(JavapExecutable, ClientJarPath, officialJvmName, out var hostText, out var err))
            {
                return new ResolveOutcome(false, null, err ?? "javap disassembly failed");
            }

            var poses = Parser.ParseBindings(hostText);
            if (poses.Count >= 2)
            {
                return new ResolveOutcome(true, hostText, "javap on-demand (host class)");
            }

            var delegateOwner = Parser.TryFindCrossClassMeshFactoryOwner(hostText);
            if (delegateOwner is not null &&
                !string.Equals(delegateOwner, officialJvmName, StringComparison.Ordinal) &&
                GeometryJavapDisassembly.TryDisassemble(JavapExecutable, ClientJarPath, delegateOwner, out var delegateText, out _))
            {
                var merged = hostText + "\n" + delegateText;
                if (Parser.ParseBindings(merged).Count > poses.Count)
                {
                    return new ResolveOutcome(true, merged, $"javap on-demand (host + {delegateOwner})");
                }
            }

            return new ResolveOutcome(true, hostText,
                poses.Count > 0 ? "javap on-demand (host class)" : "javap on-demand (no bindings)");
        }

    }

    /// <summary>Parses expected per-part poses from full-class <c>javap -c</c> text (all mesh factory methods).</summary>
    public static IReadOnlyDictionary<string, PartPose> ParseExpectedPosesFromJavap(string javapText) =>
        Parser.ParseBindings(javapText);

    public static CompareResult CompareShardToOracle(
        JsonElement shardRoot,
        IReadOnlyDictionary<string, PartPose> oracleByPartId,
        double tolerance = DefaultPoseTolerance)
    {
        var irById = CollectIrPosesByPartId(shardRoot);
        if (oracleByPartId.Count == 0)
        {
            return new CompareResult(false, "oracle: no part bindings parsed", 0, irById.Count);
        }

        if (irById.Count != oracleByPartId.Count)
        {
            return new CompareResult(
                false,
                $"pose part count oracle={oracleByPartId.Count} ir={irById.Count}",
                oracleByPartId.Count,
                irById.Count);
        }

        foreach (var (id, expected) in oracleByPartId)
        {
            if (!irById.TryGetValue(id, out var actual))
            {
                return new CompareResult(
                    false,
                    $"missing IR pose for part '{id}'",
                    oracleByPartId.Count,
                    irById.Count);
            }

            if (!PoseNear(expected, actual, tolerance))
            {
                return new CompareResult(
                    false,
                    $"part '{id}': oracle T=({expected.Tx:R},{expected.Ty:R},{expected.Tz:R}) R=({expected.Rx:R},{expected.Ry:R},{expected.Rz:R}) " +
                    $"ir T=({actual.Tx:R},{actual.Ty:R},{actual.Tz:R}) R=({actual.Rx:R},{actual.Ry:R},{actual.Rz:R})",
                    oracleByPartId.Count,
                    irById.Count);
            }
        }

        return new CompareResult(true, null, oracleByPartId.Count, irById.Count);
    }

    private sealed record ResolveOutcome(bool Ok, string? JavapText, string? SourceNote);

    private sealed class Parser
    {
        private static readonly Regex LdcStringRegex = new(
            @"^\s*\d+:\s+ldc\s+#\d+\s+//\s*String\s+(\S+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        private static readonly Regex LdcFloatRegex = new(
            @"^\s*\d+:\s+ldc\s+#\d+\s+//\s*float\s+(-?[\d.]+)f",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        private static readonly Regex MeshDelegateInvokeRegex = new(
            @"invokestatic\s+#\d+\s+//\s*Method\s+([\w./$]+)\.([\w$]+):\([^)]*\)L[\w/$]+(?:MeshDefinition|LayerDefinition);",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        public static Dictionary<string, PartPose> ParseBindings(string javapText)
        {
            var lines = FoldWrappedLines(javapText.Split('\n'));
            var poses = new Dictionary<string, PartPose>(StringComparer.Ordinal);
            for (var i = 0; i < lines.Count; i++)
            {
                if (!IsMeshBindingLine(lines[i]))
                {
                    continue;
                }

                if (!TryParseBindingAt(lines, i, out var partId, out var pose))
                {
                    continue;
                }

                poses[partId] = pose;
            }

            return poses;
        }

        public static string? TryFindCrossClassMeshFactoryOwner(string javapText)
        {
            var m = MeshDelegateInvokeRegex.Match(javapText);
            if (!m.Success)
            {
                return null;
            }

            var owner = m.Groups[1].Value.Replace('/', '.');
            return owner.Contains('.') ? owner : null;
        }

        private static bool IsMeshBindingLine(string line) =>
            line.Contains("PartDefinition.addOrReplaceChild", StringComparison.Ordinal) ||
            line.Contains("PartDefinition.addChild", StringComparison.Ordinal);

        private static bool TryParseBindingAt(IReadOnlyList<string> lines, int bindIdx, out string partId, out PartPose pose)
        {
            partId = "";
            pose = default;
            var searchFrom = Math.Max(0, bindIdx - 48);
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
                if (sm.Success)
                {
                    name = sm.Groups[1].Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(name) || poseInvoke < 0)
            {
                return false;
            }

            if (!TryParsePoseInvoke(lines, poseInvoke, searchFrom, out pose))
            {
                return false;
            }

            partId = name;
            return true;
        }

        private static bool TryParsePoseInvoke(IReadOnlyList<string> lines, int invokeIdx, int minIdx, out PartPose pose)
        {
            pose = default;
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

            if (!TryParseFloatOperandsBackward(lines, invokeIdx - 1, minIdx, operandCount, out var floats))
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
            IReadOnlyList<string> lines,
            int startIdx,
            int minIdx,
            int count,
            out List<double> values)
        {
            values = new List<double>(count);
            var j = startIdx;
            while (values.Count < count && j >= minIdx)
            {
                if (IsNonConstantFloatMath(lines[j]))
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

                if (TryParseFloatLine(lines[j], out var v))
                {
                    values.Add(v);
                }

                j--;
            }

            return values.Count == count;
        }

        private static bool IsNonConstantFloatMath(string line) =>
            line.Contains("fadd", StringComparison.Ordinal) ||
            line.Contains("fmul", StringComparison.Ordinal) ||
            line.Contains("fsub", StringComparison.Ordinal) ||
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

    private static Dictionary<string, PartPose> CollectIrPosesByPartId(JsonElement shardRoot)
    {
        var byId = new Dictionary<string, PartPose>(StringComparer.Ordinal);
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return byId;
        }

        foreach (var root in roots.EnumerateArray())
        {
            WalkIrPartPoses(root, byId);
        }

        return byId;
    }

    private static void WalkIrPartPoses(JsonElement part, Dictionary<string, PartPose> byId)
    {
        if (part.TryGetProperty("id", out var idEl) &&
            part.TryGetProperty("pose", out var pose) &&
            pose.ValueKind == JsonValueKind.Object)
        {
            var id = idEl.GetString();
            if (!string.IsNullOrEmpty(id) && !string.Equals(id, "root", StringComparison.Ordinal))
            {
                byId[id] = ReadIrPose(pose);
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkIrPartPoses(ch, byId);
            }
        }
    }

    private static PartPose ReadIrPose(JsonElement pose)
    {
        static double At(JsonElement arr, int i) =>
            arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > i ? arr[i].GetDouble() : 0;

        var t = pose.GetProperty("translation");
        var r = pose.TryGetProperty("rotationEulerRad", out var rot) ? rot : default;
        return new PartPose(At(t, 0), At(t, 1), At(t, 2), At(r, 0), At(r, 1), At(r, 2));
    }

    private static bool PoseNear(PartPose a, PartPose b, double tolerance)
    {
        return Near(a.Tx, b.Tx, tolerance) &&
               Near(a.Ty, b.Ty, tolerance) &&
               Near(a.Tz, b.Tz, tolerance) &&
               Near(a.Rx, b.Rx, tolerance) &&
               Near(a.Ry, b.Ry, tolerance) &&
               Near(a.Rz, b.Rz, tolerance);
    }

    private static bool Near(double a, double b, double tolerance) => Math.Abs(a - b) <= tolerance;

    /// <summary>Runs <c>javap -c -constants</c> against <paramref name="clientJar"/> (cached per process lifetime is caller-owned).</summary>
    internal static class GeometryJavapDisassembly
    {
        public static bool TryDisassemble(
            string? javapExecutable,
            string? clientJar,
            string officialJvmName,
            out string stdout,
            out string? error)
        {
            stdout = "";
            error = null;
            if (string.IsNullOrEmpty(clientJar) || string.IsNullOrEmpty(javapExecutable))
            {
                error = "client.jar or javap not configured";
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = javapExecutable,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-classpath");
                psi.ArgumentList.Add(clientJar);
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add("-constants");
                psi.ArgumentList.Add(officialJvmName);

                using var p = Process.Start(psi);
                if (p is null)
                {
                    error = "failed to start javap";
                    return false;
                }

                var stdoutTask = p.StandardOutput.ReadToEndAsync();
                var stderrTask = p.StandardError.ReadToEndAsync();
                p.WaitForExit();
                stdout = stdoutTask.GetAwaiter().GetResult();
                var stderr = stderrTask.GetAwaiter().GetResult();
                if (p.ExitCode != 0)
                {
                    error = $"javap exit {p.ExitCode}: {stderr}";
                    stdout = "";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    private static string? FindJavapExecutable()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            foreach (var rel in new[] { "bin/javap.exe", "bin/javap", "bin\\javap.exe", "bin\\javap" })
            {
                var p = Path.Combine(javaHome, rel);
                if (File.Exists(p))
                {
                    return p;
                }
            }
        }

        try
        {
            var fileName = OperatingSystem.IsWindows() ? "where" : "which";
            using var which = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                ArgumentList = { "javap" },
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (which is null)
            {
                return null;
            }

            var line = which.StandardOutput.ReadLine();
            which.WaitForExit();
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }
        catch
        {
            return null;
        }
    }
}
