using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Independent pose oracle: <c>PartPose.offset</c> / <c>offsetAndRotation</c> from javap vs lifted IR shards.
/// Breaks IR ↔ <c>reference_java</c> circular validation for assembly parity (Agent 2C).
/// </summary>
public static partial class GeometryJavapPoseOracle
{
    public const double DefaultPoseTolerance = 0.05;

    /// <summary>Default <c>createBodyMesh(int age, …)</c> first argument when bytecode uses <c>iload_0</c>.</summary>
    public const int DefaultQuadrupedMeshAge = 6;

    public sealed record PartPose(
        double Tx,
        double Ty,
        double Tz,
        double Rx,
        double Ry,
        double Rz);

    public sealed record CompareResult(bool IsMatch, string? Message, int OraclePartCount, int IrPartCount);

    /// <summary>Parses expected per-part poses from full-class <c>javap -c</c> text (all mesh factory methods).</summary>
    public static IReadOnlyDictionary<string, PartPose> ParseExpectedPosesFromJavap(string javapText) =>
        Parser.ParseBindings(javapText);

    public static IReadOnlyDictionary<string, PartPose> ParseExpectedPosesFromJavap(
        string javapText,
        string? snapshotDirectory) =>
        Parser.ParseBindingsMerged(
            javapText,
            snapshotDirectory,
            Parser.InferFactoryMethodFromSnapshot(javapText));

    private sealed class MeshParamContext
    {
        public int MeshAge { get; init; } = DefaultQuadrupedMeshAge;
        public bool SkipDecorativeNose { get; init; }
    }

    private sealed partial class Parser
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
            @"invokestatic\s+#\d+\s+//\s*Method\s+([\w./$]+)\.([\w$]+):\([^)]*\)L[\w/$.]+(?:MeshDefinition|LayerDefinition);",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        /// <summary>Same-class factory/helper invokes omit the owner prefix in javap (e.g. <c>Method createBodyMesh:</c>).</summary>
        private static readonly Regex SameClassStaticInvokeRegex = new(
            @"invokestatic\s+#\d+\s+//\s*Method\s+(?!net/)([\w$]+):\(",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        /// <summary>Shared leg mesh builder loaded from a local (e.g. <c>aload_3</c> or <c>aload 5</c> after <c>astore</c>).</summary>
        private static readonly Regex SharedLegBuilderLoadRegex = new(
            @"^\s*\d+:\s+aload(?:_\d+|\s+\d+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        private static readonly Regex LocalLoadRegex = new(
            @"^\s*\d+:\s+iload(?:_\d+|\s+\d+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        private static readonly Regex SameClassMeshDefinitionInvokeRegex = new(
            @"invokestatic\s+#\d+\s+//\s*Method\s+(?!net[/])([\w$]+):\([^)]*\)L[\w/$.]+(?:MeshDefinition|LayerDefinition);",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        private static readonly Regex SnapshotFactoryMethodHeaderRegex = new(
            @"#\s*Factory method:\s*(\w+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));

        public static string? InferFactoryMethodFromSnapshot(string javapText)
        {
            foreach (var line in javapText.Split('\n'))
            {
                var m = SnapshotFactoryMethodHeaderRegex.Match(line);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }

            return null;
        }

        public static Dictionary<string, PartPose> ParseBindings(string javapText) =>
            ParseBindings(javapText, new MeshParamContext());

        public static Dictionary<string, PartPose> ParseBindings(string javapText, MeshParamContext ctx)
        {
            var lines = FoldWrappedLines(javapText.Split('\n'));
            var poses = new Dictionary<string, PartPose>(StringComparer.Ordinal);
            for (var i = 0; i < lines.Count; i++)
            {
                if (!IsMeshBindingLine(lines[i]))
                {
                    continue;
                }

                if (!TryParseBindingAt(lines, i, out var partId, out var pose, ctx))
                {
                    continue;
                }

                poses[partId] = pose;
            }

            return poses;
        }

        public static Dictionary<string, PartPose> ParseBindingsMerged(
            string hostText,
            string? snapshotDirectory,
            string? factoryMethod) =>
            ParseBindingsMerged(hostText, snapshotDirectory, factoryMethod, inheritedContext: null);

        private static Dictionary<string, PartPose> ParseBindingsMerged(
            string hostText,
            string? snapshotDirectory,
            string? factoryMethod,
            MeshParamContext? inheritedContext)
        {
            var inferredFactory = string.IsNullOrEmpty(factoryMethod)
                ? InferFactoryMethodFromSnapshot(hostText)
                : factoryMethod;
            var scopeText = string.IsNullOrEmpty(inferredFactory)
                ? hostText
                : ExtractMethodText(hostText, inferredFactory) ?? hostText;

            var ctx = inheritedContext ??
                      new MeshParamContext
                      {
                          MeshAge = TryExtractCreateBodyMeshAgeParam(scopeText) ?? DefaultQuadrupedMeshAge,
                          SkipDecorativeNose =
                              hostText.Contains("FelineModel", StringComparison.Ordinal) ||
                              hostText.Contains(".feline.", StringComparison.Ordinal)
                      };

            var merged = string.IsNullOrEmpty(inferredFactory)
                ? ParseBindings(hostText, ctx)
                : ParseBindings(scopeText, ctx);

            MergeSameClassVoidHelpers(hostText, scopeText, merged, ctx);
            MergeKnownSameClassLegHelper(hostText, scopeText, merged, ctx);

            foreach (var helperMethod in FindSameClassMeshDefinitionFactories(scopeText))
            {
                if (string.Equals(helperMethod, inferredFactory, StringComparison.Ordinal))
                {
                    continue;
                }

                var helperMerged = ParseBindingsMerged(hostText, snapshotDirectory, helperMethod, ctx);
                foreach (var (partId, pose) in helperMerged)
                {
                    merged[partId] = pose;
                }
            }

            var factory = TryFindCrossClassMeshFactory(scopeText);
            if (factory is not null && !string.IsNullOrEmpty(snapshotDirectory))
            {
                var shortName = factory.Value.Owner[(factory.Value.Owner.LastIndexOf('.') + 1)..];
                var delegatePath = Path.Combine(snapshotDirectory, $"{shortName}.{factory.Value.Method}.javap.txt");
                if (File.Exists(delegatePath))
                {
                    var delegateMerged = ParseBindingsMerged(
                        File.ReadAllText(delegatePath),
                        snapshotDirectory,
                        factory.Value.Method,
                        ctx);
                    foreach (var (partId, pose) in merged)
                    {
                        delegateMerged[partId] = pose;
                    }

                    merged = delegateMerged;
                }
            }

            return merged;
        }

    }

}
