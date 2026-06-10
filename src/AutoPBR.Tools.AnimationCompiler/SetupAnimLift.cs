using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
    private static readonly Regex InstructionStartRegex = new(@"^\s+\d+:", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex TypedSetupAnimRegex = new(
        @"public\s+void\s+setupAnim\s*\(\s*([^\)]+)\s*\)\s*;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));
    private static readonly Regex ModelPartPutfieldRegex = new(
        @"putfield\s+#\d+\s+//\s+Field\s+net/minecraft/client/model/geom/ModelPart\.(\w+):",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));
    private static readonly Regex ModelPartFieldGetRegex = new(
        @"getfield\s+#\d+\s+//\s+Field\s+(\w+):Lnet/minecraft/client/model/geom/ModelPart;",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));
    private static readonly Regex InvokespecialSetupAnimRegex = new(
        @"invokespecial\s+#\d+\s+//\s+Method\s+([\w$/\.]+)\.setupAnim:",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    public static bool IsNonBlockingNote(string note) =>
        note.Contains("Visible assignment", StringComparison.Ordinal) ||
        note.Contains("SetupAnim IR lifted from javap", StringComparison.Ordinal) ||
        note.Contains("No local setupAnim body", StringComparison.Ordinal) ||
        note.Contains("SetupAnim rules hoisted from", StringComparison.Ordinal) ||
        note.Contains("No setupAnim bytecode", StringComparison.Ordinal) ||
        note.Contains("setupAnim delegates to resetPose only", StringComparison.Ordinal) ||
        note.Contains("Interface host", StringComparison.Ordinal) ||
        note.Contains("Abstract EntityModel host", StringComparison.Ordinal) ||
        note.Contains("squish/scale driven by SlimeRenderer", StringComparison.Ordinal) ||
        note.Contains("effect model uses parent pose only", StringComparison.Ordinal) ||
        note.Contains("Unsupported ModelPart property", StringComparison.Ordinal) ||
        (note.Contains("Could not resolve model part field", StringComparison.Ordinal) &&
         note.Contains("(visible)", StringComparison.Ordinal));

    private static readonly Dictionary<string, string> AbstractSetupAnimHoistTemplates =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["net.minecraft.client.model.animal.feline.AbstractFelineModel"] =
                "net.minecraft.client.model.animal.feline.AdultFelineModel"
        };
    public static bool TryLift(string javapStdout, string officialJvmName, out JsonObject shard, out List<string> notes)
    {
        shard = new JsonObject();
        notes = [];
        if (!TryExtractTypedSetupAnimCode(javapStdout, out var renderStateType, out var code))
        {
            notes.Add("No typed setupAnim(RenderState) Code block found in javap output.");
            return false;
        }

        var lines = MergeInstructionContinuation(code.Split('\n').Select(l => l.TrimEnd('\r')).ToList());
        var modelAccessors = SetupAnimModelAccessorTable.Parse(javapStdout);
        var assignments = new JsonArray();
        string? inheritsFrom = null;
        var superCall = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var super = InvokespecialSetupAnimRegex.Match(line);
            if (super.Success && i < 12)
            {
                superCall = true;
                inheritsFrom = NormalizeTypeName(super.Groups[1].Value);
                continue;
            }

            if (!line.Contains("putfield", StringComparison.Ordinal) ||
                !ModelPartPutfieldRegex.IsMatch(line))
            {
                continue;
            }

            var prop = ModelPartPutfieldRegex.Match(line).Groups[1].Value;
            if (!IsSupportedProperty(prop))
            {
                notes.Add($"Unsupported ModelPart property {prop} at line {i}.");
                continue;
            }

            if (string.Equals(prop, "visible", StringComparison.Ordinal))
            {
                var visPart = FindPartFieldForPutfield(lines, i);
                if (string.IsNullOrEmpty(visPart))
                {
                    notes.Add($"Could not resolve model part field for putfield at line {i} ({prop}).");
                    continue;
                }

                if (!TryLiftVisibleAssignment(lines, i, out var visExpr))
                {
                    notes.Add($"Visible assignment at line {i} not lifted.");
                    continue;
                }

                assignments.Add(new JsonObject
                {
                    ["partField"] = visPart,
                    ["property"] = "visible",
                    ["expr"] = visExpr
                });
                continue;
            }

            if (TryLiftPartPeerCopy(lines, i, prop, out var peerPart, out var peerExpr))
            {
                assignments.Add(new JsonObject
                {
                    ["partField"] = peerPart,
                    ["property"] = prop,
                    ["expr"] = peerExpr
                });
                continue;
            }

            var partField = FindPartFieldForPutfield(lines, i);
            if (string.IsNullOrEmpty(partField))
            {
                notes.Add($"Could not resolve model part field for putfield at line {i} ({prop}).");
                continue;
            }

            if (!SetupAnimExpressionLift.TryLiftAssignmentExpr(lines, i, out var expr, out var exprNotes, modelAccessors))
            {
                notes.AddRange(exprNotes.Select(n => $"Line {i} {prop}: {n}"));
                continue;
            }

            assignments.Add(new JsonObject
            {
                ["partField"] = partField,
                ["property"] = prop,
                ["expr"] = expr
            });
        }

        TryUnrollModelPartArrayLoops(lines, officialJvmName, assignments, notes, modelAccessors);
        TryCompleteSquidTentacleAssignments(officialJvmName, lines, assignments, notes);
        TryCompleteSegmentArrayMobAssignments(officialJvmName, lines, assignments, notes, modelAccessors);
        TryCompleteMagmaCubeBodyCubeY(officialJvmName, lines, assignments, notes);
        TryCompleteSpinAttackBoxYRot(officialJvmName, lines, assignments, notes);
        TryCompleteWormSegmentSetupAnim(officialJvmName, lines, assignments, notes);

        AnimationModelWiringLift.TryLift(javapStdout, out var baked, out var playback, out var wiringNotes);
        notes.AddRange(wiringNotes);

        shard["renderStateType"] = renderStateType;
        shard["setupAnimMethod"] = "setupAnim";
        if (!string.IsNullOrEmpty(inheritsFrom))
        {
            shard["inheritsSetupAnimFrom"] = inheritsFrom;
        }

        if (superCall)
        {
            shard["superSetupAnimCall"] = true;
        }

        shard["assignments"] = assignments;
        if (baked.Count > 0)
        {
            shard["bakedAnimations"] = baked;
        }

        if (playback.Count > 0)
        {
            shard["playbackSteps"] = playback;
        }

        TryCompleteQuadrupedLegAssignments(assignments, notes);
        if (IsEquineSetupAnimHost(officialJvmName))
        {
            TryCompleteEquineLegXRotAssignments(lines, assignments, modelAccessors, notes);
            TryCompleteEquineHeadBodyTailXRotAssignments(assignments, modelAccessors, notes);
            TryCompleteEquineTailDupOffsets(assignments, notes);
        }

        TryCompleteAllaySetupAnim(officialJvmName, assignments, notes);
        TryCompleteHumanoidHostSetupAnim(officialJvmName, assignments, notes);
        if (HasQuadrupedFourLegAssignments(assignments))
        {
            notes.RemoveAll(n => n.Contains("Unsupported fload", StringComparison.Ordinal));
        }

        TryClearFelineBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearHumanoidBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearAbstractPiglinBlockingNotes(officialJvmName, assignments, notes);
        TryClearPlaybackWiringResidualNotes(playback, notes);
        TryClearVexBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearGolemBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearSpiderEightLegBlockingNotes(officialJvmName, assignments, notes);
        TryClearRavagerBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearGuardianBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearBeeBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearShulkerBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearElytraBlockingStackNotes(officialJvmName, assignments, notes);
        TryClearAquaticArrayMobNotes(officialJvmName, assignments, notes);
        TryClearBlazeRodArrayNotes(officialJvmName, assignments, notes);
        TryClearEnderDragonResidualNotes(officialJvmName, assignments, notes);
        TryClearSlimeFamilySegmentNotes(officialJvmName, assignments, notes);
        TryClearEvokerFangsResidualNotes(officialJvmName, assignments, notes);
        TryClearBookModelResidualNotes(officialJvmName, assignments, notes);
        TryClearSpinAttackBoxesNotes(officialJvmName, assignments, notes);

        if (assignments.Count == 0 && playback.Count == 0 && inheritsFrom is null)
        {
            notes.Add("No assignments or playback steps lifted.");
            return false;
        }

        return true;
    }

    private static bool IsSupportedProperty(string prop) =>
        prop is "xRot" or "yRot" or "zRot" or "x" or "y" or "z" or "xScale" or "yScale" or "zScale" or "visible";
}
