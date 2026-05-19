using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GeometryIrReferenceBakeGateTests
{
    [Fact]
    public void Gate_rejects_shard_when_counts_match_but_cuboid_geometry_differs()
    {
        var reference = JsonDocument.Parse("""
            {
              "extractionStatus": "reference_java",
              "roots": [{
                "id": "body",
                "cuboids": [{ "from": [0,0,0], "to": [1,1,1], "uvOrigin": [0,0] }],
                "children": []
              }]
            }
            """);

        var shard = new JsonObject
        {
            ["profile"] = "named_jar_26.1.2",
            ["extractionStatus"] = "ok",
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
                            ["uvOrigin"] = new JsonArray { 0, 0 }
                        }
                    },
                    ["children"] = new JsonArray()
                }
            }
        };

        var refPath = Path.Combine(
            Program.FindRepoRoot(),
            "tools",
            "MinecraftGeometryReference",
            "reference-output",
            "net.minecraft.client.model.test.GateFingerprintModel.json");
        var refDir = Path.GetDirectoryName(refPath)!;
        Directory.CreateDirectory(refDir);
        var hadRef = File.Exists(refPath);
        try
        {
            File.WriteAllText(refPath, reference.RootElement.GetRawText());
            var issues = new List<GeometryIrStructuralValidator.Issue>();
            var ok = GeometryIrReferenceBakeGate.Apply(
                "net.minecraft.client.model.test.GateFingerprintModel",
                shard,
                liftSucceeded: true,
                issues);
            Assert.False(ok);
            Assert.Contains(issues, i => i.Code == "reference_mismatch");
        }
        finally
        {
            if (hadRef)
            {
                File.WriteAllText(refPath, reference.RootElement.GetRawText());
            }
            else if (File.Exists(refPath))
            {
                File.Delete(refPath);
            }
        }
    }
}
