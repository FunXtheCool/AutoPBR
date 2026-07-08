using System.Text.Json.Nodes;

namespace AutoPBR.Preview;

/// <summary>
/// Maps P6 renderer-state JSON shards to preview render-state dictionaries consumed by setupAnim IR.
/// Inactive animation clips use <see cref="InactiveAnimationStateSentinel"/> so apply steps are skipped.
/// </summary>
internal static class RendererStatePreviewResolver
{
    public const float InactiveAnimationStateSentinel = -1f;

    public static IReadOnlyDictionary<string, float> SynthesizeForRenderer(
        string officialRendererJvmName,
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        if (!RendererStateDocumentLoader.TryLoadByRenderer(officialRendererJvmName, out var doc))
        {
            return PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave);
        }

        return Synthesize(doc, animationTimeSeconds, idlePhase01, wave);
    }

    public static IReadOnlyDictionary<string, float> Synthesize(
        JsonObject doc,
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        var driver = (string?)doc["previewDriver"] ?? "";
        return driver switch
        {
            "allay_hold_dance_cycle" => SynthesizeAllayHoldDance(doc, animationTimeSeconds, idlePhase01, wave),
            "chicken_idle_flap" => SynthesizeChickenIdleFlap(animationTimeSeconds, idlePhase01, wave),
            "nautilus_swim_walk" => SynthesizeNautilusSwimWalk(doc, animationTimeSeconds, idlePhase01, wave),
            "static_scalar_state" => SynthesizeStaticScalarState(doc, animationTimeSeconds, idlePhase01, wave),
            _ when driver.EndsWith("_clip_cycle", StringComparison.Ordinal) =>
                SynthesizeClipCycle(doc, animationTimeSeconds, idlePhase01, wave),
            _ => PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave)
        };
    }

    private static Dictionary<string, float> SynthesizeClipCycle(
        JsonObject doc,
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        var state = new Dictionary<string, float>(
            PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave),
            StringComparer.Ordinal);

        MarkAnimationFieldsInactive(doc, state);

        if (doc["clipLengthsSeconds"] is not JsonObject segments)
        {
            return state;
        }

        var cursor = 0f;
        foreach (var segment in segments)
        {
            var key = segment.Key;
            var length = segment.Value?.GetValue<float>() ?? 0f;
            if (length <= 0f)
            {
                continue;
            }

            if (animationTimeSeconds >= cursor && animationTimeSeconds < cursor + length)
            {
                var localTime = animationTimeSeconds - cursor;
                if (string.Equals(key, "walkOnly", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "swimWalk", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                state[key] = localTime;
                break;
            }

            cursor += length;
        }

        ApplyWardenProceduralFields(doc, state);
        return state;
    }

    private static IReadOnlyDictionary<string, float> SynthesizeChickenIdleFlap(
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        var flapSpeed = 0.18f + Math.Clamp(0.22f + idlePhase01 * 0.18f + wave * 0.12f, 0.05f, 0.95f) * 0.55f;
        return PreviewRenderStateSynthesis.ForChicken(
            animationTimeSeconds,
            idlePhase01,
            wave,
            headPitchDegrees: 0f,
            headYawDegrees: 0f,
            flapSpeed);
    }

    private static Dictionary<string, float> SynthesizeStaticScalarState(
        JsonObject doc,
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        var state = new Dictionary<string, float>(
            PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave),
            StringComparer.Ordinal);
        if (doc["scalarRenderStateFields"] is not JsonArray fields ||
            doc["scalarDefaults"] is not JsonObject defaults)
        {
            return state;
        }

        foreach (var fieldNode in fields)
        {
            if (fieldNode is not JsonObject field)
            {
                continue;
            }

            var renderField = (string?)field["renderStateField"];
            var setupField = (string?)field["setupAnimStateField"];
            if (string.IsNullOrWhiteSpace(renderField) || string.IsNullOrWhiteSpace(setupField))
            {
                continue;
            }

            if (TryReadFloat(defaults[renderField], out var value) ||
                TryReadFloat(defaults[setupField], out value))
            {
                state[setupField] = value;
            }
        }

        return state;
    }

    private static Dictionary<string, float> SynthesizeAllayHoldDance(
        JsonObject doc,
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        var holdingLen = doc["phaseLengthsSeconds"]?["holding"]?.GetValue<float>() ?? 3f;
        var danceLen = doc["phaseLengthsSeconds"]?["dance"]?.GetValue<float>() ?? 4f;
        var spinRampLen = doc["phaseLengthsSeconds"]?["spinRamp"]?.GetValue<float>() ?? 1.5f;
        var cycleLen = holdingLen + danceLen;
        var t = cycleLen > 0f ? animationTimeSeconds % cycleLen : animationTimeSeconds;

        var state = new Dictionary<string, float>(
            PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave),
            StringComparer.Ordinal)
        {
            ["holdingAnimationProgress"] = 0f,
            ["isDancing"] = 0f,
            ["isSpinning"] = 0f,
            ["spinningProgress"] = 0f
        };

        if (t < holdingLen)
        {
            state["holdingAnimationProgress"] = Math.Clamp(t / Math.Max(holdingLen, 1e-6f), 0f, 1f);
        }
        else
        {
            var danceT = t - holdingLen;
            state["isDancing"] = 1f;
            state["holdingAnimationProgress"] = 0f;
            state["spinningProgress"] = Math.Clamp(danceT / Math.Max(spinRampLen, 1e-6f), 0f, 1f);
            state["isSpinning"] = state["spinningProgress"] >= 1f ? 1f : 0f;
        }

        return state;
    }

    private static Dictionary<string, float> SynthesizeNautilusSwimWalk(
        JsonObject doc,
        float animationTimeSeconds,
        float idlePhase01,
        float wave)
    {
        var swimLen = doc["clipLengthsSeconds"]?["swimWalk"]?.GetValue<float>() ?? 4f;
        var t = swimLen > 0f ? animationTimeSeconds % swimLen : animationTimeSeconds;
        var (walkPos, walkSpeed) = PreviewRenderStateSynthesis.ComputeWalkCycle(t, idlePhase01, wave);
        return new Dictionary<string, float>(StringComparer.Ordinal)
        {
            ["walkAnimationPos"] = walkPos * 3f,
            ["walkAnimationSpeed"] = walkSpeed * 0.2f,
            ["xRot"] = 0f,
            ["yRot"] = 0f,
            ["ageInTicks"] = animationTimeSeconds * 20f
        };
    }

    private static void MarkAnimationFieldsInactive(JsonObject doc, Dictionary<string, float> state)
    {
        if (doc["animationStateFields"] is not JsonArray fields)
        {
            return;
        }

        foreach (var fieldNode in fields)
        {
            if (fieldNode is not JsonObject field)
            {
                continue;
            }

            var setupField = (string?)field["setupAnimStateField"];
            if (!string.IsNullOrEmpty(setupField))
            {
                state[setupField] = InactiveAnimationStateSentinel;
            }
        }
    }

    private static void ApplyWardenProceduralFields(JsonObject doc, Dictionary<string, float> state)
    {
        if (!string.Equals(
                (string?)doc["officialJvmName"],
                "net.minecraft.client.renderer.entity.WardenRenderer",
                StringComparison.Ordinal))
        {
            return;
        }

        var age = state.GetValueOrDefault("ageInTicks", 0f);
        state["tendrilAnimation"] = MathF.Sin(age * 0.08f);
        state["heartAnimation"] = 0.5f + 0.5f * MathF.Sin(age * 0.12f);
    }

    private static bool TryReadFloat(JsonNode? node, out float value)
    {
        value = 0f;
        if (node is null)
        {
            return false;
        }

        try
        {
            value = node.GetValue<float>();
            return true;
        }
        catch (InvalidOperationException)
        {
            try
            {
                value = node.GetValue<bool>() ? 1f : 0f;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
