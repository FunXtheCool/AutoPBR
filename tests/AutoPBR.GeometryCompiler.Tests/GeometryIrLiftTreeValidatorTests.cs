using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GeometryIrLiftTreeValidatorTests
{
    [Fact]
    public void Detects_duplicate_cuboid_across_parts()
    {
        var roots = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "root",
                ["pose"] = ZeroPose(),
                ["cuboids"] = new JsonArray(),
                ["children"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "body",
                        ["pose"] = ZeroPose(),
                        ["cuboids"] = new JsonArray
                        {
                            Cuboid(-4, -2, -9, 4, 2, 1, 0, 11)
                        },
                        ["children"] = new JsonArray()
                    },
                    new JsonObject
                    {
                        ["id"] = "wrong_leg",
                        ["pose"] = ZeroPose(),
                        ["cuboids"] = new JsonArray
                        {
                            Cuboid(-4, -2, -9, 4, 2, 1, 0, 11)
                        },
                        ["children"] = new JsonArray()
                    }
                }
            }
        };

        var result = GeometryIrLiftTreeValidator.ValidateRoots(roots, "test.Dup");
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "duplicate_cuboid_across_parts");
    }

    [Fact]
    public void Detects_flat_nested_gills_at_root()
    {
        var roots = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "root",
                ["pose"] = ZeroPose(),
                ["cuboids"] = new JsonArray(),
                ["children"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "head",
                        ["pose"] = ZeroPose(),
                        ["cuboids"] = new JsonArray(),
                        ["children"] = new JsonArray()
                    },
                    new JsonObject
                    {
                        ["id"] = "top_gills",
                        ["pose"] = ZeroPose(),
                        ["cuboids"] = new JsonArray { Cuboid(0, 0, 0, 1, 1, 1, 0, 0) },
                        ["children"] = new JsonArray()
                    }
                }
            }
        };

        var result = GeometryIrLiftTreeValidator.ValidateRoots(roots, "test.Flat");
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "flat_nested_part_at_root");
    }

    private static JsonObject ZeroPose() => new()
    {
        ["translation"] = new JsonArray { 0, 0, 0 },
        ["rotationEulerRad"] = new JsonArray { 0, 0, 0 },
        ["eulerOrder"] = "XYZ"
    };

    private static JsonObject Cuboid(float fx, float fy, float fz, float tx, float ty, float tz, int u, int v) => new()
    {
        ["from"] = new JsonArray { (double)fx, (double)fy, (double)fz },
        ["to"] = new JsonArray { (double)tx, (double)ty, (double)tz },
        ["uvOrigin"] = new JsonArray { u, v },
        ["textureKey"] = "#skin",
        ["liftKind"] = "exact"
    };
}
