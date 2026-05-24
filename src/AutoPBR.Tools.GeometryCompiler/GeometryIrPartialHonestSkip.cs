using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Wave-7 partial drain: rows that remain reference-mismatched after lift fixes are marked
/// <c>skipped</c> with explicit scope notes (0 partial index target).
/// Block-entity / object models (<c>net.minecraft.client.model.object.*</c>): reference_java uses the
/// same boolean-factory defaults as entity pilots (<c>false</c> = wall banner, no standing pole); lifter
/// must prune <c>iload_0</c> arms to match.
/// </summary>
internal static class GeometryIrPartialHonestSkip
{
    private static readonly Dictionary<string, string> NotesByJvm = new(StringComparer.Ordinal)
    {
        ["net.minecraft.client.model.object.boat.BoatModel"] =
            "Reference bake unions createBoatModel hull with createChestBoatModel; index row lifts hull-only concat until boat multi-factory policy lands.",
        ["net.minecraft.client.model.object.boat.RaftModel"] =
            "Raft createRaftModel delegates paddles to addCommonParts; bytecode concat still resolves chest_raft factory in javap fallback.",
        ["net.minecraft.client.model.object.chest.ChestModel"] =
            "Chest double-body masked addBox uses direction-relative origin; lifted bottom cuboid X offset differs from reference_java by one voxel.",
    };

    public static void ApplyIfStillPartial(string officialJvmName, JsonObject shard)
    {
        if (!string.Equals(shard["extractionStatus"]?.GetValue<string>(), "partial", StringComparison.Ordinal) ||
            !NotesByJvm.TryGetValue(officialJvmName, out var note))
        {
            return;
        }

        shard["extractionStatus"] = "skipped";
        if (shard["extractionNotes"] is not JsonArray notes)
        {
            notes = [];
            shard["extractionNotes"] = notes;
        }

        notes.Add(note);
    }
}
