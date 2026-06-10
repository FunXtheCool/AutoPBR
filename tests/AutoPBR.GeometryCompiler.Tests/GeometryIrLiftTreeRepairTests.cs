using System.Text.Json.Nodes;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GeometryIrLiftTreeRepairTests
{
    [Fact]
    public void Apply_unwraps_nested_definition_root_under_synthetic_wrapper()
    {
        var rootKids = new JsonArray
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
                        ["cuboids"] = new JsonArray { Cuboid(0, 0, 0, 1, 1, 1) },
                        ["children"] = new JsonArray()
                    }
                }
            }
        };

        var roots = GeometryLiftOutputAssembly.WrapSyntheticRoot(rootKids);
        roots = GeometryIrLiftTreeRepair.Apply(roots);

        var result = GeometryIrLiftTreeValidator.ValidateRoots(roots, "test.AllayLike");
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.DoesNotContain(result.Issues, i => i.Code == "duplicate_part_id");
    }

    [Fact]
    public void Apply_removes_degenerate_zero_thickness_cuboids()
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
                            Cuboid(0, 0, 0, 4, 4, 4),
                            Cuboid(0, 11, -10, 1.57079637, 11, -10)
                        },
                        ["children"] = new JsonArray()
                    }
                }
            }
        };

        roots = GeometryIrLiftTreeRepair.Apply(roots);
        var body = roots[0]!["children"]![0]!.AsObject();
        Assert.Single(body["cuboids"]!.AsArray());
    }

    private static JsonObject ZeroPose() => new()
    {
        ["translation"] = new JsonArray { 0, 0, 0 },
        ["rotationEulerRad"] = new JsonArray { 0, 0, 0 },
        ["eulerOrder"] = "XYZ"
    };

    [Fact]
    public void Apply_drops_empty_nested_definition_root_sibling()
    {
        var rootKids = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "root",
                ["pose"] = ZeroPose(),
                ["cuboids"] = new JsonArray(),
                ["children"] = new JsonArray()
            },
            new JsonObject
            {
                ["id"] = "shell",
                ["pose"] = ZeroPose(),
                ["cuboids"] = new JsonArray { Cuboid(0, 0, 0, 2, 2, 2) },
                ["children"] = new JsonArray()
            }
        };

        var roots = GeometryLiftOutputAssembly.WrapSyntheticRoot(rootKids);
        roots = GeometryIrLiftTreeRepair.Apply(roots);

        var result = GeometryIrLiftTreeValidator.ValidateRoots(roots, "test.NautilusArmor");
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Message)));
    }

    private static JsonObject Cuboid(double fx, double fy, double fz, double tx, double ty, double tz) => new()
    {
        ["from"] = new JsonArray { fx, fy, fz },
        ["to"] = new JsonArray { tx, ty, tz },
        ["uvOrigin"] = new JsonArray { 0, 0 },
        ["textureKey"] = "#skin",
        ["liftKind"] = "exact"
    };
}
