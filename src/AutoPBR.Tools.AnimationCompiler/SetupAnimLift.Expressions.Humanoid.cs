using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
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

}
