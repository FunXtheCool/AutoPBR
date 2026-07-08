namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Phase 0C: capture javap mesh-factory bytecode for assembly-parity pilot JVMs.
/// </summary>
internal static class ExportAssemblyPilotJavapSnapshotsCommand
{
    private const int MaxSnapshotBytes = 100 * 1024;
    private const int PreferFullClassMaxBytes = 95 * 1024;

    public static int Run(string[] args)
    {
        string? clientJar = null;
        string? pilotList = null;
        string? snapshotsDir = null;
        string? summaryPath = null;
        string? javapOverride = null;
        var force = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            if (string.Equals(a, "--client-jar", StringComparison.OrdinalIgnoreCase))
            {
                clientJar = Next();
            }
            else if (string.Equals(a, "--pilot-list", StringComparison.OrdinalIgnoreCase))
            {
                pilotList = Next();
            }
            else if (string.Equals(a, "--snapshots-dir", StringComparison.OrdinalIgnoreCase))
            {
                snapshotsDir = Next();
            }
            else if (string.Equals(a, "--summary-csv", StringComparison.OrdinalIgnoreCase))
            {
                summaryPath = Next();
            }
            else if (string.Equals(a, "--javap", StringComparison.OrdinalIgnoreCase))
            {
                javapOverride = Next();
            }
            else if (string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
            }
        }

        if (string.IsNullOrWhiteSpace(clientJar) || !File.Exists(clientJar))
        {
            Console.Error.WriteLine("Missing or unreadable --client-jar.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(pilotList) || !File.Exists(pilotList))
        {
            Console.Error.WriteLine("Missing or unreadable --pilot-list.");
            return 2;
        }

        snapshotsDir = string.IsNullOrWhiteSpace(snapshotsDir)
            ? Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "javap-snapshots")
            : Path.GetFullPath(snapshotsDir);
        Directory.CreateDirectory(snapshotsDir);

        var javap = string.IsNullOrWhiteSpace(javapOverride) ? GeometryJavapLocator.FindJavap() : javapOverride;
        var pilots = File.ReadAllLines(pilotList)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

        var rows = new List<string> { "jvm,factoryMethod,offsetOnlyBinds,offsetAndRotationBinds,flatRootYN,snapshotFile,bytes" };
        var written = 0; var covered = 0; var skipped = 0;

        foreach (var officialJvmName in pilots)
        {
            if (!TryExportOne(clientJar, javap, snapshotsDir, officialJvmName, force, out var row, out var wrote))
            {
                skipped++;
                rows.Add($"{EscapeCsv(officialJvmName)},,,,,,SKIP");
                Console.Error.WriteLine($"SKIP {officialJvmName}");
                continue;
            }

            if (wrote) { written++; } covered++;

            rows.Add(row);
        }

        if (!string.IsNullOrWhiteSpace(summaryPath))
        {
            var dir = Path.GetDirectoryName(summaryPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllLines(summaryPath, rows);
        }

        Console.WriteLine($"coverage={covered}/{pilots.Count} snapshots_new={written} skipped={skipped}");
        return skipped > 0 && covered == 0 ? 1 : 0;
    }

    private static bool TryExportOne(
        string clientJar,
        string? javap,
        string snapshotsDir,
        string officialJvmName,
        bool force,
        out string csvRow,
        out bool wrote)
    {
        csvRow = string.Empty;
        wrote = false;
        if (!ClientJarIO.TryResolveJarEntry(clientJar, officialJvmName, null, out _, out var entryBytes))
        {
            return false;
        }

        const string requestedFactory = "createBodyLayer";
        var factoryMethod = ResolveFactoryMethod(clientJar, officialJvmName, requestedFactory, entryBytes);
        var hostJvm = officialJvmName;
        if (BytecodeMeshResolution.TryResolve(clientJar, null, officialJvmName, factoryMethod, out var resolved))
        {
            hostJvm = resolved.HostJvmName;
            factoryMethod = ResolveFactoryMethod(clientJar, hostJvm, factoryMethod, resolved.PrimaryClassBytes);
        }

        var simple = hostJvm[(hostJvm.LastIndexOf('.') + 1)..];
        var fileName = $"{simple}.{factoryMethod}.javap.txt";
        var outPath = Path.Combine(snapshotsDir, fileName);

        if (File.Exists(outPath) && !force)
        {
            var existing = File.ReadAllText(outPath);
            var stats = CountBindPatterns(existing);
            var flat = InferFlatRoot(existing);
            csvRow = BuildCsv(officialJvmName, factoryMethod, stats, flat, fileName, new FileInfo(outPath).Length);
            wrote = false;
            return true;
        }

        if (!JavapClassDisassembly.TryDisassemble(javap, clientJar, hostJvm, out var fullStdout, out var err))
        {
            Console.Error.WriteLine($"javap failed {hostJvm}: {err}");
            return false;
        }

        string body;
        var note = "full class disassembly";
        var utf8Len = System.Text.Encoding.UTF8.GetByteCount(fullStdout);
        if (utf8Len <= PreferFullClassMaxBytes)
        {
            body = fullStdout;
        }
        else
        {
            var slice = JavapClassDisassembly.ConcatMeshFactoryCodeDeep(
                javap, clientJar, fullStdout, hostJvm, null, hostJvm.Replace('.', '/'));
            if (string.IsNullOrWhiteSpace(slice))
            {
                slice = JavapClassDisassembly.ExtractMethodCodeBlock(fullStdout, factoryMethod) ?? string.Empty;
            }

            body = slice.Length > 0 ? slice : fullStdout;
            note = "mesh-factory slice (class exceeded size budget)";
        }

        var header =
            $"# javap snapshot - Minecraft 26.1.2 client.jar\r\n" +
            $"# Class: {hostJvm}\r\n" +
            $"# Pilot entry: {officialJvmName}\r\n" +
            $"# Factory method: {factoryMethod}\r\n" +
            $"# Command: javap -c -constants\r\n" +
            $"# Content: {note}\r\n" +
            $"# Generated: {DateTimeOffset.Now:O}\r\n";

        var payload = header + body;
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaxSnapshotBytes)
        {
            payload = TruncateUtf8(payload, MaxSnapshotBytes - 64) +
                      "\r\n# ... truncated to remain under 100KB budget\r\n";
        }

        File.WriteAllText(outPath, payload, new System.Text.UTF8Encoding(false));
        var bindStats = CountBindPatterns(payload);
        var flatRoot = InferFlatRoot(payload);
        csvRow = BuildCsv(officialJvmName, factoryMethod, bindStats, flatRoot, fileName,
            new FileInfo(outPath).Length);
        wrote = true;
        Console.WriteLine($"Wrote {fileName} ({new FileInfo(outPath).Length} bytes)");
        return true;
    }

    private static string ResolveFactoryMethod(string clientJar, string officialJvmName, string requested,
        ReadOnlySpan<byte> classBytes)
    {
        var resolved = MeshFactoryMethodResolver.Resolve(null, officialJvmName, requested, classBytes);
        foreach (var host in MeshHostClassCandidates.Enumerate(officialJvmName))
        {
            if (!ClientJarIO.TryResolveJarEntry(clientJar, host, null, out _, out var hostBytes))
            {
                continue;
            }

            if (BytecodeMeshResolution.ShouldSkipMeshHostWithoutPrimaryFactory(host, hostBytes, requested))
            {
                continue;
            }

            return MeshFactoryMethodResolver.Resolve(null, officialJvmName, requested, hostBytes);
        }

        return resolved;
    }

    private static (int OffsetOnly, int OffsetAndRotation) CountBindPatterns(string text)
    {
        var offsetAndRotation = 0;
        var offsetOnly = 0;
        foreach (var line in text.Split('\n'))
        {
            if (line.Contains("PartPose.offsetAndRotation", StringComparison.Ordinal))
            {
                offsetAndRotation++;
            }
            else if (line.Contains("PartPose.offset", StringComparison.Ordinal) &&
                     !line.Contains("offsetAndRotation", StringComparison.Ordinal))
            {
                offsetOnly++;
            }
        }

        return (offsetOnly, offsetAndRotation);
    }

    private static string InferFlatRoot(string javapText)
    {
        // Heuristic: multiple limb/head binds directly on root.getRoot() without nesting body under root.
        var hasBodyOnRoot = javapText.Contains("String body", StringComparison.Ordinal) &&
                            javapText.Contains("getRoot", StringComparison.Ordinal);
        var legOnRoot = javapText.Contains("String right_hind_leg", StringComparison.Ordinal) ||
                        javapText.Contains("String left_hind_leg", StringComparison.Ordinal);
        if (legOnRoot && hasBodyOnRoot)
        {
            var idxBody = javapText.IndexOf("String body", StringComparison.Ordinal);
            var idxLeg = javapText.IndexOf("right_hind_leg", StringComparison.Ordinal);
            if (idxLeg > 0 && idxBody > 0 && Math.Abs(idxLeg - idxBody) < 4000)
            {
                return "Y";
            }
        }

        return "N";
    }

    private static string BuildCsv(string jvm, string factory, (int OffsetOnly, int OffsetAndRotation) stats,
        string flat, string fileName, long bytes) =>
        $"{EscapeCsv(jvm)},{EscapeCsv(factory)},{stats.OffsetOnly},{stats.OffsetAndRotation},{flat},{EscapeCsv(fileName)},{bytes}";

    private static string EscapeCsv(string s)
    {
        if (s.Contains(',') || s.Contains('"'))
        {
            return '"' + s.Replace("\"", "\"\"") + '"';
        }

        return s;
    }

    private static string TruncateUtf8(string s, int maxBytes)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(s) <= maxBytes)
        {
            return s;
        }

        for (var i = s.Length - 1; i > 0; i--)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(s.AsSpan(0, i)) <= maxBytes)
            {
                return s[..i];
            }
        }

        return string.Empty;
    }
}


