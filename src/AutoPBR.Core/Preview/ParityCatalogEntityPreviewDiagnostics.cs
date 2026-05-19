using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Per-texture parity-catalog preview survey (driver, JVM, IR suppression, motion flags).
/// Used by unit tests for gain tracking; no product UI dependency.
/// </summary>
internal static class ParityCatalogEntityPreviewDiagnostics
{
    internal readonly record struct Row(
        string TexturePath,
        string BuilderMethod,
        bool IsBaby,
        bool BuildSucceeded,
        PreviewMeshDriverKind DriverKind,
        string? ResolvedGeometryJvm,
        bool SuppressesHandFallback,
        string? IrFailureReason,
        GeometryIrParityTier ParityTier,
        bool HasSetupAnimDocument,
        bool SetupAnimWouldEvaluate,
        string? DefinitionAnimationJvm,
        string? ProvenanceDetail);

    internal static Row SurveyPath(
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01 = 0.2f,
        float animationTimeSeconds = 1f,
        bool applyGeometryIrSetupAnimMotion = false)
    {
        var norm = entityTextureAssetPath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var isBaby = CleanRoomEntityModelRuntime.LooksLikeBabyTexture(stem, norm);
        var rule = EntityTextureParityCatalog.ResolveRule(norm, stem);
        var builder = rule?.BuilderMethod ?? "(no rule)";
        var tier = rule is not null ? GeometryIrParityPolicy.GetTier(rule.BuilderMethod) : GeometryIrParityTier.PreferIr;

        string? resolvedJvm = null;
        var suppresses = false;
        string? irFailure = null;
        if (rule is not null)
        {
            if (GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                    profile, rule, norm, stem, isBaby, out var jvm, out _))
            {
                resolvedJvm = jvm;
            }

            suppresses = CleanRoomEntityModelRuntime.ShouldSuppressHandBuiltParityFallback(
                profile, rule, norm, stem, isBaby, out irFailure);
        }

        var definitionJvm = rule is not null
            ? ResolveDefinitionAnimationJvm(rule.BuilderMethod, isBaby)
            : null;

        var hasSetupAnim = false;
        var setupAnimEval = false;
        if (rule is not null)
        {
            CleanRoomEntityModelRuntime.ProbeParityCatalogSetupAnimCapability(
                rule,
                resolvedJvm,
                isBaby,
                animationTimeSeconds,
                idlePhase01,
                out hasSetupAnim,
                out setupAnimEval);
        }

        var runtime = EntityModelRuntimeFactory.Create();
        var buildOk = runtime.TryBuildStaticMesh(
            norm,
            profile,
            idlePhase01,
            animationTimeSeconds,
            out _,
            out var provenance,
            applyGeometryIrSetupAnimMotion);

        if (!buildOk && suppresses && string.IsNullOrEmpty(irFailure))
        {
            irFailure = "suppressed_hand_fallback_build_failed";
        }

        if (buildOk && provenance.Kind == PreviewMeshDriverKind.RuntimeGeometryIrJson &&
            !string.IsNullOrWhiteSpace(provenance.Detail))
        {
            resolvedJvm ??= provenance.Detail;
        }

        if (buildOk && provenance.Kind == PreviewMeshDriverKind.CleanRoom && rule is not null)
        {
            irFailure = CleanRoomEntityModelRuntime.ClassifyParityCatalogGeometryIrFailure(
                profile, rule, norm, stem, isBaby);
        }

        return new Row(
            norm,
            builder,
            isBaby,
            buildOk,
            buildOk ? provenance.Kind : PreviewMeshDriverKind.None,
            resolvedJvm,
            suppresses,
            irFailure,
            tier,
            hasSetupAnim,
            setupAnimEval,
            definitionJvm,
            buildOk ? provenance.Detail : null);
    }

    internal static IReadOnlyList<Row> SurveyAllCatalog(MinecraftNativeProfile profile) =>
        EntityTextureParityCatalog
            .GetCataloguedDiffusePathsWithManifestRules()
            .Select(p => SurveyPath(p, profile))
            .ToArray();

    internal static string FormatGainChecklistTable(IEnumerable<Row> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("path\tbaby\tbuilder\tdriver\tjvm\tsuppress\tir_reason\ttier\tsetup_doc\tsetup_eval\tdef_anim\tbuild_ok");
        foreach (var r in rows)
        {
            sb.Append(r.TexturePath);
            sb.Append('\t');
            sb.Append(r.IsBaby ? "1" : "0");
            sb.Append('\t');
            sb.Append(r.BuilderMethod);
            sb.Append('\t');
            sb.Append(r.DriverKind);
            sb.Append('\t');
            sb.Append(r.ResolvedGeometryJvm ?? "");
            sb.Append('\t');
            sb.Append(r.SuppressesHandFallback ? "1" : "0");
            sb.Append('\t');
            sb.Append(r.IrFailureReason ?? "");
            sb.Append('\t');
            sb.Append(r.ParityTier);
            sb.Append('\t');
            sb.Append(r.HasSetupAnimDocument ? "1" : "0");
            sb.Append('\t');
            sb.Append(r.SetupAnimWouldEvaluate ? "1" : "0");
            sb.Append('\t');
            sb.Append(r.DefinitionAnimationJvm ?? "");
            sb.Append('\t');
            sb.AppendLine(r.BuildSucceeded ? "1" : "0");
        }

        return sb.ToString();
    }

    private static string? ResolveDefinitionAnimationJvm(string builderMethod, bool isBaby)
    {
        string? match = null;
        foreach (var binding in EntityCleanRoomAnimationMap.GetBindingsForParityBuilder(builderMethod))
        {
            if (binding.RestrictToBabyTextures is true && !isBaby)
            {
                continue;
            }

            if (binding.RestrictToBabyTextures is false && isBaby)
            {
                continue;
            }

            match = binding.AnimationOfficialJvmName;
        }

        return match;
    }
}
