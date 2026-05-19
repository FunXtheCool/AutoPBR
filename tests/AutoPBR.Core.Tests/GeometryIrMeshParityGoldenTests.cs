using System.Numerics;


namespace AutoPBR.Core.Tests;

public sealed class GeometryIrMeshParityGoldenTests
{
    [Fact]
    public void Parity_emit_options_use_zero_degenerate_thickness()
    {
        var o = GeometryIrMeshEmitOptions.ForParity();
        Assert.Equal(GeometryIrEmitFidelity.Parity, o.Fidelity);
        Assert.Equal(0f, o.PreviewDegenerateAxisThickness);
    }

    internal static void AssertMeshesEquivalent(MergedJavaBlockModel a, MergedJavaBlockModel b, float tol)
    {
        Assert.Equal(a.Elements.Count, b.Elements.Count);
        var sortedA = a.Elements.OrderBy(SortKey).ToList();
        var sortedB = b.Elements.OrderBy(SortKey).ToList();
        for (var i = 0; i < sortedA.Count; i++)
        {
            AssertElementNear(sortedA[i], sortedB[i], tol);
        }
    }

    private static string SortKey(ModelElement e) =>
        $"{e.From[0]:F3},{e.From[1]:F3},{e.From[2]:F3},{e.To[0]:F3},{e.To[1]:F3},{e.To[2]:F3}";

    private static void AssertElementNear(ModelElement expected, ModelElement actual, float tol)
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.InRange(actual.From[i], expected.From[i] - tol, expected.From[i] + tol);
            Assert.InRange(actual.To[i], expected.To[i] - tol, expected.To[i] + tol);
        }

        AssertMatrixNear(expected.LocalToParent, actual.LocalToParent, tol);
    }

    private static void AssertMatrixNear(Matrix4x4 expected, Matrix4x4 actual, float tol)
    {
        Assert.InRange(actual.M41, expected.M41 - tol, expected.M41 + tol);
        Assert.InRange(actual.M42, expected.M42 - tol, expected.M42 + tol);
        Assert.InRange(actual.M43, expected.M43 - tol, expected.M43 + tol);
    }
}
