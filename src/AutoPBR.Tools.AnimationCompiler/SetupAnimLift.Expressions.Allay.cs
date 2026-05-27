using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
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

}
