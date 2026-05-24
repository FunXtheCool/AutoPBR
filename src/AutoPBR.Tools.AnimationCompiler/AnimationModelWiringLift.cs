using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static class AnimationModelWiringLift
{
    private static readonly Regex BakeRegex = new(
        @"getstatic\s+#\d+\s+//\s+Field\s+net/minecraft/client/animation/definitions/(\w+)\.(\w+):Lnet/minecraft/client/animation/AnimationDefinition;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));
    private static readonly Regex PutKeyframeAnimRegex = new(
        @"putfield\s+#\d+\s+//\s+Field\s+(\w+):Lnet/minecraft/client/animation/KeyframeAnimation;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));
    private static readonly Regex RootPartGetfieldRegex = new(
        @"getfield\s+#\d+\s+//\s+Field\s+(\w+):Lnet/minecraft/client/model/geom/ModelPart;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    public static void TryLift(string javapStdout, out JsonArray baked, out JsonArray playback, out List<string> notes)
    {
        baked = [];
        playback = [];
        notes = [];
        var lines = javapStdout.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        LiftBakedAnimations(lines, baked, notes);
        LiftPlaybackSteps(lines, playback, notes);
    }

    private static void LiftBakedAnimations(List<string> lines, JsonArray baked, List<string> notes)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var bm = BakeRegex.Match(lines[i]);
            if (!bm.Success)
            {
                continue;
            }

            if (i + 4 >= lines.Count || !lines[i + 2].Contains("bake:(", StringComparison.Ordinal))
            {
                notes.Add($"AnimationDefinition bake pattern incomplete near line {i}.");
                continue;
            }

            var put = PutKeyframeAnimRegex.Match(lines[i + 3]);
            if (!put.Success)
            {
                put = PutKeyframeAnimRegex.Match(lines[i + 4]);
            }

            if (!put.Success)
            {
                notes.Add($"KeyframeAnimation putfield missing after bake at line {i}.");
                continue;
            }

            var rootPart = "root";
            for (var j = i + 1; j < i + 4; j++)
            {
                var rm = RootPartGetfieldRegex.Match(lines[j]);
                if (rm.Success && !string.Equals(rm.Groups[1].Value, "root", StringComparison.Ordinal))
                {
                    rootPart = rm.Groups[1].Value;
                }
            }

            baked.Add(new JsonObject
            {
                ["field"] = put.Groups[1].Value,
                ["definitionClass"] = $"net.minecraft.client.animation.definitions.{bm.Groups[1].Value}",
                ["definitionField"] = bm.Groups[2].Value,
                ["rootPartField"] = rootPart
            });
        }
    }

    private static void LiftPlaybackSteps(List<string> lines, JsonArray playback, List<string> notes)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Contains("KeyframeAnimation.applyWalk", StringComparison.Ordinal))
            {
                if (!lines[i].Contains("KeyframeAnimation.apply:", StringComparison.Ordinal))
                {
                    continue;
                }

                var animField = FindAnimationFieldBefore(lines, i);
                if (string.IsNullOrEmpty(animField))
                {
                    notes.Add($"apply: missing animation field near line {i}.");
                    continue;
                }

                var stateField = FindRenderStateField(lines, i, "AnimationState");
                var ageField = FindRenderStateField(lines, i, "ageInTicks");
                playback.Add(new JsonObject
                {
                    ["mode"] = "apply",
                    ["animationField"] = animField,
                    ["stateField"] = stateField ?? "animationState",
                    ["ageField"] = ageField ?? "ageInTicks"
                });
                continue;
            }

            var walkAnim = FindAnimationFieldBefore(lines, i);
            if (string.IsNullOrEmpty(walkAnim))
            {
                notes.Add($"applyWalk: missing animation field near line {i}.");
                continue;
            }

            var walkPos = FindRenderStateField(lines, i, "walkAnimationPos") ?? "walkAnimationPos";
            var walkSpeed = FindRenderStateField(lines, i, "walkAnimationSpeed") ?? "walkAnimationSpeed";
            float? speedScale = null;
            float? posScale = null;
            var consts = CollectNearbyFloatConsts(lines, i, 20);
            if (consts.Count >= 2)
            {
                speedScale = consts[^2];
                posScale = consts[^1];
            }
            else if (consts.Count == 1)
            {
                speedScale = consts[0];
            }

            var step = new JsonObject
            {
                ["mode"] = "applyWalk",
                ["animationField"] = walkAnim,
                ["walkPosField"] = walkPos,
                ["walkSpeedField"] = walkSpeed
            };
            if (speedScale is { } ss)
            {
                step["speedScale"] = ss;
            }

            if (posScale is { } ps)
            {
                step["posScale"] = ps;
            }

            playback.Add(step);
        }
    }

    private static List<float> CollectNearbyFloatConsts(List<string> lines, int center, int radius)
    {
        var list = new List<float>();
        var rx = new Regex(@"//\s*float\s+(-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)f", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        for (var j = Math.Max(0, center - radius); j < Math.Min(lines.Count, center + radius); j++)
        {
            var m = rx.Match(lines[j]);
            if (m.Success &&
                float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f))
            {
                list.Add(f);
            }
        }

        return list;
    }

    private static string? FindAnimationFieldBefore(List<string> lines, int idx)
    {
        for (var j = idx; j >= Math.Max(0, idx - 48); j--)
        {
            var m = Regex.Match(
                lines[j],
                @"getfield\s+#\d+\s+//\s+Field\s+(\w+):Lnet/minecraft/client/animation/KeyframeAnimation;",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }

        return null;
    }

    private static string? FindRenderStateField(List<string> lines, int idx, string fieldSuffix)
    {
        for (var j = idx; j >= Math.Max(0, idx - 15); j--)
        {
            if (!lines[j].Contains(fieldSuffix, StringComparison.Ordinal))
            {
                continue;
            }

            var m = Regex.Match(
                lines[j],
                @"Field\s+net/minecraft/client/renderer/entity/state/[\w$.]+\.(\w+):",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));
            if (m.Success)
            {
                return m.Groups[1].Value;
            }
        }

        return null;
    }
}
