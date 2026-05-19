using System.Text.Json;



namespace AutoPBR.Core.Tests;

public sealed class GeometryIrLiftPolicyTests
{
    [Fact]
    public void EvaluateDocument_rejects_direction_mask_full_box()
    {
        const string json = """
        {
          "roots": [{
            "id": "root",
            "cuboids": [{
              "from": [0,0,0], "to": [1,1,1], "uvOrigin": [0,0],
              "liftKind": "direction_mask_full_box"
            }],
            "children": []
          }]
        }
        """;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(GeometryIrLiftPolicyDecision.RejectForParity,
            GeometryIrLiftPolicy.EvaluateDocument(doc.RootElement));
    }

    [Fact]
    public void EvaluateDocument_allows_exact_cuboids()
    {
        const string json = """
        {
          "roots": [{
            "id": "root",
            "cuboids": [{
              "from": [0,0,0], "to": [1,1,1], "uvOrigin": [0,0],
              "liftKind": "exact"
            }],
            "children": []
          }]
        }
        """;
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(GeometryIrLiftPolicyDecision.Emit,
            GeometryIrLiftPolicy.EvaluateDocument(doc.RootElement));
    }
}
