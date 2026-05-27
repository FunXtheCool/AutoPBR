using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
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

}
