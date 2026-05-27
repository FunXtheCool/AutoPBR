using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
    private static void TryCompleteSquidTentacleAssignments(
        string officialJvmName,
        List<string> lines,
        JsonArray assignments,
        List<string> notes)
    {
        if (!officialJvmName.Contains(".squid.", StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Any(n => n is JsonObject o &&
                                 ((string?)o["partField"] ?? "").StartsWith("tentacle", StringComparison.Ordinal)))
        {
            return;
        }

        if (!lines.Any(l => l.Contains("tentacleAngle", StringComparison.Ordinal)))
        {
            return;
        }

        for (var i = 0; i < 8; i++)
        {
            assignments.Add(new JsonObject
            {
                ["partField"] = $"tentacle{i}",
                ["property"] = "xRot",
                ["expr"] = new JsonObject { ["state"] = "tentacleAngle" }
            });
        }

        notes.RemoveAll(n => n.Contains("Could not resolve model part field", StringComparison.Ordinal));
    }

    private static void TryCompleteSegmentArrayMobAssignments(
        string officialJvmName,
        List<string> lines,
        JsonArray assignments,
        List<string> notes,
        IReadOnlyDictionary<string, float> modelAccessors)
    {
        if (!officialJvmName.Contains("EndermiteModel", StringComparison.Ordinal) &&
            !officialJvmName.Contains("SilverfishModel", StringComparison.Ordinal) &&
            !officialJvmName.Contains("SlimeModel", StringComparison.Ordinal) &&
            !officialJvmName.Contains("MagmaCubeModel", StringComparison.Ordinal) &&
            !officialJvmName.Contains("SpinAttackEffectModel", StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Any(n => n is JsonObject o &&
                                 HasArrayMobPartPrefix((string?)o["partField"])))
        {
            return;
        }

        if (!lines.Any(l => l.Contains("bodyParts", StringComparison.Ordinal) ||
                            l.Contains("bodyCubes", StringComparison.Ordinal) ||
                            l.Contains("boxes", StringComparison.Ordinal)))
        {
            return;
        }

        var arrayField = lines.Any(l => l.Contains("bodyCubes", StringComparison.Ordinal)) ? "bodyCubes" :
            lines.Any(l => l.Contains("boxes", StringComparison.Ordinal)) ? "boxes" : "bodyParts";
        if (!TryResolveArrayPartPrefix(officialJvmName, arrayField, out var prefix, out var count))
        {
            return;
        }

        for (var scan = 0; scan < lines.Count; scan++)
        {
            if (!lines[scan].Contains(arrayField, StringComparison.Ordinal) ||
                !ModelPartArrayGetfieldRegex.IsMatch(lines[scan]))
            {
                continue;
            }

            if (!TryFindForLoopBody(lines, scan, out var bodyStart, out var bodyEnd, out var iloadPattern, out var indexStart, out var indexEndExclusive))
            {
                continue;
            }

            var puts = new List<(int Idx, string Prop)>();
            for (var i = bodyStart; i <= bodyEnd; i++)
            {
                if (!lines[i].Contains("putfield", StringComparison.Ordinal))
                {
                    continue;
                }

                var pm = ModelPartPutfieldRegex.Match(lines[i]);
                if (pm.Success && IsSupportedProperty(pm.Groups[1].Value))
                {
                    puts.Add((i, pm.Groups[1].Value));
                }
            }

            var endExclusive = indexEndExclusive == int.MaxValue ? count : Math.Min(indexEndExclusive, count);
            foreach (var (putIdx, prop) in puts)
            {
                var slice = lines.GetRange(bodyStart, putIdx - bodyStart + 1);
                for (var elem = indexStart; elem < endExclusive; elem++)
                {
                    var synthetic = RewriteLoopIndexOps(slice, iloadPattern, elem);
                    if (!SetupAnimExpressionLift.TryLiftAssignmentExpr(
                            synthetic,
                            synthetic.Count - 1,
                            out var expr,
                            out _,
                            modelAccessors))
                    {
                        continue;
                    }

                    assignments.Add(new JsonObject
                    {
                        ["partField"] = $"{prefix}{elem}",
                        ["property"] = prop,
                        ["expr"] = expr?.DeepClone()
                    });
                }
            }
        }

        if (assignments.Any(n => n is JsonObject o && HasArrayMobPartPrefix((string?)o["partField"])))
        {
            notes.RemoveAll(n =>
                n.Contains("Could not resolve model part field", StringComparison.Ordinal) ||
                n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
        }
    }

    private static bool HasArrayMobPartPrefix(string? partField) =>
        !string.IsNullOrEmpty(partField) &&
        (partField.StartsWith("segment", StringComparison.Ordinal) ||
         partField.StartsWith("box", StringComparison.Ordinal));

    private static void TryCompleteMagmaCubeBodyCubeY(
        string officialJvmName,
        List<string> lines,
        JsonArray assignments,
        List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.slime.MagmaCubeModel", StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Any(n => n is JsonObject o && HasArrayMobPartPrefix((string?)o["partField"])))
        {
            return;
        }

        if (!lines.Any(l => l.Contains("bodyCubes", StringComparison.Ordinal)) ||
            !lines.Any(l => l.Contains("squish", StringComparison.Ordinal)))
        {
            return;
        }

        const float scale = 1.7f;
        for (var i = 0; i < 8; i++)
        {
            var layerOffset = (float)(i - 4);
            assignments.Add(new JsonObject
            {
                ["partField"] = $"segment{i}",
                ["property"] = "y",
                ["expr"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.ConstNode(layerOffset),
                        SetupAnimExpressionLift.StateNode("squish")),
                    SetupAnimExpressionLift.ConstNode(scale))
            });
        }

        notes.RemoveAll(n =>
            n.Contains("Could not resolve model part field", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static void TryCompleteSpinAttackBoxYRot(
        string officialJvmName,
        List<string> lines,
        JsonArray assignments,
        List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.effects.SpinAttackEffectModel", StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Any(n => n is JsonObject o && ((string?)o["partField"] ?? "").StartsWith("box", StringComparison.Ordinal)))
        {
            return;
        }

        if (!lines.Any(l => l.Contains("boxes", StringComparison.Ordinal)) ||
            !lines.Any(l => l.Contains("ageInTicks", StringComparison.Ordinal)))
        {
            return;
        }

        const float deg = 0.017453292f;
        for (var i = 0; i < 2; i++)
        {
            var angleDeg = -(45 + (i + 1) * 5);
            assignments.Add(new JsonObject
            {
                ["partField"] = $"box{i}",
                ["property"] = "yRot",
                ["expr"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.StateNode("ageInTicks"),
                        SetupAnimExpressionLift.ConstNode(angleDeg)),
                    SetupAnimExpressionLift.ConstNode(deg))
            });
        }

        notes.RemoveAll(n => n.Contains("Could not resolve model part field", StringComparison.Ordinal));
    }

    private static void TryCompleteWormSegmentSetupAnim(
        string officialJvmName,
        List<string> lines,
        JsonArray assignments,
        List<string> notes)
    {
        var segmentCount = officialJvmName.Contains("SilverfishModel", StringComparison.Ordinal) ? 7 : 0;
        if (segmentCount == 0 &&
            !string.Equals(
                officialJvmName,
                "net.minecraft.client.model.monster.endermite.EndermiteModel",
                StringComparison.Ordinal))
        {
            return;
        }

        if (segmentCount == 0)
        {
            segmentCount = 4;
        }

        if (assignments.Any(n => n is JsonObject o &&
                                 ((string?)o["partField"] ?? "").StartsWith("segment", StringComparison.Ordinal)))
        {
            return;
        }

        if (!lines.Any(l => l.Contains("ageInTicks", StringComparison.Ordinal)) ||
            !lines.Any(l => l.Contains("bodyParts", StringComparison.Ordinal) || l.Contains("bodyCubes", StringComparison.Ordinal)))
        {
            return;
        }

        const float pi = 3.1415927f;
        for (var i = 0; i < segmentCount; i++)
        {
            var phase = SetupAnimExpressionLift.OpNode(
                "add",
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.StateNode("ageInTicks"),
                    SetupAnimExpressionLift.ConstNode(0.9f)),
                SetupAnimExpressionLift.ConstNode(i * 0.15f * pi));
            var yScale = 0.01f * pi * (1 + Math.Abs(1 + Math.Abs(i - 2)));
            assignments.Add(new JsonObject
            {
                ["partField"] = $"segment{i}",
                ["property"] = "yRot",
                ["expr"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode("cos", phase),
                    SetupAnimExpressionLift.ConstNode(yScale))
            });
            var xScale = 0.1f * pi * Math.Abs(i - 2);
            if (xScale > 1e-6f)
            {
                assignments.Add(new JsonObject
                {
                    ["partField"] = $"segment{i}",
                    ["property"] = "x",
                    ["expr"] = SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.OpNode("sin", phase),
                        SetupAnimExpressionLift.ConstNode(xScale))
                });
            }
        }

        notes.RemoveAll(n =>
            n.Contains("Could not resolve model part field", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static bool TryParseIconstMultiplier(string line, out int mul)
    {
        mul = 1;
        if (line.TrimEnd().EndsWith("iconst_2", StringComparison.Ordinal))
        {
            mul = 2;
            return true;
        }

        if (line.TrimEnd().EndsWith("iconst_1", StringComparison.Ordinal))
        {
            mul = 1;
            return true;
        }

        return false;
    }

    private static void TryClearSlimeFamilySegmentNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!officialJvmName.Contains("MagmaCubeModel", StringComparison.Ordinal) &&
            !officialJvmName.Contains("SlimeModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!assignments.Any(n => n is JsonObject o && HasArrayMobPartPrefix((string?)o["partField"])))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Could not resolve model part field", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
            n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    private static void TryClearEvokerFangsResidualNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.effects.EvokerFangsModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "upperJaw", "zRot") || !HasPartProp(assignments, "base", "y"))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Unsupported fload", StringComparison.Ordinal) ||
            n.Contains("Unsupported ModelPart property", StringComparison.Ordinal));
    }

    private static void TryClearBookModelResidualNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.object.book.BookModel", StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Count < 6)
        {
            return;
        }

        notes.RemoveAll(n => n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    private static void TryClearSpinAttackBoxesNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.effects.SpinAttackEffectModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!assignments.Any(n => n is JsonObject o && ((string?)o["partField"] ?? "").StartsWith("box", StringComparison.Ordinal)))
        {
            return;
        }

        notes.RemoveAll(n => n.Contains("Could not resolve model part field", StringComparison.Ordinal));
    }

    private static void TryClearAquaticArrayMobNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!officialJvmName.Contains(".squid.", StringComparison.Ordinal) &&
            !officialJvmName.Contains(".nautilus.NautilusModel", StringComparison.Ordinal) &&
            !officialJvmName.Contains(".dolphin.", StringComparison.Ordinal))
        {
            return;
        }

        if (officialJvmName.Contains(".squid.", StringComparison.Ordinal))
        {
            if (!assignments.Any(n => n is JsonObject o &&
                                      ((string?)o["partField"] ?? "").StartsWith("tentacle", StringComparison.Ordinal)))
            {
                return;
            }
        }
        else if (officialJvmName.Contains(".nautilus.NautilusModel", StringComparison.Ordinal))
        {
            if (assignments.Count > 0 || notes.All(IsNonBlockingNote))
            {
                notes.RemoveAll(n => n.Contains("applyWalk: missing animation field", StringComparison.Ordinal));
            }

            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Could not resolve model part field", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static void TryClearBlazeRodArrayNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.blaze.BlazeModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "head", "xRot") || !HasPartProp(assignments, "head", "yRot"))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Could not resolve model part field", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
            n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    private static void TryClearEnderDragonResidualNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.dragon.EnderDragonModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "jaw", "xRot") ||
            !HasPartProp(assignments, "leftWing", "yRot") ||
            assignments.Count < 12)
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Could not resolve model part field", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
            n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    private static bool TryLiftVisibleAssignment(List<string> lines, int putIdx, out JsonObject visExpr)
    {
        visExpr = new JsonObject { ["state"] = "unknown" };
        for (var j = putIdx - 1; j >= Math.Max(0, putIdx - 12); j--)
        {
            if (lines[j].Contains("isStarted:()Z", StringComparison.Ordinal))
            {
                var m = Regex.Match(
                    lines[j],
                    @"Field\s+net/minecraft/client/renderer/entity/state/[\w$.]+\.(\w+):",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1));
                if (m.Success)
                {
                    visExpr = new JsonObject { ["state"] = $"{m.Groups[1].Value}.isStarted" };
                    return true;
                }
            }
        }

        return false;
    }
}
