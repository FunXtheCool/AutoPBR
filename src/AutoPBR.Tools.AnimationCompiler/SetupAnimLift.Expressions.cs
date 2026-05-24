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
    private static void TryCompleteEquineLegXRotAssignments(
        List<string> lines,
        JsonArray assignments,
        IReadOnlyDictionary<string, float> modelAccessors,
        List<string> notes)
    {
        _ = lines;
        var legSpecs = new (string Part, bool? AddHindWave, bool Front)[]
        {
            ("leftHindLeg", false, false),
            ("rightHindLeg", true, false),
            ("leftFrontLeg", null, true),
            ("rightFrontLeg", null, true)
        };

        foreach (var (part, addHindWave, front) in legSpecs)
        {
            if (assignments.Any(n => n is JsonObject o &&
                                     string.Equals((string?)o["partField"], part, StringComparison.Ordinal) &&
                                     string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal)))
            {
                continue;
            }

            JsonObject expr;
            if (front)
            {
                var subtractWave = string.Equals(part, "rightFrontLeg", StringComparison.Ordinal);
                expr = BuildEquineFrontLegXRotExpr(modelAccessors, subtractWave);
            }
            else if (addHindWave is bool add)
            {
                expr = BuildEquineHindLegXRotExpr(modelAccessors, add);
            }
            else
            {
                continue;
            }

            assignments.Add(new JsonObject
            {
                ["partField"] = part,
                ["property"] = "xRot",
                ["expr"] = expr
            });
        }

        if (legSpecs.All(spec => assignments.Any(n => n is JsonObject o &&
                                                       string.Equals((string?)o["partField"], spec.Part, StringComparison.Ordinal) &&
                                                       string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal))))
        {
            notes.RemoveAll(n => n.Contains("Unsupported fload", StringComparison.Ordinal) &&
                                 n.Contains("xRot", StringComparison.Ordinal));
        }
    }

    private static JsonObject BuildEquineHindLegXRotExpr(IReadOnlyDictionary<string, float> modelAccessors, bool addWave)
    {
        var standAngle = SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.ConstNode(modelAccessors.GetValueOrDefault("getLegStandAngle", 0.2617994f)),
            SetupAnimExpressionLift.StateNode("standAnimation"));
        var waveTerm = BuildEquineWalkWaveLegTerm();
        return addWave
            ? SetupAnimExpressionLift.OpNode("add", standAngle, waveTerm)
            : SetupAnimExpressionLift.OpNode("sub", standAngle, waveTerm);
    }

    private static JsonObject BuildEquineFrontLegXRotExpr(IReadOnlyDictionary<string, float> modelAccessors, bool subtractWave)
    {
        var offset = modelAccessors.GetValueOrDefault("getLegStandingXRotOffset", -1.0471976f);
        var legBase = SetupAnimExpressionLift.OpNode(
            "add",
            SetupAnimExpressionLift.ConstNode(offset),
            SetupAnimExpressionLift.OpNode(
                "cos",
                SetupAnimExpressionLift.OpNode(
                    "add",
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.StateNode("ageInTicks"),
                        SetupAnimExpressionLift.ConstNode(0.6f)),
                    SetupAnimExpressionLift.ConstNode(MathF.PI))));
        var standTerm = SetupAnimExpressionLift.OpNode(
            "mul",
            legBase,
            SetupAnimExpressionLift.StateNode("standAnimation"));
        var waveTerm = SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    BuildEquineWalkCosWave(),
                    SetupAnimExpressionLift.ConstNode(0.8f)),
                SetupAnimExpressionLift.StateNode("walkAnimationSpeed")),
            SetupAnimExpressionLift.OpNode(
                "sub",
                SetupAnimExpressionLift.ConstNode(1f),
                SetupAnimExpressionLift.StateNode("standAnimation")));
        return subtractWave
            ? SetupAnimExpressionLift.OpNode("sub", standTerm, waveTerm)
            : SetupAnimExpressionLift.OpNode("add", standTerm, waveTerm);
    }

    private static JsonObject BuildEquineWalkWaveLegTerm() =>
        SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    BuildEquineWalkCosWave(),
                    SetupAnimExpressionLift.ConstNode(0.5f)),
                SetupAnimExpressionLift.StateNode("walkAnimationSpeed")),
            SetupAnimExpressionLift.OpNode(
                "sub",
                SetupAnimExpressionLift.ConstNode(1f),
                SetupAnimExpressionLift.StateNode("standAnimation")));

    private static void TryCompleteEquineHeadBodyTailXRotAssignments(
        JsonArray assignments,
        IReadOnlyDictionary<string, float> modelAccessors,
        List<string> notes)
    {
        RemoveAssignments(assignments, o =>
            string.Equals((string?)o["partField"], "headParts", StringComparison.Ordinal) &&
            string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal));
        assignments.Add(new JsonObject
        {
            ["partField"] = "headParts",
            ["property"] = "xRot",
            ["expr"] = BuildEquineHeadXRotExpr()
        });

        RemoveAssignments(assignments, o =>
            string.Equals((string?)o["partField"], "body", StringComparison.Ordinal) &&
            string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal));
        assignments.Add(new JsonObject
        {
            ["partField"] = "body",
            ["property"] = "xRot",
            ["expr"] = BuildEquineBodyXRotExpr()
        });

        RemoveAssignments(assignments, o =>
            string.Equals((string?)o["partField"], "tail", StringComparison.Ordinal) &&
            string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal));
        assignments.Add(new JsonObject
        {
            ["partField"] = "tail",
            ["property"] = "xRot",
            ["expr"] = BuildEquineTailXRotExpr(modelAccessors)
        });

        notes.RemoveAll(n =>
            n.Contains("xRot", StringComparison.Ordinal) &&
            (n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
             n.Contains("Unsupported fload", StringComparison.Ordinal)) &&
            (n.Contains("Line 58 ", StringComparison.Ordinal) ||
             n.Contains("Line 127 ", StringComparison.Ordinal) ||
             n.Contains("Line 161 ", StringComparison.Ordinal) ||
             n.Contains("Line 242 ", StringComparison.Ordinal) ||
             n.Contains("Line 254 ", StringComparison.Ordinal) ||
             n.Contains("Line 258 ", StringComparison.Ordinal) ||
             n.Contains("Line 262 ", StringComparison.Ordinal)));
    }

    private static void RemoveAssignments(JsonArray assignments, Func<JsonObject, bool> match)
    {
        for (var i = assignments.Count - 1; i >= 0; i--)
        {
            if (assignments[i] is JsonObject o && match(o))
            {
                assignments.RemoveAt(i);
            }
        }
    }

    private static JsonObject BuildEquineHeadInputPitchRad() =>
        SetupAnimExpressionLift.OpNode(
            "add",
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.StateNode("xRot"),
                SetupAnimExpressionLift.ConstNode(0.017453292f)),
            new JsonObject
            {
                ["when"] = new JsonObject
                {
                    ["state"] = "walkAnimationSpeed",
                    ["cmp"] = "gt",
                    ["value"] = 0.2
                },
                ["then"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.OpNode(
                            "cos",
                            SetupAnimExpressionLift.OpNode(
                                "mul",
                                SetupAnimExpressionLift.StateNode("walkAnimationPos"),
                                SetupAnimExpressionLift.ConstNode(0.8f))),
                        SetupAnimExpressionLift.ConstNode(0.15f)),
                    SetupAnimExpressionLift.StateNode("walkAnimationSpeed")),
                ["else"] = SetupAnimExpressionLift.ConstNode(0f)
            });

    private static JsonObject BuildEquineHeadXRotExpr()
    {
        var headPitch = BuildEquineHeadInputPitchRad();
        var feedingBlend = SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.OpNode(
                "sub",
                SetupAnimExpressionLift.ConstNode(1f),
                SetupAnimExpressionLift.OpNode(
                    "max",
                    SetupAnimExpressionLift.StateNode("standAnimation"),
                    SetupAnimExpressionLift.StateNode("eatAnimation"))),
            SetupAnimExpressionLift.OpNode(
                "add",
                SetupAnimExpressionLift.OpNode(
                    "add",
                    SetupAnimExpressionLift.ConstNode(0.5235988f),
                    headPitch),
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.StateNode("feedingAnimation"),
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.OpNode("sin", SetupAnimExpressionLift.StateNode("ageInTicks")),
                        SetupAnimExpressionLift.ConstNode(0.05f)))));
        var standTerm = SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.StateNode("standAnimation"),
            SetupAnimExpressionLift.OpNode(
                "add",
                SetupAnimExpressionLift.ConstNode(0.2617994f),
                headPitch));
        var eatTerm = SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.StateNode("eatAnimation"),
            SetupAnimExpressionLift.OpNode(
                "add",
                SetupAnimExpressionLift.ConstNode(2.1816616f),
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode("sin", SetupAnimExpressionLift.StateNode("ageInTicks")),
                    SetupAnimExpressionLift.ConstNode(0.05f))));
        return SetupAnimExpressionLift.OpNode(
            "add",
            SetupAnimExpressionLift.OpNode("add", standTerm, eatTerm),
            feedingBlend);
    }

    private static JsonObject BuildEquineBodyXRotExpr() =>
        SetupAnimExpressionLift.OpNode(
            "add",
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.StateNode("standAnimation"),
                SetupAnimExpressionLift.ConstNode(-0.7853982f)),
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode(
                    "sub",
                    SetupAnimExpressionLift.ConstNode(1f),
                    SetupAnimExpressionLift.StateNode("standAnimation")),
                new JsonObject { ["partSelf"] = "xRot" }));

    private static JsonObject BuildEquineTailXRotExpr(IReadOnlyDictionary<string, float> modelAccessors) =>
        SetupAnimExpressionLift.OpNode(
            "add",
            SetupAnimExpressionLift.OpNode(
                "add",
                SetupAnimExpressionLift.ConstNode(modelAccessors.GetValueOrDefault("getTailXRotOffset", 0f)),
                SetupAnimExpressionLift.ConstNode(0.5235988f)),
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.StateNode("walkAnimationSpeed"),
                SetupAnimExpressionLift.ConstNode(0.75f)));

    private static JsonObject BuildEquineWalkCosWave() =>
        SetupAnimExpressionLift.OpNode(
            "cos",
            SetupAnimExpressionLift.OpNode(
                "add",
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        BuildEquineInWaterWalkScale(),
                        SetupAnimExpressionLift.StateNode("walkAnimationPos")),
                    SetupAnimExpressionLift.ConstNode(0.6662f)),
                SetupAnimExpressionLift.ConstNode(MathF.PI)));

    private static JsonObject BuildEquineInWaterWalkScale() =>
        new()
        {
            ["when"] = new JsonObject
            {
                ["state"] = "isInWater",
                ["cmp"] = "eq",
                ["value"] = true
            },
            ["then"] = SetupAnimExpressionLift.ConstNode(0.2f),
            ["else"] = SetupAnimExpressionLift.ConstNode(1f)
        };

    private static void TryCompleteEquineTailDupOffsets(JsonArray assignments, List<string> notes)
    {
        if (!assignments.Any(n => n is JsonObject o &&
                                  string.Equals((string?)o["partField"], "tail", StringComparison.Ordinal) &&
                                  string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal)))
        {
            return;
        }

        if (!assignments.Any(n => n is JsonObject o &&
                string.Equals((string?)o["partField"], "tail", StringComparison.Ordinal) &&
                string.Equals((string?)o["property"], "y", StringComparison.Ordinal)))
        {
            assignments.Add(new JsonObject
            {
                ["partField"] = "tail",
                ["property"] = "y",
                ["expr"] = SetupAnimExpressionLift.OpNode(
                    "add",
                    new JsonObject { ["partSelf"] = "y" },
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.StateNode("walkAnimationSpeed"),
                        SetupAnimExpressionLift.StateNode("ageScale")))
            });
        }

        if (!assignments.Any(n => n is JsonObject o &&
                string.Equals((string?)o["partField"], "tail", StringComparison.Ordinal) &&
                string.Equals((string?)o["property"], "z", StringComparison.Ordinal)))
        {
            assignments.Add(new JsonObject
            {
                ["partField"] = "tail",
                ["property"] = "z",
                ["expr"] = SetupAnimExpressionLift.OpNode(
                    "add",
                    new JsonObject { ["partSelf"] = "z" },
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.OpNode(
                            "mul",
                            SetupAnimExpressionLift.StateNode("walkAnimationSpeed"),
                            SetupAnimExpressionLift.ConstNode(2f)),
                        SetupAnimExpressionLift.StateNode("ageScale")))
            });
        }

        notes.RemoveAll(n =>
            n.Contains("tail", StringComparison.Ordinal) &&
            (n.Contains("Unsupported fload", StringComparison.Ordinal) ||
             n.Contains("Expression stack did not reduce", StringComparison.Ordinal)));
    }

    private static void TryCompleteQuadrupedLegAssignments(JsonArray assignments, List<string> notes)
    {
        JsonObject? legExpr = null;
        foreach (var n in assignments)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["partField"], "rightHindLeg", StringComparison.Ordinal) &&
                string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal))
            {
                legExpr = o["expr"]!.DeepClone().AsObject();
                break;
            }
        }

        if (legExpr is null)
        {
            return;
        }

        var legTemplate = CloneExprTree(legExpr);

        var specs = new (string Part, bool AddPi)[]
        {
            ("leftHindLeg", true),
            ("rightFrontLeg", true),
            ("leftFrontLeg", false)
        };
        foreach (var (part, addPi) in specs)
        {
            if (assignments.Any(n => n is JsonObject o &&
                                     string.Equals((string?)o["partField"], part, StringComparison.Ordinal) &&
                                     string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal)))
            {
                continue;
            }

            assignments.Add(new JsonObject
            {
                ["partField"] = part,
                ["property"] = "xRot",
                ["expr"] = addPi ? AddPiToLegCosExpr(legTemplate) : CloneExprTree(legTemplate)
            });
            notes.RemoveAll(n => n.Contains(part, StringComparison.Ordinal) && n.Contains("xRot", StringComparison.Ordinal));
        }
    }

    private static JsonObject AddPiToLegCosExpr(JsonObject legExpr)
    {
        if (legExpr["op"]?.GetValue<string>() != "mul" || legExpr["args"] is not JsonArray mulArgs ||
            mulArgs.Count < 2 || mulArgs[0] is not JsonObject inner || inner["op"]?.GetValue<string>() != "mul" ||
            inner["args"] is not JsonArray innerArgs || innerArgs.Count < 2 ||
            innerArgs[0] is not JsonObject cosNode || cosNode["op"]?.GetValue<string>() != "cos" ||
            cosNode["args"] is not JsonArray cosArgs || cosArgs[0] is not JsonObject cosArg)
        {
            return CloneExprTree(legExpr);
        }

        var phased = new JsonObject
        {
            ["op"] = "add",
            ["args"] = new JsonArray
            {
                cosArg.DeepClone(),
                SetupAnimExpressionLift.ConstNode(MathF.PI)
            }
        };
        var newCos = new JsonObject { ["op"] = "cos", ["args"] = new JsonArray { phased } };
        var newInner = new JsonObject
        {
            ["op"] = "mul",
            ["args"] = new JsonArray { newCos, innerArgs[1]!.DeepClone() }
        };
        return new JsonObject
        {
            ["op"] = "mul",
            ["args"] = new JsonArray { newInner, mulArgs[1]!.DeepClone() }
        };
    }

    private static JsonObject CloneExprTree(JsonObject expr) =>
        JsonNode.Parse(expr.ToJsonString())!.AsObject();

    private static void TryClearFelineBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!officialJvmName.Contains(".feline.", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasQuadrupedFourLegAssignments(assignments))
        {
            return;
        }

        static bool HasPartProp(JsonArray arr, string part, string prop) =>
            arr.Any(n => n is JsonObject o &&
                         string.Equals((string?)o["partField"], part, StringComparison.Ordinal) &&
                         string.Equals((string?)o["property"], prop, StringComparison.Ordinal));

        if (!HasPartProp(assignments, "head", "xRot") ||
            !HasPartProp(assignments, "head", "yRot") ||
            !HasPartProp(assignments, "tail2", "xRot"))
        {
            return;
        }

        notes.RemoveAll(n => n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static bool IsEquineSetupAnimHost(string officialJvmName) =>
        officialJvmName.Contains(".equine.", StringComparison.Ordinal);

    private static void TryCompleteHumanoidHostSetupAnim(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.HumanoidModel", StringComparison.Ordinal))
        {
            return;
        }

        assignments.Clear();
        const float deg = 0.017453292f;
        assignments.Add(new JsonObject
        {
            ["partField"] = "head",
            ["property"] = "xRot",
            ["expr"] = new JsonObject
            {
                ["when"] = new JsonObject { ["state"] = "isFallFlying", ["cmp"] = "eq", ["value"] = true },
                ["then"] = SetupAnimExpressionLift.ConstNode(-0.7853982f),
                ["else"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.StateNode("xRot"),
                    SetupAnimExpressionLift.ConstNode(deg))
            }
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "head",
            ["property"] = "yRot",
            ["expr"] = SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.StateNode("yRot"),
                SetupAnimExpressionLift.ConstNode(deg))
        });
        foreach (var (part, addPi, isLeg) in new (string Part, bool AddPi, bool IsLeg)[]
                 {
                     ("rightArm", true, false),
                     ("leftArm", false, false),
                     ("rightLeg", false, true),
                     ("leftLeg", true, true)
                 })
        {
            assignments.Add(new JsonObject
            {
                ["partField"] = part,
                ["property"] = "xRot",
                ["expr"] = BuildHumanoidWalkCosXRot(addPi, isLeg)
            });
        }

        assignments.Add(new JsonObject
        {
            ["partField"] = "rightLeg",
            ["property"] = "yRot",
            ["expr"] = SetupAnimExpressionLift.ConstNode(0.005f)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "leftLeg",
            ["property"] = "yRot",
            ["expr"] = SetupAnimExpressionLift.ConstNode(-0.005f)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "rightLeg",
            ["property"] = "zRot",
            ["expr"] = SetupAnimExpressionLift.ConstNode(0.005f)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "leftLeg",
            ["property"] = "zRot",
            ["expr"] = SetupAnimExpressionLift.ConstNode(-0.005f)
        });

        notes.RemoveAll(n => !IsNonBlockingNote(n));
    }

    private static JsonObject BuildHumanoidWalkCosXRot(bool addPi, bool isLeg)
    {
        var phase = SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.StateNode("walkAnimationPos"),
            SetupAnimExpressionLift.ConstNode(0.6662f));
        if (addPi)
        {
            phase = SetupAnimExpressionLift.OpNode("add", phase, SetupAnimExpressionLift.ConstNode(MathF.PI));
        }

        var wave = SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.OpNode("cos", phase),
            SetupAnimExpressionLift.StateNode("walkAnimationSpeed"));
        if (isLeg)
        {
            wave = SetupAnimExpressionLift.OpNode("mul", wave, SetupAnimExpressionLift.ConstNode(1.4f));
        }
        else
        {
            wave = SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode("mul", wave, SetupAnimExpressionLift.ConstNode(2f)),
                SetupAnimExpressionLift.ConstNode(0.5f));
        }

        return SetupAnimExpressionLift.OpNode("div", wave, SetupAnimExpressionLift.StateNode("speedValue"));
    }

    private static void TryClearHumanoidBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!IsHumanoidSetupAnimHost(officialJvmName))
        {
            return;
        }

        if (!HasHumanoidCoreWalkAssignments(assignments))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
            n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    private static bool IsHumanoidSetupAnimHost(string officialJvmName) =>
        string.Equals(officialJvmName, "net.minecraft.client.model.HumanoidModel", StringComparison.Ordinal) ||
        officialJvmName.Contains(".piglin.", StringComparison.Ordinal) ||
        officialJvmName.Contains(".golem.IronGolemModel", StringComparison.Ordinal) ||
        officialJvmName.Contains(".golem.SnowGolemModel", StringComparison.Ordinal) ||
        string.Equals(officialJvmName, "net.minecraft.client.model.monster.vex.VexModel", StringComparison.Ordinal);

    private static bool HasHumanoidCoreWalkAssignments(JsonArray assignments) =>
        HasPartProp(assignments, "head", "xRot") &&
        HasPartProp(assignments, "head", "yRot") &&
        HasPartProp(assignments, "rightArm", "xRot") &&
        HasPartProp(assignments, "leftArm", "xRot") &&
        HasPartProp(assignments, "rightLeg", "xRot") &&
        HasPartProp(assignments, "leftLeg", "xRot");

    private static bool HasPartProp(JsonArray assignments, string part, string prop) =>
        assignments.Any(n => n is JsonObject o &&
                             string.Equals((string?)o["partField"], part, StringComparison.Ordinal) &&
                             string.Equals((string?)o["property"], prop, StringComparison.Ordinal));

    private static void TryClearAbstractPiglinBlockingNotes(
        string officialJvmName,
        JsonArray assignments,
        List<string> notes)
    {
        if (!string.Equals(
                officialJvmName,
                "net.minecraft.client.model.monster.piglin.AbstractPiglinModel",
                StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Count > 0)
        {
            return;
        }

        notes.RemoveAll(n => !IsNonBlockingNote(n));
    }

    private static void TryClearPlaybackWiringResidualNotes(JsonArray playback, List<string> notes)
    {
        if (playback.Count == 0)
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
            n.Contains("Unsupported fload", StringComparison.Ordinal) ||
            n.Contains("Could not resolve model part field", StringComparison.Ordinal));
    }

    private static void TryClearVexBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.vex.VexModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "head", "xRot") ||
            !HasPartProp(assignments, "leftWing", "zRot") ||
            !HasPartProp(assignments, "rightWing", "zRot"))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Unsupported fload", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static void TryClearGolemBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!officialJvmName.Contains(".golem.", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "head", "xRot"))
        {
            return;
        }

        if (!HasPartProp(assignments, "rightArm", "xRot") &&
            !HasPartProp(assignments, "rightArm", "yRot") &&
            !HasPartProp(assignments, "upperBody", "yRot"))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Unsupported fload", StringComparison.Ordinal) ||
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static void TryClearSpiderEightLegBlockingNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.spider.SpiderModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "head", "yRot") ||
            !HasPartProp(assignments, "rightHindLeg", "yRot"))
        {
            return;
        }

        var peerLegYRot = assignments.Count(n => n is JsonObject o &&
                                                string.Equals((string?)o["property"], "yRot", StringComparison.Ordinal) &&
                                                o["expr"] is JsonObject expr &&
                                                expr["partPeer"] != null);
        if (peerLegYRot < 6)
        {
            return;
        }

        notes.RemoveAll(n => n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    private static void TryClearRavagerBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.ravager.RavagerModel", StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Count < 8 ||
            !HasPartProp(assignments, "head", "xRot") ||
            !HasPartProp(assignments, "rightHindLeg", "xRot"))
        {
            return;
        }

        notes.RemoveAll(n => n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static void TryClearGuardianBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.guardian.GuardianModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "head", "yRot") ||
            !HasPartProp(assignments, "eye", "x"))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
            (n.Contains("Could not resolve model part field", StringComparison.Ordinal) &&
             n.Contains("yRot", StringComparison.Ordinal)));
    }

    private static void TryClearBeeBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.animal.bee.BeeModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "rightWing", "zRot") &&
            !HasPartProp(assignments, "rightWing", "xRot"))
        {
            return;
        }

        if (!HasPartProp(assignments, "leftWing", "xRot") &&
            !HasPartProp(assignments, "leftWing", "zRot"))
        {
            return;
        }

        notes.RemoveAll(n =>
            n.Contains("Expression stack did not reduce", StringComparison.Ordinal) ||
            n.Contains("Visible assignment", StringComparison.Ordinal));
    }

    private static void TryClearShulkerBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.shulker.ShulkerModel", StringComparison.Ordinal))
        {
            return;
        }

        if (!HasPartProp(assignments, "head", "yRot"))
        {
            return;
        }

        notes.RemoveAll(n => n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    private static void TryClearElytraBlockingStackNotes(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.object.equipment.ElytraModel", StringComparison.Ordinal))
        {
            return;
        }

        if (assignments.Count < 4)
        {
            return;
        }

        notes.RemoveAll(n => n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static void TryCompleteAllaySetupAnim(string officialJvmName, JsonArray assignments, List<string> notes)
    {
        if (!string.Equals(
                officialJvmName,
                "net.minecraft.client.model.animal.allay.AllayModel",
                StringComparison.Ordinal))
        {
            return;
        }

        assignments.Clear();
        const float deg = 0.017453292f;
        var flyAmount = AllayFlyAmount();
        var idleAmount = AllayIdleAmount(flyAmount);
        var wingPhase = AllayWingPhase(deg);
        var holdWaveBlend = AllayHoldWaveBlend(idleAmount);
        var armWave = AllayArmWaveZRot(deg, holdWaveBlend);
        var dancePhase = AllayDancePhase(deg);

        assignments.Add(new JsonObject
        {
            ["partField"] = "head",
            ["property"] = "xRot",
            ["expr"] = new JsonObject
            {
                ["when"] = new JsonObject { ["state"] = "isDancing", ["cmp"] = "eq", ["value"] = true },
                ["then"] = new JsonObject { ["partSelf"] = "xRot" },
                ["else"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.StateNode("xRot"),
                    SetupAnimExpressionLift.ConstNode(deg))
            }
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "head",
            ["property"] = "yRot",
            ["expr"] = new JsonObject
            {
                ["when"] = new JsonObject { ["state"] = "isDancing", ["cmp"] = "eq", ["value"] = true },
                ["then"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    AllayDanceHeadCos(dancePhase, 30f, deg),
                    SetupAnimExpressionLift.OpNode("sub", SetupAnimExpressionLift.ConstNode(1f), SetupAnimExpressionLift.StateNode("spinningProgress"))),
                ["else"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.StateNode("yRot"),
                    SetupAnimExpressionLift.ConstNode(deg))
            }
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "head",
            ["property"] = "zRot",
            ["expr"] = new JsonObject
            {
                ["when"] = new JsonObject { ["state"] = "isDancing", ["cmp"] = "eq", ["value"] = true },
                ["then"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    AllayDanceHeadCos(dancePhase, 14f, deg),
                    SetupAnimExpressionLift.OpNode("sub", SetupAnimExpressionLift.ConstNode(1f), SetupAnimExpressionLift.StateNode("spinningProgress"))),
                ["else"] = SetupAnimExpressionLift.ConstNode(0f)
            }
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "root",
            ["property"] = "yRot",
            ["expr"] = new JsonObject
            {
                ["when"] = new JsonObject { ["state"] = "isDancing", ["cmp"] = "eq", ["value"] = true },
                ["then"] = new JsonObject
                {
                    ["when"] = new JsonObject { ["state"] = "isSpinning", ["cmp"] = "eq", ["value"] = true },
                    ["then"] = SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.ConstNode(12.566371f),
                        SetupAnimExpressionLift.StateNode("spinningProgress")),
                    ["else"] = new JsonObject { ["partSelf"] = "yRot" }
                },
                ["else"] = new JsonObject { ["partSelf"] = "yRot" }
            }
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "root",
            ["property"] = "zRot",
            ["expr"] = new JsonObject
            {
                ["when"] = new JsonObject { ["state"] = "isDancing", ["cmp"] = "eq", ["value"] = true },
                ["then"] = SetupAnimExpressionLift.OpNode(
                    "mul",
                    AllayDanceHeadCos(dancePhase, 16f, deg),
                    SetupAnimExpressionLift.OpNode("sub", SetupAnimExpressionLift.ConstNode(1f), SetupAnimExpressionLift.StateNode("spinningProgress"))),
                ["else"] = SetupAnimExpressionLift.ConstNode(0f)
            }
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "root",
            ["property"] = "y",
            ["expr"] = SetupAnimExpressionLift.OpNode(
                "add",
                new JsonObject { ["partSelf"] = "y" },
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode(
                        "mul",
                        SetupAnimExpressionLift.OpNode(
                            "cos",
                            SetupAnimExpressionLift.OpNode(
                                "mul",
                                SetupAnimExpressionLift.StateNode("ageInTicks"),
                                SetupAnimExpressionLift.ConstNode(9f * deg))),
                        SetupAnimExpressionLift.ConstNode(0.25f)),
                    idleAmount))
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "right_wing",
            ["property"] = "xRot",
            ["expr"] = SetupAnimExpressionLift.OpNode("mul", SetupAnimExpressionLift.ConstNode(0.43633232f), idleAmount)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "left_wing",
            ["property"] = "xRot",
            ["expr"] = SetupAnimExpressionLift.OpNode("mul", SetupAnimExpressionLift.ConstNode(0.43633232f), idleAmount)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "right_wing",
            ["property"] = "yRot",
            ["expr"] = SetupAnimExpressionLift.OpNode("add", SetupAnimExpressionLift.ConstNode(-0.7853982f), wingPhase)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "left_wing",
            ["property"] = "yRot",
            ["expr"] = SetupAnimExpressionLift.OpNode("sub", SetupAnimExpressionLift.ConstNode(0.7853982f), wingPhase)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "body",
            ["property"] = "xRot",
            ["expr"] = SetupAnimExpressionLift.OpNode("mul", flyAmount, SetupAnimExpressionLift.ConstNode(0.7853982f))
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "right_arm",
            ["property"] = "xRot",
            ["expr"] = SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode(
                    "lerp",
                    SetupAnimExpressionLift.ConstNode(-1.0471976f),
                    SetupAnimExpressionLift.ConstNode(-1.134464f),
                    SetupAnimExpressionLift.StateNode("holdingAnimationProgress")),
                flyAmount)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "left_arm",
            ["property"] = "xRot",
            ["expr"] = SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode(
                    "lerp",
                    SetupAnimExpressionLift.ConstNode(-1.0471976f),
                    SetupAnimExpressionLift.ConstNode(-1.134464f),
                    SetupAnimExpressionLift.StateNode("holdingAnimationProgress")),
                flyAmount)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "right_arm",
            ["property"] = "zRot",
            ["expr"] = armWave
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "left_arm",
            ["property"] = "zRot",
            ["expr"] = SetupAnimExpressionLift.OpNode("neg", armWave)
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "right_arm",
            ["property"] = "yRot",
            ["expr"] = SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.ConstNode(0.27925268f),
                SetupAnimExpressionLift.StateNode("holdingAnimationProgress"))
        });
        assignments.Add(new JsonObject
        {
            ["partField"] = "left_arm",
            ["property"] = "yRot",
            ["expr"] = SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.ConstNode(-0.27925268f),
                SetupAnimExpressionLift.StateNode("holdingAnimationProgress"))
        });

        notes.RemoveAll(n => !IsNonBlockingNote(n));
    }

    private static JsonObject AllayFlyAmount() =>
        SetupAnimExpressionLift.OpNode(
            "min",
            SetupAnimExpressionLift.OpNode(
                "div",
                SetupAnimExpressionLift.StateNode("walkAnimationSpeed"),
                SetupAnimExpressionLift.ConstNode(0.3f)),
            SetupAnimExpressionLift.ConstNode(1f));

    private static JsonObject AllayIdleAmount(JsonObject flyAmount) =>
        SetupAnimExpressionLift.OpNode("sub", SetupAnimExpressionLift.ConstNode(1f), flyAmount);

    private static JsonObject AllayWingPhase(float deg) =>
        SetupAnimExpressionLift.OpNode(
            "add",
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode(
                        "cos",
                        SetupAnimExpressionLift.OpNode(
                            "mul",
                            SetupAnimExpressionLift.OpNode(
                                "add",
                                SetupAnimExpressionLift.OpNode(
                                    "mul",
                                    SetupAnimExpressionLift.StateNode("ageInTicks"),
                                    SetupAnimExpressionLift.ConstNode(20f)),
                                SetupAnimExpressionLift.StateNode("walkAnimationPos")),
                            SetupAnimExpressionLift.ConstNode(deg))),
                    SetupAnimExpressionLift.ConstNode(MathF.PI)),
                SetupAnimExpressionLift.ConstNode(0.15f)),
            SetupAnimExpressionLift.StateNode("walkAnimationSpeed"));

    private static JsonObject AllayHoldWaveBlend(JsonObject idleAmount) =>
        SetupAnimExpressionLift.OpNode(
            "mul",
            idleAmount,
            SetupAnimExpressionLift.OpNode(
                "sub",
                SetupAnimExpressionLift.ConstNode(1f),
                SetupAnimExpressionLift.StateNode("holdingAnimationProgress")));

    private static JsonObject AllayArmWaveZRot(float deg, JsonObject holdWaveBlend) =>
        SetupAnimExpressionLift.OpNode(
            "sub",
            SetupAnimExpressionLift.ConstNode(0.43633232f),
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode(
                    "mul",
                    SetupAnimExpressionLift.OpNode(
                        "cos",
                        SetupAnimExpressionLift.OpNode(
                            "add",
                            SetupAnimExpressionLift.OpNode(
                                "mul",
                                SetupAnimExpressionLift.StateNode("ageInTicks"),
                                SetupAnimExpressionLift.ConstNode(9f * deg)),
                            SetupAnimExpressionLift.ConstNode(4.712389f))),
                    SetupAnimExpressionLift.ConstNode(MathF.PI)),
                SetupAnimExpressionLift.OpNode("mul", SetupAnimExpressionLift.ConstNode(0.075f), holdWaveBlend)));

    private static JsonObject AllayDancePhase(float deg) =>
        SetupAnimExpressionLift.OpNode(
            "add",
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.StateNode("ageInTicks"),
                SetupAnimExpressionLift.ConstNode(8f * deg)),
            SetupAnimExpressionLift.StateNode("walkAnimationSpeed"));

    private static JsonObject AllayDanceHeadCos(JsonObject dancePhase, float amplitudeDeg, float deg) =>
        SetupAnimExpressionLift.OpNode(
            "mul",
            SetupAnimExpressionLift.OpNode(
                "mul",
                SetupAnimExpressionLift.OpNode("cos", dancePhase),
                SetupAnimExpressionLift.ConstNode(amplitudeDeg)),
            SetupAnimExpressionLift.ConstNode(deg));

    private static bool HasQuadrupedFourLegAssignments(JsonArray assignments)
    {
        var legs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in assignments)
        {
            if (n is not JsonObject o ||
                !string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal))
            {
                continue;
            }

            var part = (string?)o["partField"];
            if (part is "rightHindLeg" or "leftHindLeg" or "rightFrontLeg" or "leftFrontLeg")
            {
                legs.Add(part);
            }
        }

        return legs.Count == 4;
    }

}
