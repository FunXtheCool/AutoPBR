using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
    public static bool TryWriteInheritanceOnlyShard(
        string officialJvmName,
        string inheritsFrom,
        out JsonObject shard,
        out List<string> notes)
    {
        shard = new JsonObject
        {
            ["inheritsSetupAnimFrom"] = inheritsFrom,
            ["assignments"] = new JsonArray(),
            ["setupAnimMethod"] = "setupAnim"
        };
        notes =
        [
            $"No local setupAnim body; inherits procedural rules from {inheritsFrom}."
        ];
        return true;
    }

    /// <summary>Models that extend <c>Model</c> without a <c>setupAnim</c> method (particle/effect shells).</summary>
    public static bool TryWriteNoSetupAnimEffectShard(
        string javapStdout,
        out JsonObject shard,
        out List<string> notes)
    {
        shard = new JsonObject();
        notes = [];
        if (javapStdout.Contains("void setupAnim", StringComparison.Ordinal))
        {
            return false;
        }

        if (!javapStdout.Contains("extends net.minecraft.client.model.Model", StringComparison.Ordinal) &&
            !javapStdout.Contains("extends net/minecraft/client/model/Model", StringComparison.Ordinal) &&
            !javapStdout.Contains("extends net.minecraft.client.model.EntityModel", StringComparison.Ordinal) &&
            !javapStdout.Contains("extends net/minecraft/client/model/EntityModel", StringComparison.Ordinal))
        {
            return false;
        }

        shard["inheritsSetupAnimFrom"] = javapStdout.Contains("EntityModel", StringComparison.Ordinal)
            ? "net.minecraft.client.model.EntityModel"
            : "net.minecraft.client.model.Model";
        shard["assignments"] = new JsonArray();
        shard["setupAnimMethod"] = "setupAnim";
        shard["setupAnimEffectOnly"] = true;
        notes.Add("No setupAnim bytecode; effect model uses parent pose only.");
        return true;
    }

    /// <summary>Slime outer body: scale/squish applied in <c>SlimeRenderer</c>, not model setupAnim.</summary>
    public static bool TryWriteRendererDrivenSlimeShard(
        string officialJvmName,
        string javapStdout,
        out JsonObject shard,
        out List<string> notes)
    {
        shard = new JsonObject();
        notes = [];
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.monster.slime.SlimeModel", StringComparison.Ordinal) ||
            javapStdout.Contains("void setupAnim", StringComparison.Ordinal))
        {
            return false;
        }

        shard["inheritsSetupAnimFrom"] = "net.minecraft.client.model.EntityModel";
        shard["assignments"] = new JsonArray();
        shard["setupAnimMethod"] = "setupAnim";
        shard["setupAnimEffectOnly"] = true;
        notes.Add("No setupAnim bytecode; squish/scale driven by SlimeRenderer (not model IR).");
        return true;
    }

    /// <summary>Interface mesh hosts (HeadedModel, ArmedModel, VillagerLikeModel) — no setupAnim on type.</summary>
    public static bool TryWriteInterfaceSetupAnimMarkerShard(
        string officialJvmName,
        string javapStdout,
        out JsonObject shard,
        out List<string> notes)
    {
        shard = new JsonObject();
        notes = [];
        if (!javapStdout.Contains("interface ", StringComparison.Ordinal) ||
            javapStdout.Contains("void setupAnim", StringComparison.Ordinal))
        {
            return false;
        }

        shard["assignments"] = new JsonArray();
        shard["setupAnimMethod"] = "setupAnim";
        shard["setupAnimEffectOnly"] = true;
        notes.Add($"Interface host {officialJvmName}; setupAnim lifted on concrete implementors.");
        return true;
    }

    /// <summary><c>Model.setupAnim</c> only resets part poses — honest effect-only ok for abstract bases.</summary>
    public static bool TryWriteModelResetPoseOnlyShard(
        string javapStdout,
        out JsonObject shard,
        out List<string> notes)
    {
        shard = new JsonObject();
        notes = [];
        if (!TryExtractTypedSetupAnimCode(javapStdout, out var renderStateType, out var code))
        {
            return false;
        }

        var lines = code.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var hasReset = lines.Any(l =>
            l.Contains("resetPose", StringComparison.Ordinal) &&
            l.Contains("invoke", StringComparison.Ordinal));
        var hasPartPut = lines.Any(l =>
            l.Contains("putfield", StringComparison.Ordinal) &&
            l.Contains("ModelPart.", StringComparison.Ordinal));
        if (!hasReset || hasPartPut)
        {
            return false;
        }

        shard["renderStateType"] = renderStateType;
        shard["setupAnimMethod"] = "setupAnim";
        shard["assignments"] = new JsonArray();
        shard["setupAnimEffectOnly"] = true;
        notes.Add("setupAnim delegates to resetPose only (Model base).");
        return true;
    }

    /// <summary>Abstract <c>EntityModel</c> — no local setupAnim; inherits <c>Model</c> resetPose.</summary>
    public static bool TryWriteEntityModelAbstractShard(
        string officialJvmName,
        string javapStdout,
        out JsonObject shard,
        out List<string> notes)
    {
        shard = new JsonObject();
        notes = [];
        if (!string.Equals(officialJvmName, "net.minecraft.client.model.EntityModel", StringComparison.Ordinal) ||
            javapStdout.Contains("void setupAnim", StringComparison.Ordinal))
        {
            return false;
        }

        shard["inheritsSetupAnimFrom"] = "net.minecraft.client.model.Model";
        shard["assignments"] = new JsonArray();
        shard["setupAnimMethod"] = "setupAnim";
        shard["setupAnimEffectOnly"] = true;
        notes.Add("Abstract EntityModel host; procedural setupAnim on Model.resetPose only.");
        return true;
    }
}
