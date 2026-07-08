using AutoPBR.Core.Models;
using System.Text.Json;

namespace AutoPBR.Preview.Parity;

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
        string SetupAnimStateSource,
        EntityModelRuntime.GeometryIrLerBasisKind LerBasis,
        bool LerComposeProbeAvailable,
        float LerDefaultLegMinusHeadY,
        float LerRightComposeLegMinusHeadY,
        string LerComposeRecommended,
        string? DefinitionAnimationJvm,
        string? ProvenanceDetail);

    internal static string FormatExplorePlacementLine(
        string normalizedTexturePath,
        string lerBasis,
        bool gpuSkinning,
        float liftY,
        float frameAnimClock,
        bool setupAnimMotion,
        float groundContactY,
        float bodyCentroidY,
        float headCentroidY,
        float legCentroidY) =>
        $"[3D preview] Parity placement: path={normalizedTexturePath} ler={lerBasis} gpuSubject={(gpuSkinning ? 1 : 0)} liftY={liftY:0.####} animClock={frameAnimClock:0.###} setupAnimMotion={(setupAnimMotion ? 1 : 0)} contactY={groundContactY:0.####} bodyY={bodyCentroidY:0.####} headY={headCentroidY:0.####} legY={legCentroidY:0.####} (rebake ctx; see GPU runtime line for shader UBO)";

    public static Row SurveyPath(
        string entityTextureAssetPath,
        MinecraftNativeProfile profile,
        float idlePhase01 = 0.2f,
        float animationTimeSeconds = 1f,
        bool applyGeometryIrSetupAnimMotion = false)
    {
        var norm = entityTextureAssetPath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var isBaby = EntityModelRuntime.LooksLikeBabyTexture(stem, norm);
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

            suppresses = EntityModelRuntime.ShouldSuppressHandBuiltParityFallback(
                profile, rule, norm, stem, isBaby, out irFailure);
        }

        var definitionJvm = rule is not null
            ? ResolveDefinitionAnimationJvm(rule.BuilderMethod, isBaby)
            : null;

        var hasSetupAnim = false;
        var setupAnimEval = false;
        var setupStateSource = "";
        if (rule is not null)
        {
            EntityModelRuntime.ProbeParityCatalogSetupAnimCapability(
                rule,
                resolvedJvm,
                isBaby,
                animationTimeSeconds,
                idlePhase01,
                out hasSetupAnim,
                out setupAnimEval);
            if (hasSetupAnim)
            {
                var modelJvm = SetupAnimParityResolver.ResolveModelJvmForPreview(
                    rule.BuilderMethod,
                    rule.DeobfuscatedModelClass,
                    isBaby,
                    resolvedJvm ?? "");
                _ = EntityModelRuntime.ResolveSetupAnimPreviewStateForTests(
                    modelJvm,
                    animationTimeSeconds,
                    idlePhase01,
                    MathF.Sin(animationTimeSeconds * MathF.PI * 2f * 0.8f),
                    out setupStateSource);
            }
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

        var lerProbeAvailable = false;
        var lerDefaultLegMinusHeadY = 0f;
        var lerRightLegMinusHeadY = 0f;
        var lerRecommended = "";
        if (rule is not null &&
            !string.IsNullOrWhiteSpace(resolvedJvm) &&
            GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                profile,
                rule,
                norm,
                stem,
                isBaby,
                out var probeJvm,
                out var probeRoot))
        {
            probeRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(probeJvm, probeRoot);
            if (TryResolveAtlasDimensionsForLerProbe(rule, probeRoot, out var atlasW, out var atlasH) &&
                TryProbeLerComposeOrdering(probeJvm, probeRoot, atlasW, atlasH, out lerDefaultLegMinusHeadY, out lerRightLegMinusHeadY))
            {
                lerProbeAvailable = true;
                lerRecommended = MathF.Abs(lerDefaultLegMinusHeadY - lerRightLegMinusHeadY) < 1e-4f
                    ? ""
                    : lerRightLegMinusHeadY < lerDefaultLegMinusHeadY
                        ? EntityModelRuntime.GeometryIrLerBasisKind.RightComposeLocalChain.ToString()
                        : EntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot.ToString();
            }
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
            setupStateSource,
            EntityModelRuntime.ResolveGeometryIrLerBasis(resolvedJvm, stem, norm),
            lerProbeAvailable,
            lerDefaultLegMinusHeadY,
            lerRightLegMinusHeadY,
            lerRecommended,
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
        sb.AppendLine("path\tbaby\tbuilder\tdriver\tjvm\tsuppress\tir_reason\ttier\tsetup_doc\tsetup_eval\tsetup_state\tler_basis\tler_probe\tler_default_leg_minus_head\tler_right_leg_minus_head\tler_recommended\tdef_anim\tbuild_ok");
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
            sb.Append(r.SetupAnimStateSource);
            sb.Append('\t');
            sb.Append(r.LerBasis);
            sb.Append('\t');
            sb.Append(r.LerComposeProbeAvailable ? "1" : "0");
            sb.Append('\t');
            sb.Append(r.LerDefaultLegMinusHeadY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(r.LerRightComposeLegMinusHeadY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append('\t');
            sb.Append(r.LerComposeRecommended);
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
        foreach (var binding in EntityParityAnimationMap.GetBindingsForParityBuilder(builderMethod))
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

    private static bool TryResolveAtlasDimensionsForLerProbe(
        EntityTextureParityRule rule,
        JsonElement geometryRoot,
        out int atlasWidth,
        out int atlasHeight)
    {
        atlasWidth = 64;
        atlasHeight = 64;
        if (rule.GeometryIrTextureWidth is > 0 and var rw &&
            rule.GeometryIrTextureHeight is > 0 and var rh)
        {
            atlasWidth = rw;
            atlasHeight = rh;
            return true;
        }

        if (geometryRoot.TryGetProperty("textureWidth", out var tw) &&
            tw.TryGetInt32(out var twi) &&
            twi > 0 &&
            geometryRoot.TryGetProperty("textureHeight", out var th) &&
            th.TryGetInt32(out var thi) &&
            thi > 0)
        {
            atlasWidth = twi;
            atlasHeight = thi;
        }

        return true;
    }

    private static bool TryProbeLerComposeOrdering(
        string officialJvmName,
        JsonElement geometryRoot,
        int atlasWidth,
        int atlasHeight,
        out float defaultLegMinusHeadY,
        out float rightComposeLegMinusHeadY)
    {
        defaultLegMinusHeadY = 0f;
        rightComposeLegMinusHeadY = 0f;
        var defaultMesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTestsWithLerCompose(
            "entity/test",
            officialJvmName,
            atlasWidth,
            atlasHeight,
            geometryRoot,
            lerMirrorRightComposeLocalChain: false,
            out _);
        var rightMesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTestsWithLerCompose(
            "entity/test",
            officialJvmName,
            atlasWidth,
            atlasHeight,
            geometryRoot,
            lerMirrorRightComposeLocalChain: true,
            out _);
        if (defaultMesh is null || rightMesh is null)
        {
            return false;
        }

        if (!TryMeasureLegMinusHeadY(defaultMesh, geometryRoot, atlasWidth, atlasHeight, officialJvmName, out defaultLegMinusHeadY) ||
            !TryMeasureLegMinusHeadY(rightMesh, geometryRoot, atlasWidth, atlasHeight, officialJvmName, out rightComposeLegMinusHeadY))
        {
            return false;
        }

        return true;
    }

    private static bool TryMeasureLegMinusHeadY(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasWidth,
        int atlasHeight,
        string officialJvmName,
        out float legMinusHeadY)
    {
        legMinusHeadY = 0f;
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = System.Numerics.Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasWidth,
            AtlasHeight = atlasHeight,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        if (partIds.Count != mesh.Elements.Count)
        {
            return false;
        }

        float headSum = 0f;
        var headCount = 0;
        float legSum = 0f;
        var legCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            var cy = MeasureElementCenterY(mesh.Elements[i]);
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }

            if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        if (headCount == 0 || legCount == 0)
        {
            return false;
        }

        legMinusHeadY = (legSum / legCount) - (headSum / headCount);
        return true;
    }

    private static float MeasureElementCenterY(ModelElement element)
    {
        var minY = float.PositiveInfinity;
        var maxY = float.NegativeInfinity;
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (element.From[0], element.From[1], element.From[2]),
            (element.To[0], element.From[1], element.From[2]),
            (element.From[0], element.To[1], element.From[2]),
            (element.To[0], element.To[1], element.From[2]),
            (element.From[0], element.From[1], element.To[2]),
            (element.To[0], element.From[1], element.To[2]),
            (element.From[0], element.To[1], element.To[2]),
            (element.To[0], element.To[1], element.To[2]),
        ];
        foreach (var (x, y, z) in corners)
        {
            var w = System.Numerics.Vector3.Transform(
                new System.Numerics.Vector3(x, y, z),
                element.LocalToParent);
            minY = MathF.Min(minY, w.Y);
            maxY = MathF.Max(maxY, w.Y);
        }

        return (minY + maxY) * 0.5f;
    }
}
