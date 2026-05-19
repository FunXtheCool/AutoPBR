using System.Text.Json.Nodes;



namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GeometryIrStructuralValidatorTests
{
    [Fact]
    public void Strict_rejects_non_exact_lift_kind()
    {
        var shard = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["extractionStatus"] = "ok",
            ["liftSummary"] = new JsonObject
            {
                ["cuboidApproxCount"] = 0,
                ["poseApproxCount"] = 0
            },
            ["roots"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "root",
                    ["cuboids"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["from"] = new JsonArray { 0, 0, 0 },
                            ["to"] = new JsonArray { 1, 1, 1 },
                            ["uvOrigin"] = new JsonArray { 0, 0 },
                            ["liftKind"] = "direction_mask_full_box"
                        }
                    },
                    ["children"] = new JsonArray()
                }
            }
        };

        var result = GeometryIrStructuralValidator.ValidateShard(shard, "test", new GeometryIrStructuralValidator.Options(Strict: true));
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "lift_kind");
    }

    [Fact]
    public void Adjacency_flags_misaligned_touching_faces()
    {
        var shard = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["extractionStatus"] = "partial",
            ["roots"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "body",
                    ["cuboids"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["from"] = new JsonArray { 0, 0, 0 },
                            ["to"] = new JsonArray { 2, 2, 2 },
                            ["uvOrigin"] = new JsonArray { 0, 0 },
                            ["liftKind"] = "exact"
                        },
                        new JsonObject
                        {
                            ["from"] = new JsonArray { 2.01, 0, 0 },
                            ["to"] = new JsonArray { 4, 2, 2 },
                            ["uvOrigin"] = new JsonArray { 0, 0 },
                            ["liftKind"] = "exact"
                        }
                    },
                    ["children"] = new JsonArray()
                }
            }
        };

        var result = GeometryIrStructuralValidator.ValidateShard(shard, "gap-test",
            new GeometryIrStructuralValidator.Options(CheckAdjacency: true));
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "adjacency_gap");
    }

    [Fact]
    public void Packaged_cod_ok_shard_passes_strict()
    {
        var root = Program.FindRepoRoot();
        var path = Path.Combine(root, "docs", "generated", "geometry", "26.1.2",
            "net.minecraft.client.model.animal.fish.CodModel.json");
        if (!File.Exists(path))
        {
            return;
        }

        var result = GeometryIrStructuralValidator.ValidateFile(path,
            new GeometryIrStructuralValidator.Options(Strict: true, RequireOkStatus: false));
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Message)));
    }
}
