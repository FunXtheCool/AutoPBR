using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GeometryLiftForestMergeOverlayRelocateTests
{
    [Fact]
    public void ApplyMultiIslandPostMerge_Relocates_multiple_misplaced_overlays_from_same_parent()
    {
        var head = Part("head");
        var body = Part(
            "body",
            children:
            [
                Part("left_ear", translation: [0, 0, 0]),
                Part("right_ear", translation: [0, 0, 0]),
            ]);
        var root = new JsonArray { body, head };

        GeometryLiftForestMerge.ApplyMultiIslandPostMerge(root);

        var headKids = head["children"]!.AsArray();
        AssertChildPartId(headKids, "left_ear");
        AssertChildPartId(headKids, "right_ear");
        Assert.DoesNotContain(
            body["children"]!.AsArray(),
            n => n is JsonObject j &&
                 (string.Equals((string?)j["id"], "left_ear", StringComparison.Ordinal) ||
                  string.Equals((string?)j["id"], "right_ear", StringComparison.Ordinal)));
    }

    [Fact]
    public void ApplyMultiIslandPostMerge_Dedupes_duplicate_overlay_in_same_parent_before_relocate()
    {
        var head = Part("head");
        var body = Part(
            "body",
            children:
            [
                Part("left_ear", translation: [0, 0, 0]),
                Part("left_ear", translation: [4, 0, 0]),
            ]);
        var root = new JsonArray { body, head };

        GeometryLiftForestMerge.ApplyMultiIslandPostMerge(root);

        var headKids = head["children"]!.AsArray();
        Assert.Single(
            headKids,
            n => n is JsonObject j && string.Equals((string?)j["id"], "left_ear", StringComparison.Ordinal));
        Assert.DoesNotContain(
            body["children"]!.AsArray(),
            n => n is JsonObject j && string.Equals((string?)j["id"], "left_ear", StringComparison.Ordinal));
    }

    private static JsonObject Part(
        string id,
        double[]? translation = null,
        JsonArray? children = null)
    {
        var part = new JsonObject { ["id"] = id, ["cuboids"] = new JsonArray() };
        if (translation is not null)
        {
            part["pose"] = new JsonObject
            {
                ["translation"] = new JsonArray
                {
                    translation[0],
                    translation[1],
                    translation[2],
                },
            };
        }

        if (children is not null)
        {
            part["children"] = children;
        }

        return part;
    }

    private static void AssertChildPartId(JsonArray parts, string id) =>
        Assert.Contains(parts, n => n is JsonObject j && string.Equals((string?)j["id"], id, StringComparison.Ordinal));
}
