using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
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
            GeometryIrPartWorldPoseIndex.Build(geometryRoot),
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
        GeometryIrMeshEmitOptions emitOptions)
    {
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
            GeometryIrPartWorldPoseIndex.Build(geometryRoot),
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
