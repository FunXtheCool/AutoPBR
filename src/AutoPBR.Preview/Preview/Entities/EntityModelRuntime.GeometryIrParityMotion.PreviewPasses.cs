using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Models;

namespace AutoPBR.Preview.Entities;

internal sealed partial class EntityModelRuntime
{
    /// <summary>Parity-catalog: apply idle preview animation drivers to IR-emitted chicken elements.</summary>

    internal static void ApplyChickenGeometryIrPreviewAnimPassForTests(
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        bool isBaby,
        float headPitchRad,
        float headYawRad,
        float wingZRadians,
        float rightLegPitchRad,
        float leftLegPitchRad,
        GeometryIrMeshEmitOptions emitOptions) =>
        ApplyChickenGeometryIrPreviewAnimPass(
            merged,
            geometryRoot,
            isBaby,
            headPitchRad,
            headYawRad,
            wingZRadians,
            rightLegPitchRad,
            leftLegPitchRad,
            emitOptions);

    private static void ApplyChickenGeometryIrPreviewAnimPass(

        MergedJavaBlockModel merged,

        JsonElement geometryRoot,

        bool _,

        float headPitchRad,

        float headYawRad,

        float wingZRadians,

        float rightLegPitchRad,

        float leftLegPitchRad,

        GeometryIrMeshEmitOptions emitOptions)

    {

        var pose = new VanillaSetupAnimRuntime.PoseResult
        {
            Parts =
            {
                ["head"] = new VanillaSetupAnimRuntime.PartPose
                {
                    XRot = headPitchRad,
                    YRot = headYawRad,
                    Assigned = VanillaSetupAnimRuntime.PartPoseChannel.XRot |
                               VanillaSetupAnimRuntime.PartPoseChannel.YRot,
                },
                ["rightWing"] = new VanillaSetupAnimRuntime.PartPose
                {
                    ZRot = wingZRadians,
                    Assigned = VanillaSetupAnimRuntime.PartPoseChannel.ZRot,
                },
                ["leftWing"] = new VanillaSetupAnimRuntime.PartPose
                {
                    ZRot = -wingZRadians,
                    Assigned = VanillaSetupAnimRuntime.PartPoseChannel.ZRot,
                },
                ["rightLeg"] = new VanillaSetupAnimRuntime.PartPose
                {
                    XRot = rightLegPitchRad,
                    Assigned = VanillaSetupAnimRuntime.PartPoseChannel.XRot,
                },
                ["leftLeg"] = new VanillaSetupAnimRuntime.PartPose
                {
                    XRot = leftLegPitchRad,
                    Assigned = VanillaSetupAnimRuntime.PartPoseChannel.XRot,
                },
            },
        };



        _ = ApplySetupAnimToGeometryIrMesh(
            merged,
            geometryRoot,
            pose,
            GeometryIrPartWorldPoseIndex.Build(geometryRoot, emitOptions),
            emitOptions);

    }



    private static bool TryApplySetupAnimGeometryIrPreviewPass(
        EntityTextureParityRule parityRule,
        string geometryIrOfficialJvm,
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        bool isBaby,
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        GeometryIrMeshEmitOptions emitOptions,
        string? normalizedAssetPath = null)
    {
        if (string.Equals(parityRule.BuilderMethod, "ChestEntity", StringComparison.Ordinal))
        {
            // Block chest atlas preview uses closed bind pose; setupAnim forces lid.xRot = π/2.
            return false;
        }

        if (string.Equals(parityRule.BuilderMethod, "Ghast", StringComparison.Ordinal) ||
            string.Equals(parityRule.BuilderMethod, "HappyGhast", StringComparison.Ordinal))
        {
            ApplyGhastFamilyAnimateTentaclesGeometryIrPreviewPass(
                merged,
                geometryRoot,
                animationTimeSeconds,
                emitOptions);
            return true;
        }

        if (string.Equals(parityRule.BuilderMethod, "Dolphin", StringComparison.Ordinal))
        {
            ApplyDolphinGeometryIrSetupAnimPreviewPass(
                merged,
                geometryRoot,
                parityRule,
                geometryIrOfficialJvm,
                isBaby,
                animationTimeSeconds,
                idlePhase01,
                wave,
                emitOptions);
            return true;
        }

        if (EntityPreviewPoseCatalog.IsIllagerBuilderMethod(parityRule.BuilderMethod) &&
            !string.IsNullOrWhiteSpace(normalizedAssetPath))
        {
            return ApplyIllagerGeometryIrSetupAnimPreviewPass(
                parityRule,
                geometryIrOfficialJvm,
                merged,
                geometryRoot,
                isBaby,
                animationTimeSeconds,
                idlePhase01,
                wave,
                emitOptions,
                normalizedAssetPath);
        }

        if (EntityPreviewPoseCatalog.IsHumanoidPoseBuilderMethod(parityRule.BuilderMethod) ||
            GeometryIrHumanoidLayerMeshPreviewPolicy.UsesHumanoidArmPosePreviewPass(
                parityRule.BuilderMethod,
                geometryIrOfficialJvm))
        {
            return ApplyHumanoidGeometryIrSetupAnimPreviewPass(
                parityRule,
                geometryIrOfficialJvm,
                merged,
                geometryRoot,
                isBaby,
                animationTimeSeconds,
                idlePhase01,
                wave,
                emitOptions);
        }

        var modelJvm = SetupAnimParityResolver.ResolveModelJvmForPreview(
            parityRule.BuilderMethod,
            parityRule.DeobfuscatedModelClass,
            isBaby,
            geometryIrOfficialJvm);
        if (!SetupAnimDocumentLoader.TryLoad(modelJvm, out _))
        {
            return false;
        }

        var baselineParts = BuildSetupAnimBaselineParts(geometryRoot);
        var state = ResolveSetupAnimPreviewState(modelJvm, animationTimeSeconds, idlePhase01, wave, out _);
        if (!VanillaSetupAnimRuntime.TryEvaluate(
                modelJvm,
                state,
                animationTimeSeconds,
                out var pose,
                parityRule.BuilderMethod,
                isBaby,
                baselineParts))
        {
            return false;
        }

        StripUnsafeFlatQuadrupedPeerPositionChannels(modelJvm, pose);

        return ApplySetupAnimToGeometryIrMesh(
            merged,
            geometryRoot,
            pose,
            GeometryIrPartWorldPoseIndex.Build(geometryRoot, emitOptions),
            emitOptions);
    }

    private static bool ApplyIllagerGeometryIrSetupAnimPreviewPass(
        EntityTextureParityRule parityRule,
        string geometryIrOfficialJvm,
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        bool isBaby,
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        GeometryIrMeshEmitOptions emitOptions,
        string normalizedAssetPath)
    {
        var modelJvm = SetupAnimParityResolver.ResolveModelJvmForPreview(
            parityRule.BuilderMethod,
            parityRule.DeobfuscatedModelClass,
            isBaby,
            geometryIrOfficialJvm);
        var baselineParts = BuildSetupAnimBaselineParts(geometryRoot);
        var state = ResolveSetupAnimPreviewState(modelJvm, animationTimeSeconds, idlePhase01, wave, out _);
        if (SetupAnimDocumentLoader.TryLoad(modelJvm, out _) &&
            VanillaSetupAnimRuntime.TryEvaluate(
                modelJvm,
                state,
                animationTimeSeconds,
                out var pose,
                parityRule.BuilderMethod,
                isBaby,
                baselineParts))
        {
            IllagerPreviewPoseSupport.StripSetupAnimArmChannels(pose);
            _ = ApplySetupAnimToGeometryIrMesh(
                merged,
                geometryRoot,
                pose,
                GeometryIrPartWorldPoseIndex.Build(geometryRoot, emitOptions),
                emitOptions);
        }

        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var armPose = EntityPreviewPoseCatalog.ResolveEffectiveIllagerArmPose(
            norm,
            parityRule.BuilderMethod,
            EntityPreviewBuildContext.CurrentPoseId);
        return IllagerPreviewPoseSupport.TryApplyArmPoseToGeometryIrMesh(
            merged,
            geometryRoot,
            emitOptions,
            armPose,
            idlePhase01,
            animationTimeSeconds,
            wave);
    }

    private static bool ApplyHumanoidGeometryIrSetupAnimPreviewPass(
        EntityTextureParityRule parityRule,
        string geometryIrOfficialJvm,
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        bool isBaby,
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        GeometryIrMeshEmitOptions emitOptions)
    {
        var modelJvm = SetupAnimParityResolver.ResolveModelJvmForPreview(
            parityRule.BuilderMethod,
            parityRule.DeobfuscatedModelClass,
            isBaby,
            geometryIrOfficialJvm);
        var baselineParts = BuildSetupAnimBaselineParts(geometryRoot);
        var state = ResolveSetupAnimPreviewState(modelJvm, animationTimeSeconds, idlePhase01, wave, out _);
        if (SetupAnimDocumentLoader.TryLoad(modelJvm, out _) &&
            VanillaSetupAnimRuntime.TryEvaluate(
                modelJvm,
                state,
                animationTimeSeconds,
                out var pose,
                parityRule.BuilderMethod,
                isBaby,
                baselineParts))
        {
            HumanoidPreviewPoseSupport.StripSetupAnimArmChannels(pose);
            if (GeometryIrHumanoidLayerMeshPreviewPolicy.ShouldStripSetupAnimBodyLegHeadChannels(
                    parityRule.BuilderMethod,
                    geometryIrOfficialJvm))
            {
                HumanoidPreviewPoseSupport.StripSetupAnimBodyLegChannels(pose);
            }

            _ = ApplySetupAnimToGeometryIrMesh(
                merged,
                geometryRoot,
                pose,
                GeometryIrPartWorldPoseIndex.Build(geometryRoot, emitOptions),
                emitOptions);
        }

        var armPose = EntityPreviewPoseCatalog.ResolveEffectiveHumanoidArmPose(
            parityRule.BuilderMethod,
            EntityPreviewBuildContext.CurrentPoseId);
        return ApplyHumanoidGeometryIrArmPosePreviewPass(
            parityRule,
            geometryIrOfficialJvm,
            merged,
            geometryRoot,
            armPose,
            idlePhase01,
            animationTimeSeconds,
            wave,
            emitOptions);
    }

    private static bool ApplyHumanoidGeometryIrArmPosePreviewPass(
        EntityTextureParityRule parityRule,
        string geometryIrOfficialJvm,
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        GeometryIrMeshEmitOptions emitOptions)
    {
        var armPose = EntityPreviewPoseCatalog.ResolveEffectiveHumanoidArmPose(
            parityRule.BuilderMethod,
            EntityPreviewBuildContext.CurrentPoseId);
        return ApplyHumanoidGeometryIrArmPosePreviewPass(
            parityRule,
            geometryIrOfficialJvm,
            merged,
            geometryRoot,
            armPose,
            idlePhase01,
            animationTimeSeconds,
            wave,
            emitOptions);
    }

    private static bool ApplyHumanoidGeometryIrArmPosePreviewPass(
        EntityTextureParityRule parityRule,
        string geometryIrOfficialJvm,
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        EntityHumanoidPreviewArmPose armPose,
        float idlePhase01,
        float animationTimeSeconds,
        float wave,
        GeometryIrMeshEmitOptions emitOptions)
    {
        if (!EntityPreviewPoseCatalog.IsHumanoidPoseBuilderMethod(parityRule.BuilderMethod) &&
            !GeometryIrHumanoidLayerMeshPreviewPolicy.UsesHumanoidArmPosePreviewPass(
                parityRule.BuilderMethod,
                geometryIrOfficialJvm))
        {
            return false;
        }

        return HumanoidPreviewPoseSupport.TryApplyArmPoseToGeometryIrMesh(
            merged,
            geometryRoot,
            emitOptions,
            armPose,
            idlePhase01,
            animationTimeSeconds,
            wave,
            IsBabyGeometryIrOfficialJvm(geometryIrOfficialJvm));
    }

    private static bool IsBabyGeometryIrOfficialJvm(string? geometryIrOfficialJvm) =>
        !string.IsNullOrWhiteSpace(geometryIrOfficialJvm) &&
        geometryIrOfficialJvm.Contains("Baby", StringComparison.Ordinal);

    /// <summary>
    /// <c>GhastModel.setupAnim</c> only calls <c>animateTentacles</c> (setup-anim IR assignments are empty).
    /// </summary>
    private static void ApplyGhastFamilyAnimateTentaclesGeometryIrPreviewPass(
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        float animationTimeSeconds,
        GeometryIrMeshEmitOptions emitOptions)
    {
        // ModelPart xRot in row-affine emit: part-local rotation premultiplies LocalToParent (Rx * LTP).
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, emitOptions);
        var count = Math.Min(merged.Elements.Count, partIds.Count);
        for (var i = 0; i < count; i++)
        {
            if (!GeometryIrEmitPolicy.TryParseGhastFamilyTentacleIndex(partIds[i], out var tentacleIndex))
            {
                continue;
            }

            var pitch = GeometryIrEmitPolicy.ComputeGhastAnimateTentaclesXRot(tentacleIndex, animationTimeSeconds);
            var e = merged.Elements[i];
            merged.Elements[i] = new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = EntityParityTemplate.Mul(EntityParityTemplate.Rx(pitch), e.LocalToParent),
                DepthLayerKind = e.DepthLayerKind,
                LayerOrdinal = e.LayerOrdinal,
                CastsShadow = e.CastsShadow,
                ShellInflateTexels = e.ShellInflateTexels,
                EnableParallax = e.EnableParallax,
                MirrorCuboidUv = e.MirrorCuboidUv,
                BakeAtlasWidth = e.BakeAtlasWidth,
                BakeAtlasHeight = e.BakeAtlasHeight,
            };
        }
    }

    /// <summary>
    /// Lifted dolphin setup-anim IR omits the <c>if (isMoving)</c> guard from javap; apply swim channels only when moving.
    /// </summary>
    private static void ApplyDolphinGeometryIrSetupAnimPreviewPass(
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        EntityTextureParityRule parityRule,
        string geometryIrOfficialJvm,
        bool isBaby,
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        GeometryIrMeshEmitOptions emitOptions)
    {
        var modelJvm = SetupAnimParityResolver.ResolveModelJvmForPreview(
            parityRule.BuilderMethod,
            parityRule.DeobfuscatedModelClass,
            isBaby,
            geometryIrOfficialJvm);
        var state = ResolveSetupAnimPreviewState(modelJvm, animationTimeSeconds, idlePhase01, wave, out _);
        var pose = new VanillaSetupAnimRuntime.PoseResult();
        GeometryIrEmitPolicy.ApplyDolphinSetupAnimPose(state, pose);
        _ = ApplySetupAnimToGeometryIrMesh(
            merged,
            geometryRoot,
            pose,
            GeometryIrPartWorldPoseIndex.Build(geometryRoot, emitOptions),
            emitOptions);
    }

    internal static IReadOnlyDictionary<string, float> ResolveSetupAnimPreviewStateForTests(
        string modelJvm,
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        out string source) =>
        ResolveSetupAnimPreviewState(modelJvm, animationTimeSeconds, idlePhase01, wave, out source);

    private static IReadOnlyDictionary<string, float> ResolveSetupAnimPreviewState(
        string modelJvm,
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        out string source)
    {
        if (RendererStateDocumentLoader.TryLoadForModel(modelJvm, out var rendererState))
        {
            source = "renderer-state";
            return RendererStatePreviewResolver.Synthesize(rendererState, animationTimeSeconds, idlePhase01, wave);
        }

        if (EntityPreviewSizeCatalog.IsSlimeFamilyModelJvm(modelJvm))
        {
            source = "slime-family";
            var state = new Dictionary<string, float>(
                PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave));
            var size = EntityPreviewSizeCatalog.ResolveEffectiveSize(EntityPreviewBuildContext.CurrentSizeId);
            state["size"] = size;
            state["squish"] = MathF.Max(0f, wave);
            return state;
        }

        source = "living-walk";
        return PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave);
    }



    private static bool IsChickenHeadFamilyPartId(string partId) =>

        string.Equals(partId, "head", StringComparison.Ordinal) ||

        string.Equals(partId, "beak", StringComparison.Ordinal) ||

        string.Equals(partId, "red_thing", StringComparison.Ordinal);

    /// <summary>
    /// Flat quadruped setupAnim peer position writes are parent-local aliases and can scatter when applied as world deltas.
    /// Keep direct/self positional channels; strip only x/y/z assignments sourced from partPeer expressions.
    /// </summary>
    private static void StripUnsafeFlatQuadrupedPeerPositionChannels(string modelJvm, VanillaSetupAnimRuntime.PoseResult pose)
    {
        if (!UsesFlatPartPoseOffsetQuadrupedJvm(modelJvm))
        {
            return;
        }

        if (!TryCollectPeerPositionChannels(modelJvm, out var stripMap))
        {
            return;
        }

        foreach (var (partField, mask) in stripMap)
        {
            if (pose.Parts.TryGetValue(partField, out var partPose))
            {
                partPose.Assigned &= ~mask;
            }
        }
    }

    private static bool TryCollectPeerPositionChannels(
        string modelJvm,
        out Dictionary<string, VanillaSetupAnimRuntime.PartPoseChannel> stripMap)
    {
        stripMap = new Dictionary<string, VanillaSetupAnimRuntime.PartPoseChannel>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        return CollectPeerPositionChannelsRecursive(modelJvm, visited, stripMap);
    }

    private static bool CollectPeerPositionChannelsRecursive(
        string modelJvm,
        HashSet<string> visited,
        Dictionary<string, VanillaSetupAnimRuntime.PartPoseChannel> stripMap)
    {
        if (!visited.Add(modelJvm) || !SetupAnimDocumentLoader.TryLoad(modelJvm, out var doc))
        {
            return false;
        }

        var any = false;
        if (doc["assignments"] is JsonArray assignments)
        {
            foreach (var n in assignments)
            {
                if (n is not JsonObject a)
                {
                    continue;
                }

                var partField = (string?)a["partField"] ?? "";
                var property = (string?)a["property"] ?? "";
                if (string.IsNullOrWhiteSpace(partField) || !TryMapPositionPropertyToChannel(property, out var channel))
                {
                    continue;
                }

                if (a["expr"] is JsonObject expr && expr["partPeer"] is not null)
                {
                    stripMap[partField] = stripMap.TryGetValue(partField, out var prev) ? (prev | channel) : channel;
                    any = true;
                }
            }
        }

        if (doc["inheritsSetupAnimFrom"] is JsonValue inh)
        {
            var parent = inh.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(parent))
            {
                any |= CollectPeerPositionChannelsRecursive(parent, visited, stripMap);
            }
        }

        return any;
    }

    private static bool TryMapPositionPropertyToChannel(
        string property,
        out VanillaSetupAnimRuntime.PartPoseChannel channel)
    {
        channel = property switch
        {
            "x" => VanillaSetupAnimRuntime.PartPoseChannel.X,
            "y" => VanillaSetupAnimRuntime.PartPoseChannel.Y,
            "z" => VanillaSetupAnimRuntime.PartPoseChannel.Z,
            _ => VanillaSetupAnimRuntime.PartPoseChannel.None,
        };
        return channel != VanillaSetupAnimRuntime.PartPoseChannel.None;
    }

    internal static bool TryHasFlatQuadrupedPeerPositionAssignmentsForTests(string modelJvm) =>
        UsesFlatPartPoseOffsetQuadrupedJvm(modelJvm) &&
        TryCollectPeerPositionChannels(modelJvm, out var stripMap) &&
        stripMap.Count > 0;
    
    internal static bool TryHasFlatQuadrupedPeerPositionAssignmentsForTests(
        string modelJvm,
        out Dictionary<string, VanillaSetupAnimRuntime.PartPoseChannel> stripMap)
    {
        stripMap = new Dictionary<string, VanillaSetupAnimRuntime.PartPoseChannel>(StringComparer.Ordinal);
        return UsesFlatPartPoseOffsetQuadrupedJvm(modelJvm) &&
               TryCollectPeerPositionChannels(modelJvm, out stripMap) &&
               stripMap.Count > 0;
    }

    private static Dictionary<string, VanillaSetupAnimRuntime.PartPose> BuildSetupAnimBaselineParts(JsonElement geometryRoot)
    {
        var map = new Dictionary<string, VanillaSetupAnimRuntime.PartPose>(StringComparer.Ordinal);
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var root in roots.EnumerateArray())
        {
            CollectBaselineRecursive(root, map);
        }

        return map;
    }

    private static void CollectBaselineRecursive(
        JsonElement part,
        Dictionary<string, VanillaSetupAnimRuntime.PartPose> map)
    {
        var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        if (!string.IsNullOrWhiteSpace(partId))
        {
            var pose = new VanillaSetupAnimRuntime.PartPose();
            if (part.TryGetProperty("pose", out var poseEl))
            {
                if (poseEl.TryGetProperty("translation", out var t) &&
                    t.ValueKind == JsonValueKind.Array &&
                    t.GetArrayLength() >= 3)
                {
                    pose.X = (float)t[0].GetDouble();
                    pose.Y = (float)t[1].GetDouble();
                    pose.Z = (float)t[2].GetDouble();
                }

                if (poseEl.TryGetProperty("rotationEulerRad", out var r) &&
                    r.ValueKind == JsonValueKind.Array &&
                    r.GetArrayLength() >= 3)
                {
                    pose.XRot = (float)r[0].GetDouble();
                    pose.YRot = (float)r[1].GetDouble();
                    pose.ZRot = (float)r[2].GetDouble();
                }
            }

            map[partId] = pose;
            if (TryResolveSetupAnimPartField(partId, out var modelPartField))
            {
                map[modelPartField] = pose;
            }
        }

        if (!part.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var child in children.EnumerateArray())
        {
            CollectBaselineRecursive(child, map);
        }
    }

    /// <summary>Survey helper: whether setupAnim bytecode exists and evaluates for a catalog rule.</summary>
    internal static void ProbeParityCatalogSetupAnimCapability(
        EntityTextureParityRule parityRule,
        string? resolvedGeometryJvm,
        bool isBaby,
        float animationTimeSeconds,
        float idlePhase01,
        out bool hasSetupAnimDocument,
        out bool setupAnimWouldEvaluate)
    {
        hasSetupAnimDocument = false;
        setupAnimWouldEvaluate = false;
        var modelJvm = !string.IsNullOrWhiteSpace(parityRule.DeobfuscatedModelClass)
            ? parityRule.DeobfuscatedModelClass
            : resolvedGeometryJvm ?? "";
        if (string.IsNullOrWhiteSpace(modelJvm) || !SetupAnimDocumentLoader.TryLoadOk(modelJvm, out _))
        {
            return;
        }

        hasSetupAnimDocument = true;
        var wave = Wave(animationTimeSeconds, 0.8f);
        var state = ResolveSetupAnimPreviewState(modelJvm, animationTimeSeconds, idlePhase01, wave, out _);
        setupAnimWouldEvaluate = VanillaSetupAnimRuntime.TryEvaluate(
            modelJvm,
            state,
            animationTimeSeconds,
            out _,
            parityRule.BuilderMethod,
            isBaby);
    }

}
