using System.Text;
using System.Text.Json.Nodes;

namespace AutoPBR.Preview;

/// <summary>
/// Discovery/report helpers for P6 renderer-state shards. This stays read-only and mirrors the
/// content-linked runtime location used by <see cref="RendererStateDocumentLoader"/>.
/// </summary>
internal static class RendererStateContractDiagnostics
{
    private static readonly string[] JsonParseFailedIssue = ["json_parse_failed"];

    public const string HandPilotSource = "hand-pilot";
    public const string CompilerLiftSource = "compiler-lift";
    public const string UnknownSource = "unknown";

    public const string ClipCycleDriverCategory = "clip-cycle";
    public const string PhaseCycleDriverCategory = "phase-cycle";
    public const string WalkCycleDriverCategory = "walk-cycle";
    public const string ScalarDriverCategory = "scalar-driver";
    public const string UnknownDriverCategory = "unknown";

    internal sealed record RendererStateShardReport(
        string ShardFileName,
        string VersionLabel,
        string OfficialRendererJvmName,
        string RenderStateType,
        string ExtractionStatus,
        string SourceCategory,
        string PreviewDriver,
        string DriverCategory,
        string FieldCategory,
        IReadOnlyList<string> ModelJvmNames,
        IReadOnlyList<string> AnimationStateFields,
        IReadOnlyList<string> ScalarRenderStateFields,
        IReadOnlyList<string> LivingEntityFields,
        IReadOnlyList<string> ContractIssues)
    {
        public bool IsContractOk => ContractIssues.Count == 0;
    }

    public static IReadOnlyList<RendererStateShardReport> Discover() =>
        Discover(RendererStateDocumentLoader.VersionLabel);

    public static IReadOnlyList<RendererStateShardReport> Discover(string versionLabel)
    {
        var dir = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "renderer-state",
            versionLabel);
        return DiscoverDirectory(dir, versionLabel);
    }

    internal static IReadOnlyList<RendererStateShardReport> DiscoverDirectory(
        string directory,
        string expectedVersionLabel)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<RendererStateShardReport>();
        }

        var rows = new List<RendererStateShardReport>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            rows.Add(LoadShard(file, expectedVersionLabel));
        }

        return rows;
    }

    public static string FormatReportTable(IEnumerable<RendererStateShardReport> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("renderer\tdriver\tdriver_category\tsource\tfield_category\tmodels\tanimation_fields\tscalar_fields\tliving_fields\tcontract_ok\tissues");
        foreach (var row in rows)
        {
            sb.Append(Sanitize(row.OfficialRendererJvmName));
            sb.Append('\t');
            sb.Append(Sanitize(row.PreviewDriver));
            sb.Append('\t');
            sb.Append(row.DriverCategory);
            sb.Append('\t');
            sb.Append(row.SourceCategory);
            sb.Append('\t');
            sb.Append(row.FieldCategory);
            sb.Append('\t');
            sb.Append(row.ModelJvmNames.Count);
            sb.Append('\t');
            sb.Append(row.AnimationStateFields.Count);
            sb.Append('\t');
            sb.Append(row.ScalarRenderStateFields.Count);
            sb.Append('\t');
            sb.Append(row.LivingEntityFields.Count);
            sb.Append('\t');
            sb.Append(row.IsContractOk ? "1" : "0");
            sb.Append('\t');
            sb.AppendLine(Sanitize(string.Join(';', row.ContractIssues)));
        }

        return sb.ToString();
    }

    private static RendererStateShardReport LoadShard(string file, string expectedVersionLabel)
    {
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            return BuildReport(Path.GetFileName(file), doc, expectedVersionLabel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return new RendererStateShardReport(
                Path.GetFileName(file),
                "",
                "",
                "",
                "",
                UnknownSource,
                "",
                UnknownDriverCategory,
                "empty",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                JsonParseFailedIssue);
        }
    }

    private static RendererStateShardReport BuildReport(
        string shardFileName,
        JsonObject doc,
        string expectedVersionLabel)
    {
        var issues = new List<string>();
        var versionLabel = ReadString(doc, "versionLabel");
        var renderer = ReadString(doc, "officialJvmName");
        var renderStateType = ReadString(doc, "renderStateType");
        var extractionStatus = ReadString(doc, "extractionStatus");
        var previewDriver = ReadString(doc, "previewDriver");
        var modelJvmNames = ReadStringArray(doc, "modelJvmNames");
        var animationFields = ReadSetupStateFields(doc, "animationStateFields", issues);
        var scalarFields = ReadSetupStateFields(doc, "scalarRenderStateFields", issues);
        var livingFields = ReadStringArray(doc, "livingEntityFields");
        var driverCategory = ClassifyDriver(previewDriver);

        if (ReadSchemaVersion(doc) != 1)
        {
            issues.Add("schemaVersion_not_1");
        }

        if (!string.Equals(versionLabel, expectedVersionLabel, StringComparison.Ordinal))
        {
            issues.Add("versionLabel_mismatch");
        }

        if (string.IsNullOrWhiteSpace(renderer))
        {
            issues.Add("officialJvmName_missing");
        }
        else if (!string.Equals(Path.GetFileNameWithoutExtension(shardFileName), renderer, StringComparison.Ordinal))
        {
            issues.Add("officialJvmName_file_mismatch");
        }

        if (string.IsNullOrWhiteSpace(renderStateType))
        {
            issues.Add("renderStateType_missing");
        }

        if (modelJvmNames.Count == 0)
        {
            issues.Add("modelJvmNames_empty");
        }

        if (string.IsNullOrWhiteSpace(extractionStatus))
        {
            issues.Add("extractionStatus_missing");
        }

        if (string.IsNullOrWhiteSpace(previewDriver))
        {
            issues.Add("previewDriver_missing");
        }
        else if (driverCategory == UnknownDriverCategory)
        {
            issues.Add("previewDriver_unclassified");
        }

        ValidateDriverContract(doc, previewDriver, driverCategory, animationFields, scalarFields, issues);

        return new RendererStateShardReport(
            shardFileName,
            versionLabel,
            renderer,
            renderStateType,
            extractionStatus,
            ClassifySource(extractionStatus),
            previewDriver,
            driverCategory,
            ClassifyFields(animationFields.Count, scalarFields.Count, livingFields.Count),
            modelJvmNames,
            animationFields,
            scalarFields,
            livingFields,
            issues);
    }

    private static void ValidateDriverContract(
        JsonObject doc,
        string previewDriver,
        string driverCategory,
        IReadOnlyList<string> animationFields,
        IReadOnlyList<string> scalarFields,
        List<string> issues)
    {
        if (driverCategory == ClipCycleDriverCategory)
        {
            var clipKeys = ReadObjectKeys(doc, "clipLengthsSeconds");
            if (clipKeys.Count == 0)
            {
                issues.Add("clipLengthsSeconds_empty");
                return;
            }

            foreach (var field in animationFields)
            {
                if (!clipKeys.Contains(field))
                {
                    issues.Add($"clip_missing:{field}");
                }
            }

            return;
        }

        if (string.Equals(previewDriver, "allay_hold_dance_cycle", StringComparison.Ordinal))
        {
            if (scalarFields.Count == 0)
            {
                issues.Add("scalarRenderStateFields_empty");
            }

            var phases = ReadObjectKeys(doc, "phaseLengthsSeconds");
            foreach (var requiredPhase in new[] { "holding", "dance", "spinRamp" })
            {
                if (!phases.Contains(requiredPhase))
                {
                    issues.Add($"phase_missing:{requiredPhase}");
                }
            }

            return;
        }

        if (string.Equals(previewDriver, "nautilus_swim_walk", StringComparison.Ordinal) &&
            !ReadObjectKeys(doc, "clipLengthsSeconds").Contains("swimWalk"))
        {
            issues.Add("clip_missing:swimWalk");
        }

        if (driverCategory == ScalarDriverCategory &&
            ReadObjectKeys(doc, "scalarDefaults").Count == 0)
        {
            issues.Add("scalarDefaults_empty");
        }
    }

    private static int ReadSchemaVersion(JsonObject doc)
    {
        try
        {
            return doc["schemaVersion"]?.GetValue<int>() ?? 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static string ReadString(JsonObject doc, string key) =>
        TryReadString(doc[key]);

    private static string TryReadString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>() ?? "";
        }
        catch (InvalidOperationException)
        {
            return "";
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject doc, string key)
    {
        if (doc[key] is not JsonArray values)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var node in values)
        {
            var value = TryReadString(node);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ReadSetupStateFields(
        JsonObject doc,
        string arrayKey,
        List<string> issues)
    {
        if (doc[arrayKey] is not JsonArray values)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var node in values)
        {
            if (node is not JsonObject field)
            {
                issues.Add($"{arrayKey}_entry_not_object");
                continue;
            }

            var renderStateField = ReadString(field, "renderStateField");
            var setupStateField = ReadString(field, "setupAnimStateField");
            if (string.IsNullOrWhiteSpace(renderStateField) || string.IsNullOrWhiteSpace(setupStateField))
            {
                issues.Add($"{arrayKey}_entry_incomplete");
                continue;
            }

            result.Add(setupStateField);
        }

        return result;
    }

    private static HashSet<string> ReadObjectKeys(JsonObject doc, string key)
    {
        if (doc[key] is not JsonObject obj)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return obj.Select(kvp => kvp.Key).ToHashSet(StringComparer.Ordinal);
    }

    private static string ClassifyDriver(string previewDriver)
    {
        if (string.IsNullOrWhiteSpace(previewDriver))
        {
            return UnknownDriverCategory;
        }

        if (previewDriver.EndsWith("_clip_cycle", StringComparison.Ordinal))
        {
            return ClipCycleDriverCategory;
        }

        return previewDriver switch
        {
            "allay_hold_dance_cycle" => PhaseCycleDriverCategory,
            "chicken_idle_flap" => ScalarDriverCategory,
            "nautilus_swim_walk" => WalkCycleDriverCategory,
            "static_scalar_state" => ScalarDriverCategory,
            _ => UnknownDriverCategory
        };
    }

    private static string ClassifySource(string extractionStatus)
    {
        if (string.Equals(extractionStatus, "partial", StringComparison.OrdinalIgnoreCase) ||
            extractionStatus.Contains("hand", StringComparison.OrdinalIgnoreCase))
        {
            return HandPilotSource;
        }

        if (extractionStatus.Contains("compiler", StringComparison.OrdinalIgnoreCase) ||
            extractionStatus.Contains("lift", StringComparison.OrdinalIgnoreCase))
        {
            return CompilerLiftSource;
        }

        return UnknownSource;
    }

    private static string ClassifyFields(int animationFieldCount, int scalarFieldCount, int livingFieldCount)
    {
        if (animationFieldCount > 0 && scalarFieldCount > 0)
        {
            return "animation+scalar";
        }

        if (animationFieldCount > 0)
        {
            return "animation-state";
        }

        if (scalarFieldCount > 0)
        {
            return "scalar";
        }

        return livingFieldCount > 0 ? "living-only" : "empty";
    }

    private static string Sanitize(string value) =>
        value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
