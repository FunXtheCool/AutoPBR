using System.Collections.Concurrent;

namespace AutoPBR.Preview;

public static partial class GeometryJavapPoseOracle
{
    private sealed record ResolveOutcome(bool Ok, string? JavapText, string? SourceNote);

    public sealed class Context
    {
        private readonly ConcurrentDictionary<string, Lazy<ResolveOutcome>> _disasmCache = new(StringComparer.Ordinal);

        public Context(
            string repoRoot,
            string versionLabel,
            IReadOnlySet<string> pilotJvmNames,
            string? clientJarPath,
            string? javapExecutable,
            IReadOnlyDictionary<string, GeometryAssemblyPilotJavapSnapshotIndex.PilotSnapshot>? pilotSnapshots = null)
        {
            RepoRoot = repoRoot;
            VersionLabel = versionLabel;
            PilotJvmNames = pilotJvmNames;
            ClientJarPath = clientJarPath;
            JavapExecutable = javapExecutable;
            PilotSnapshots = pilotSnapshots ?? new Dictionary<string, GeometryAssemblyPilotJavapSnapshotIndex.PilotSnapshot>(StringComparer.Ordinal);
            SnapshotDirectory = Path.Combine(repoRoot, "tools", "minecraft-parity", versionLabel, "javap-snapshots");
        }

        public string RepoRoot { get; }
        public string VersionLabel { get; }
        public IReadOnlySet<string> PilotJvmNames { get; }
        public string? ClientJarPath { get; }
        public string? JavapExecutable { get; }
        public IReadOnlyDictionary<string, GeometryAssemblyPilotJavapSnapshotIndex.PilotSnapshot> PilotSnapshots { get; }
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

            var pilotSnapshots = GeometryAssemblyPilotJavapSnapshotIndex.Load(repoRoot, versionLabel);
            return new Context(repoRoot, versionLabel, pilots, jar, FindJavapExecutable(), pilotSnapshots);
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

            PilotSnapshots.TryGetValue(officialJvmName, out var indexed);
            var factoryMethod = indexed?.FactoryMethod;
            poses = Parser.ParseBindingsMerged(outcome.JavapText, SnapshotDirectory, factoryMethod);
            return poses.Count > 0;
        }

        private ResolveOutcome ResolveJavapTextForJvm(string officialJvmName)
        {
            if (PilotSnapshots.TryGetValue(officialJvmName, out var indexed) &&
                !string.IsNullOrEmpty(indexed.SnapshotFile))
            {
                var indexedPath = Path.Combine(SnapshotDirectory, indexed.SnapshotFile);
                if (File.Exists(indexedPath))
                {
                    return new ResolveOutcome(true, File.ReadAllText(indexedPath), $"javap snapshot ({indexed.SnapshotFile})");
                }
            }

            var shortName = officialJvmName[(officialJvmName.LastIndexOf('.') + 1)..];
            foreach (var candidate in new[]
                     {
                         $"{shortName}.createBodyLayer.javap.txt",
                         $"{shortName}.createBodyMesh.javap.txt",
                         $"{shortName}.createSaddleLayer.javap.txt",
                         $"{shortName}.createFurLayer.javap.txt",
                         $"{shortName}.createBabyMesh.javap.txt",
                         $"{shortName}.createBabyLayer.javap.txt",
                     })
            {
                var snapshotPath = Path.Combine(SnapshotDirectory, candidate);
                if (File.Exists(snapshotPath))
                {
                    return new ResolveOutcome(true, File.ReadAllText(snapshotPath), $"javap snapshot ({candidate})");
                }
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
}

