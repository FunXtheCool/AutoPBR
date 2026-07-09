using System.Text.Json;



namespace AutoPBR.Core.Tests;

public sealed class GeometryIrReferenceComparerTests
{
    [Fact]
    public void CompareReferenceToIrShardWithPoses_matches_identical_trees()
    {
        const string json = """
            {
              "extractionStatus": "ok",
              "roots": [{
                "id": "root",
                "pose": { "translation": [0,0,0], "rotationEulerRad": [0,0,0] },
                "cuboids": [{ "from": [0,0,0], "to": [1,1,1] }],
                "children": [{
                  "id": "arm",
                  "pose": { "translation": [1,2,0], "rotationEulerRad": [0.1,0,0] },
                  "cuboids": [{ "from": [-1,0,-1], "to": [1,2,1] }],
                  "children": []
                }]
              }]
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardWithPoses(
            doc.RootElement, doc.RootElement, cuboidTolerance: 0.01, poseTolerance: 0.01);
        Assert.True(cmp.IsMatch, cmp.Message);
        Assert.Equal(2, cmp.ReferencePoses);
    }
}
