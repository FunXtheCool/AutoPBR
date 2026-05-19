using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Evaluates lifted setupAnim IR (procedural assignments + AnimationDefinition playback).
/// Replaces hand-written <c>Compute*</c> helpers and <see cref="VanillaAnimationIrPreviewSampler"/> entry points.
/// </summary>
internal static class VanillaSetupAnimRuntime
{
    private static readonly ConcurrentDictionary<string, JsonObject?> EffectiveRulesCache = new(StringComparer.OrdinalIgnoreCase);

    public sealed class PoseResult
    {
        public Dictionary<string, PartPose> Parts { get; } = new(StringComparer.Ordinal);
    }

    public sealed class PartPose
    {
        public float XRot { get; set; }
        public float YRot { get; set; }
        public float ZRot { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public bool? Visible { get; set; }
    }

    public static bool TryEvaluate(
        string modelOfficialJvmName,
        IReadOnlyDictionary<string, float> renderState,
        float animationTimeSeconds,
        out PoseResult pose,
        string? parityBuilderMethod = null,
        bool isBaby = false)
    {
        pose = new PoseResult();
        if (!TryGetEffectiveRules(modelOfficialJvmName, out var rules))
        {
            return false;
        }

        if (rules["assignments"] is JsonArray assignments)
        {
            foreach (var n in assignments)
            {
                if (n is not JsonObject a)
                {
                    continue;
                }

                var part = (string?)a["partField"] ?? "";
                var prop = (string?)a["property"] ?? "";
                if (string.IsNullOrEmpty(part) || string.IsNullOrEmpty(prop))
                {
                    continue;
                }

                if (!pose.Parts.TryGetValue(part, out var partPose))
                {
                    partPose = new PartPose();
                    pose.Parts[part] = partPose;
                }

                var v = SetupAnimExprEvaluator.Evaluate(a["expr"], renderState);
                ApplyProperty(partPose, prop, v);
            }
        }

        if (rules["playbackSteps"] is JsonArray steps)
        {
            ApplyPlaybackSteps(modelOfficialJvmName, steps, renderState, animationTimeSeconds, pose, parityBuilderMethod, isBaby);
        }

        return pose.Parts.Count > 0;
    }

    public static bool TryGetLegXRots(
        string modelOfficialJvmName,
        IReadOnlyDictionary<string, float> renderState,
        out float rightHind,
        out float leftHind,
        out float rightFront,
        out float leftFront)
    {
        rightHind = leftHind = rightFront = leftFront = 0f;
        if (!TryEvaluate(modelOfficialJvmName, renderState, renderState.TryGetValue("ageInTicks", out var age) ? age / 20f : 0f,
                out var pose))
        {
            return false;
        }

        rightHind = pose.Parts.GetValueOrDefault("rightHindLeg")?.XRot ?? 0f;
        leftHind = pose.Parts.GetValueOrDefault("leftHindLeg")?.XRot ?? 0f;
        rightFront = pose.Parts.GetValueOrDefault("rightFrontLeg")?.XRot ?? 0f;
        leftFront = pose.Parts.GetValueOrDefault("leftFrontLeg")?.XRot ?? 0f;
        return true;
    }

    /// <summary>True when leg xRot changes between rest and a walk-heavy preview sample (e.g. fox faceplant-only legs).</summary>
    public static bool LegPitchesVaryWithWalk(string modelOfficialJvmName, float idlePhase01, float wave)
    {
        var rest = PreviewRenderStateSynthesis.ForLivingWalk(0f, 0f, 0f);
        var walk = PreviewRenderStateSynthesis.ForLivingWalk(2.07f, idlePhase01, wave);
        // ReSharper disable InconsistentNaming — leg slot names mirror javap locals (r0h = right-hind, etc.).
        if (!TryGetLegXRots(modelOfficialJvmName, rest, out var r0h, out var r0l, out var r0f, out var r0fr) ||
            !TryGetLegXRots(modelOfficialJvmName, walk, out var r1h, out var r1l, out var r1f, out var r1fr))
        {
            return false;
        }

        var delta = MathF.Abs(r0h - r1h) + MathF.Abs(r0l - r1l) + MathF.Abs(r0f - r1f) + MathF.Abs(r0fr - r1fr);
        return delta > 1e-5f;
    }

    private static void ApplyProperty(PartPose partPose, string prop, float v)
    {
        switch (prop)
        {
            case "xRot": partPose.XRot = v; break;
            case "yRot": partPose.YRot = v; break;
            case "zRot": partPose.ZRot = v; break;
            case "x": partPose.X = v; break;
            case "y": partPose.Y = v; break;
            case "z": partPose.Z = v; break;
            case "visible": partPose.Visible = v >= 0.5f; break;
        }
    }

    private static void ApplyPlaybackSteps(
        string modelOfficialJvmName,
        JsonArray steps,
        IReadOnlyDictionary<string, float> renderState,
        float animationTimeSeconds,
        PoseResult pose,
        string? parityBuilderMethod,
        bool isBaby)
    {
        foreach (var n in steps)
        {
            if (n is not JsonObject step)
            {
                continue;
            }

            var mode = (string?)step["mode"] ?? "";
            if (string.Equals(mode, "apply", StringComparison.OrdinalIgnoreCase))
            {
                var animField = (string?)step["animationField"] ?? "";
                var def = FindBakedDefinition(modelOfficialJvmName, animField, parityBuilderMethod, isBaby);
                if (def is null)
                {
                    continue;
                }

                var stateField = (string?)step["stateField"] ?? "";
                var ageField = (string?)step["ageField"] ?? "ageInTicks";
                var t = animationTimeSeconds;
                if (!string.IsNullOrEmpty(stateField) &&
                    renderState.TryGetValue(stateField, out var stateTime) &&
                    stateTime > 0f)
                {
                    t = stateTime;
                }
                else if (renderState.TryGetValue(ageField, out var ageTicks))
                {
                    t = ageTicks / 20f;
                }

                SampleDefinitionChannels(def, t, 1f, pose);
            }
            else if (string.Equals(mode, "applyWalk", StringComparison.OrdinalIgnoreCase))
            {
                var animField = (string?)step["animationField"] ?? "";
                var def = FindBakedDefinition(modelOfficialJvmName, animField, parityBuilderMethod, isBaby);
                if (def is null)
                {
                    continue;
                }

                var posField = (string?)step["walkPosField"] ?? "walkAnimationPos";
                var speedField = (string?)step["walkSpeedField"] ?? "walkAnimationSpeed";
                var pos = renderState.GetValueOrDefault(posField, 0f);
                var speed = renderState.GetValueOrDefault(speedField, 0f);
                var speedScale = step["speedScale"]?.GetValue<float>() ?? 1f;
                var posScale = step["posScale"]?.GetValue<float>() ?? 1f;
                var t = pos * posScale;
                SampleDefinitionChannels(def, t, speed * speedScale, pose);
            }
        }
    }

    private static JsonObject? FindBakedDefinition(
        string modelOfficialJvmName,
        string animationField,
        string? parityBuilderMethod,
        bool isBaby)
    {
        if (TryGetEffectiveRules(modelOfficialJvmName, out var rules) &&
            rules["bakedAnimations"] is JsonArray baked)
        {
            foreach (var n in baked)
            {
                if (n is JsonObject o && string.Equals((string?)o["field"], animationField, StringComparison.Ordinal))
                {
                    var defClass = (string?)o["definitionClass"] ?? "";
                    var defField = (string?)o["definitionField"] ?? "";
                    if (VanillaAnimationIrPreviewSampler.TryGetDefinitionByClassField(defClass, defField, out var def))
                    {
                        return def;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(parityBuilderMethod))
        {
            return SetupAnimPlaybackDefinitionResolver.TryResolve(parityBuilderMethod, animationField, isBaby);
        }

        return null;
    }

    private static void SampleDefinitionChannels(JsonObject definition, float timeSeconds, float _, PoseResult pose)
    {
        if (definition["channels"] is not JsonArray channels)
        {
            return;
        }

        foreach (var ch in channels)
        {
            if (ch is not JsonObject co)
            {
                continue;
            }

            var partName = (string?)co["partName"] ?? "";
            var target = (string?)co["target"] ?? "";
            if (string.IsNullOrEmpty(partName))
            {
                continue;
            }

            if (!pose.Parts.TryGetValue(partName, out var partPose))
            {
                partPose = new PartPose();
                pose.Parts[partName] = partPose;
            }

            if (!VanillaAnimationIrPreviewSampler.TrySampleChannel(co, timeSeconds, target, out var vec))
            {
                continue;
            }

            if (string.Equals(target, "ROTATION", StringComparison.OrdinalIgnoreCase))
            {
                partPose.XRot = vec.X * PreviewRenderStateSynthesis.DegToRad;
                partPose.YRot = vec.Y * PreviewRenderStateSynthesis.DegToRad;
                partPose.ZRot = vec.Z * PreviewRenderStateSynthesis.DegToRad;
            }
            else if (string.Equals(target, "POSITION", StringComparison.OrdinalIgnoreCase))
            {
                partPose.X = vec.X;
                partPose.Y = vec.Y;
                partPose.Z = vec.Z;
            }
        }
    }

    private static bool TryGetEffectiveRules(string modelOfficialJvmName, out JsonObject rules)
    {
        rules = null!;
        if (EffectiveRulesCache.TryGetValue(modelOfficialJvmName, out var cached) && cached is not null)
        {
            rules = cached;
            return true;
        }

        if (!SetupAnimDocumentLoader.TryLoad(modelOfficialJvmName, out var doc))
        {
            EffectiveRulesCache[modelOfficialJvmName] = null;
            return false;
        }

        var merged = new JsonObject();
        var chain = new List<string>();
        CollectInheritanceChain(modelOfficialJvmName, doc, chain);

        var assignmentMap = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        JsonArray? baked = null;
        JsonArray? playback = null;

        foreach (var fqn in chain)
        {
            if (!SetupAnimDocumentLoader.TryLoad(fqn, out var shard))
            {
                continue;
            }

            if (shard["assignments"] is JsonArray a)
            {
                foreach (var n in a)
                {
                    if (n is not JsonObject o)
                    {
                        continue;
                    }

                    var key = $"{(string?)o["partField"]}|{(string?)o["property"]}";
                    assignmentMap[key] = o.DeepClone();
                }
            }

            if (shard["bakedAnimations"] is JsonArray b)
            {
                baked = [];
                foreach (var n in b)
                {
                    baked.Add(n!.DeepClone());
                }
            }

            if (shard["playbackSteps"] is JsonArray p)
            {
                playback = [];
                foreach (var n in p)
                {
                    playback.Add(n!.DeepClone());
                }
            }
        }

        var allAssignments = new JsonArray();
        foreach (var n in assignmentMap.Values)
        {
            allAssignments.Add(n);
        }

        merged["assignments"] = allAssignments;
        if (baked is not null)
        {
            merged["bakedAnimations"] = baked;
        }

        if (playback is not null)
        {
            merged["playbackSteps"] = playback;
        }

        EffectiveRulesCache[modelOfficialJvmName] = merged;
        rules = merged;
        return true;
    }

    private static void CollectInheritanceChain(string modelOfficialJvmName, JsonObject doc, List<string> chain)
    {
        if (doc["inheritsSetupAnimFrom"] is JsonValue inh)
        {
            var parent = inh.GetValue<string>();
            if (!string.IsNullOrEmpty(parent) && SetupAnimDocumentLoader.TryLoad(parent, out var parentDoc))
            {
                CollectInheritanceChain(parent, parentDoc, chain);
            }
        }

        if (!chain.Contains(modelOfficialJvmName, StringComparer.Ordinal))
        {
            chain.Add(modelOfficialJvmName);
        }
    }
}

