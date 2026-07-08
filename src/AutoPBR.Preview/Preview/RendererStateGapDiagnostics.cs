using System.Text;
using System.Text.Json.Nodes;

namespace AutoPBR.Preview;

/// <summary>
/// Cross-checks setupAnim state-field usage against P6 renderer-state shards so preview fallbacks are explicit.
/// </summary>
internal static class RendererStateGapDiagnostics
{
    public const string LivingWalkOk = "living_walk_ok";
    public const string HasRendererState = "has_renderer_state";
    public const string NeedsAnimationState = "needs_animation_state";
    public const string NeedsScalarState = "needs_scalar_state";
    public const string NeedsBooleanOrPoseState = "needs_boolean_or_pose_state";
    public const string BlockedLayered = "blocked_layered";
    public const string OutOfScope = "out_of_scope";

    private static readonly HashSet<string> LivingWalkFields = new(StringComparer.Ordinal)
    {
        "ageInTicks",
        "walkAnimationPos",
        "walkAnimationSpeed",
        "limbSwing",
        "limbSwingAmount",
        "xRot",
        "yRot",
        "scale",
        "ageScale",
        "isBaby"
    };

    private static readonly Dictionary<string, string> ModelRendererHints = new(StringComparer.Ordinal)
    {
        ["net.minecraft.client.model.animal.armadillo.ArmadilloModel"] = "net.minecraft.client.renderer.entity.ArmadilloRenderer",
        ["net.minecraft.client.model.animal.armadillo.AdultArmadilloModel"] = "net.minecraft.client.renderer.entity.ArmadilloRenderer",
        ["net.minecraft.client.model.animal.armadillo.BabyArmadilloModel"] = "net.minecraft.client.renderer.entity.ArmadilloRenderer",
        ["net.minecraft.client.model.animal.rabbit.RabbitModel"] = "net.minecraft.client.renderer.entity.RabbitRenderer",
        ["net.minecraft.client.model.animal.rabbit.AdultRabbitModel"] = "net.minecraft.client.renderer.entity.RabbitRenderer",
        ["net.minecraft.client.model.animal.rabbit.BabyRabbitModel"] = "net.minecraft.client.renderer.entity.RabbitRenderer",
        ["net.minecraft.client.model.ambient.BatModel"] = "net.minecraft.client.renderer.entity.BatRenderer",
        ["net.minecraft.client.model.animal.chicken.ChickenModel"] = "net.minecraft.client.renderer.entity.ChickenRenderer",
        ["net.minecraft.client.model.animal.chicken.AdultChickenModel"] = "net.minecraft.client.renderer.entity.ChickenRenderer",
        ["net.minecraft.client.model.animal.chicken.BabyChickenModel"] = "net.minecraft.client.renderer.entity.ChickenRenderer",
        ["net.minecraft.client.model.animal.chicken.ColdChickenModel"] = "net.minecraft.client.renderer.entity.ChickenRenderer",
        ["net.minecraft.client.model.animal.chicken.WarmChickenModel"] = "net.minecraft.client.renderer.entity.ChickenRenderer",
        ["net.minecraft.client.model.animal.fish.CodModel"] = "net.minecraft.client.renderer.entity.CodRenderer",
        ["net.minecraft.client.model.animal.fish.SalmonModel"] = "net.minecraft.client.renderer.entity.SalmonRenderer",
        ["net.minecraft.client.model.animal.fish.TropicalFishLargeModel"] = "net.minecraft.client.renderer.entity.TropicalFishRenderer",
        ["net.minecraft.client.model.animal.fish.TropicalFishSmallModel"] = "net.minecraft.client.renderer.entity.TropicalFishRenderer",
        ["net.minecraft.client.model.monster.dragon.EnderDragonModel"] = "net.minecraft.client.renderer.entity.EnderDragonRenderer",
        ["net.minecraft.client.model.animal.parrot.ParrotModel"] = "net.minecraft.client.renderer.entity.ParrotRenderer"
    };

    private static readonly Dictionary<string, string> HardCaseStatuses = new(StringComparer.Ordinal)
    {
        ["net.minecraft.client.model.monster.dragon.EnderDragonModel"] = BlockedLayered,
        ["net.minecraft.client.model.animal.parrot.ParrotModel"] = NeedsBooleanOrPoseState
    };

    internal sealed record SetupAnimRendererStateGapRow(
        string ModelJvmName,
        string RenderStateType,
        string? RendererJvmName,
        string Status,
        IReadOnlyList<string> RequiredStateFields,
        IReadOnlyList<string> NonLivingWalkStateFields,
        IReadOnlyList<string> PlaybackStateFields,
        IReadOnlyList<string> MissingRendererStateFields,
        string RecommendedAction);

    internal sealed record RendererStatePromotionGateRow(
        string RendererJvmName,
        string ModelJvmName,
        bool HasSetupAnimShard,
        IReadOnlyList<string> MissingFields,
        IReadOnlyList<string> WaivedFields,
        bool IsGateOk)
    {
        public string Status => IsGateOk ? "ok" : "missing_fields";
    }

    public static IReadOnlyList<SetupAnimRendererStateGapRow> Audit() =>
        Audit(RendererStateDocumentLoader.VersionLabel);

    public static IReadOnlyList<SetupAnimRendererStateGapRow> Audit(string versionLabel)
    {
        var setupDir = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "setup-anim",
            versionLabel);
        return AuditDirectory(setupDir, RendererStateContractDiagnostics.Discover(versionLabel));
    }

    internal static IReadOnlyList<SetupAnimRendererStateGapRow> AuditDirectory(
        string setupAnimDirectory,
        IReadOnlyList<RendererStateContractDiagnostics.RendererStateShardReport> rendererRows)
    {
        if (!Directory.Exists(setupAnimDirectory))
        {
            return Array.Empty<SetupAnimRendererStateGapRow>();
        }

        var rendererByModel = rendererRows
            .SelectMany(r => r.ModelJvmNames.Select(m => (Model: m, Renderer: r)))
            .GroupBy(x => x.Model, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Renderer, StringComparer.Ordinal);

        var rows = new List<SetupAnimRendererStateGapRow>();
        foreach (var file in Directory.EnumerateFiles(setupAnimDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            if (TryBuildGapRow(file, rendererByModel, out var row))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    public static IReadOnlyList<RendererStatePromotionGateRow> EvaluatePromotionGates() =>
        EvaluatePromotionGates(RendererStateDocumentLoader.VersionLabel);

    public static IReadOnlyList<RendererStatePromotionGateRow> EvaluatePromotionGates(string versionLabel)
    {
        var setupDir = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "minecraft-native",
            "setup-anim",
            versionLabel);
        return EvaluatePromotionGatesDirectory(setupDir, RendererStateContractDiagnostics.Discover(versionLabel));
    }

    internal static IReadOnlyList<RendererStatePromotionGateRow> EvaluatePromotionGatesDirectory(
        string setupAnimDirectory,
        IReadOnlyList<RendererStateContractDiagnostics.RendererStateShardReport> rendererRows)
    {
        var rows = new List<RendererStatePromotionGateRow>();
        foreach (var renderer in rendererRows)
        {
            var represented = new HashSet<string>(LivingWalkFields, StringComparer.Ordinal);
            represented.UnionWith(renderer.LivingEntityFields);
            represented.UnionWith(renderer.AnimationStateFields);
            represented.UnionWith(renderer.ScalarRenderStateFields);
            var waived = ReadWaivedFields(renderer.OfficialRendererJvmName);

            foreach (var model in renderer.ModelJvmNames)
            {
                var setupPath = Path.Combine(setupAnimDirectory, $"{model}.json");
                if (!File.Exists(setupPath))
                {
                    rows.Add(new RendererStatePromotionGateRow(
                        renderer.OfficialRendererJvmName,
                        model,
                        HasSetupAnimShard: false,
                        MissingFields: Array.Empty<string>(),
                        WaivedFields: waived,
                        IsGateOk: true));
                    continue;
                }

                var setup = JsonNode.Parse(File.ReadAllText(setupPath))!.AsObject();
                var required = CollectRequiredStateFields(setup).Where(f => !LivingWalkFields.Contains(f)).ToArray();
                var missing = required
                    .Where(f => !represented.Contains(f) && !waived.Contains(f))
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                rows.Add(new RendererStatePromotionGateRow(
                    renderer.OfficialRendererJvmName,
                    model,
                    HasSetupAnimShard: true,
                    MissingFields: missing,
                    WaivedFields: waived,
                    IsGateOk: missing.Length == 0));
            }
        }

        return rows;
    }

    public static string FormatGapAuditTable(IEnumerable<SetupAnimRendererStateGapRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("model\trenderer\tstatus\trequired_fields\tnon_living_fields\tplayback_fields\tmissing_fields\taction");
        foreach (var row in rows)
        {
            sb.Append(Sanitize(row.ModelJvmName));
            sb.Append('\t');
            sb.Append(Sanitize(row.RendererJvmName ?? ""));
            sb.Append('\t');
            sb.Append(row.Status);
            sb.Append('\t');
            sb.Append(string.Join(';', row.RequiredStateFields));
            sb.Append('\t');
            sb.Append(string.Join(';', row.NonLivingWalkStateFields));
            sb.Append('\t');
            sb.Append(string.Join(';', row.PlaybackStateFields));
            sb.Append('\t');
            sb.Append(string.Join(';', row.MissingRendererStateFields));
            sb.Append('\t');
            sb.AppendLine(row.RecommendedAction);
        }

        return sb.ToString();
    }

    private static bool TryBuildGapRow(
        string setupAnimFile,
        Dictionary<string, RendererStateContractDiagnostics.RendererStateShardReport> rendererByModel,
        out SetupAnimRendererStateGapRow row)
    {
        row = null!;
        JsonObject setup;
        try
        {
            setup = JsonNode.Parse(File.ReadAllText(setupAnimFile))!.AsObject();
        }
        catch
        {
            return false;
        }

        var model = (string?)setup["officialJvmName"] ?? "";
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        var renderStateType = (string?)setup["renderStateType"] ?? "";
        var required = CollectRequiredStateFields(setup);
        var nonLiving = required.Where(f => !LivingWalkFields.Contains(f)).ToArray();
        var playback = CollectPlaybackStateFields(setup);
        var hasRendererState = rendererByModel.TryGetValue(model, out var renderer);
        var rendererName = hasRendererState ? renderer!.OfficialRendererJvmName : ResolveRendererHint(model, renderStateType);
        var missing = hasRendererState
            ? Array.Empty<string>()
            : nonLiving;
        var status = ClassifyStatus(model, nonLiving, playback, hasRendererState);
        row = new SetupAnimRendererStateGapRow(
            model,
            renderStateType,
            rendererName,
            status,
            required,
            nonLiving,
            playback,
            missing,
            RecommendAction(status));
        return true;
    }

    private static string? ResolveRendererHint(string modelJvmName, string renderStateType)
    {
        if (ModelRendererHints.TryGetValue(modelJvmName, out var renderer))
        {
            return renderer;
        }

        if (renderStateType.StartsWith("net.minecraft.client.renderer.entity.state.", StringComparison.Ordinal) &&
            renderStateType.EndsWith("RenderState", StringComparison.Ordinal))
        {
            var stateName = renderStateType["net.minecraft.client.renderer.entity.state.".Length..^"RenderState".Length];
            return $"net.minecraft.client.renderer.entity.{stateName}Renderer";
        }

        return null;
    }

    private static string ClassifyStatus(
        string modelJvmName,
        string[] nonLivingFields,
        string[] playbackFields,
        bool hasRendererState)
    {
        if (hasRendererState)
        {
            return HasRendererState;
        }

        if (HardCaseStatuses.TryGetValue(modelJvmName, out var hardStatus))
        {
            return hardStatus;
        }

        if (nonLivingFields.Length == 0)
        {
            return LivingWalkOk;
        }

        if (playbackFields.Length > 0)
        {
            return NeedsAnimationState;
        }

        return nonLivingFields.Any(f => f.StartsWith("is", StringComparison.Ordinal) || f.Contains("Pose", StringComparison.OrdinalIgnoreCase))
            ? NeedsBooleanOrPoseState
            : NeedsScalarState;
    }

    private static string RecommendAction(string status) =>
        status switch
        {
            HasRendererState => "keep_promotion_gate",
            LivingWalkOk => "fallback_ok",
            NeedsAnimationState => "add_hand_pilot_or_compiler_copyFrom",
            NeedsScalarState => "add_scalar_driver_or_compiler_putfield",
            NeedsBooleanOrPoseState => "add_boolean_pose_driver_or_waiver",
            BlockedLayered => "defer_layered_renderer_compiler",
            _ => "document_waiver"
        };

    private static string[] CollectRequiredStateFields(JsonObject setup)
    {
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        CollectStateFieldsRecursive(setup, fields);
        foreach (var playback in CollectPlaybackStateFields(setup))
        {
            fields.Add(playback);
        }

        return fields.ToArray();
    }

    private static string[] CollectPlaybackStateFields(JsonObject setup)
    {
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        if (setup["playbackSteps"] is not JsonArray playback)
        {
            return Array.Empty<string>();
        }

        foreach (var node in playback)
        {
            if (node is JsonObject step && step["stateField"]?.GetValue<string>() is { Length: > 0 } stateField)
            {
                fields.Add(stateField);
            }
        }

        return fields.ToArray();
    }

    private static void CollectStateFieldsRecursive(JsonNode? node, ISet<string> fields)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (string.Equals(key, "state", StringComparison.Ordinal) &&
                        value?.GetValue<string>() is { Length: > 0 } state)
                    {
                        fields.Add(state);
                    }
                    else
                    {
                        CollectStateFieldsRecursive(value, fields);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    CollectStateFieldsRecursive(item, fields);
                }

                break;
        }
    }

    private static string[] ReadWaivedFields(string officialRendererJvmName)
    {
        if (!RendererStateDocumentLoader.TryLoadByRenderer(officialRendererJvmName, out var doc) ||
            doc["waivedSetupAnimStateFields"] is not JsonArray values)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(v => v?.GetValue<string>())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToArray();
    }

    private static string Sanitize(string value) =>
        value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
