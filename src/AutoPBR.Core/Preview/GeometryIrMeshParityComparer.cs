using System.Numerics;


namespace AutoPBR.Core.Preview;

/// <summary>
/// Compares two emitted block models by sorted world-space corner sets.
/// </summary>
internal static class GeometryIrMeshParityComparer
{
    public readonly record struct CompareResult(bool IsMatch, float MaxCornerDelta, string? Message);

    public static CompareResult Compare(MergedJavaBlockModel expected, MergedJavaBlockModel actual, float tolerance = 0.01f)
    {
        if (expected.Elements.Count != actual.Elements.Count)
        {
            return new CompareResult(false, float.MaxValue,
                $"element count expected={expected.Elements.Count} actual={actual.Elements.Count}");
        }

        var sortedExpected = expected.Elements.OrderBy(ElementSortKey).ToList();
        var sortedActual = actual.Elements.OrderBy(ElementSortKey).ToList();
        var maxDelta = 0f;
        for (var i = 0; i < sortedExpected.Count; i++)
        {
            var d = ElementCornerDelta(sortedExpected[i], sortedActual[i]);
            if (d > maxDelta)
            {
                maxDelta = d;
            }

            if (d > tolerance)
            {
                return new CompareResult(false, maxDelta,
                    $"element {i} max corner delta {d:G6} exceeds tolerance {tolerance:G6}");
            }
        }

        return new CompareResult(true, maxDelta, null);
    }

    private static string ElementSortKey(ModelElement e)
    {
        GetWorldMinCorner(e, out var wMin);
        return $"{wMin.X:F4},{wMin.Y:F4},{wMin.Z:F4},{e.From[0]:F4},{e.From[1]:F4},{e.From[2]:F4},{e.To[0]:F4},{e.To[1]:F4},{e.To[2]:F4}";
    }

    private static void GetWorldMinCorner(ModelElement e, out Vector3 wMin)
    {
        wMin = new Vector3(float.PositiveInfinity);
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (e.From[0], e.From[1], e.From[2]),
            (e.To[0], e.From[1], e.From[2]),
            (e.From[0], e.To[1], e.From[2]),
            (e.To[0], e.To[1], e.From[2]),
            (e.From[0], e.From[1], e.To[2]),
            (e.To[0], e.From[1], e.To[2]),
            (e.From[0], e.To[1], e.To[2]),
            (e.To[0], e.To[1], e.To[2]),
        ];
        foreach (var (x, y, z) in corners)
        {
            wMin = Vector3.Min(wMin, Vector3.Transform(new Vector3(x, y, z), e.LocalToParent));
        }
    }

    private static float ElementCornerDelta(ModelElement a, ModelElement b)
    {
        var cornersA = GetWorldCorners(a).ToArray();
        var cornersB = GetWorldCorners(b).ToArray();
        if (cornersA.Length != cornersB.Length)
        {
            return float.MaxValue;
        }

        Array.Sort(cornersA, CornerSortComparer);
        Array.Sort(cornersB, CornerSortComparer);
        var maxDelta = 0f;
        for (var i = 0; i < cornersA.Length; i++)
        {
            var d = Vector3.Distance(cornersA[i], cornersB[i]);
            if (d > maxDelta)
            {
                maxDelta = d;
            }
        }

        return maxDelta;
    }

    private static readonly Comparison<Vector3> CornerSortComparer = (x, y) =>
    {
        var c = x.X.CompareTo(y.X);
        if (c != 0)
        {
            return c;
        }

        c = x.Y.CompareTo(y.Y);
        return c != 0 ? c : x.Z.CompareTo(y.Z);
    };

    private static IEnumerable<Vector3> GetWorldCorners(ModelElement e)
    {
        var local = new[]
        {
            new Vector3(e.From[0], e.From[1], e.From[2]),
            new Vector3(e.To[0], e.From[1], e.From[2]),
            new Vector3(e.From[0], e.To[1], e.From[2]),
            new Vector3(e.To[0], e.To[1], e.From[2]),
            new Vector3(e.From[0], e.From[1], e.To[2]),
            new Vector3(e.To[0], e.From[1], e.To[2]),
            new Vector3(e.From[0], e.To[1], e.To[2]),
            new Vector3(e.To[0], e.To[1], e.To[2])
        };

        foreach (var c in local)
        {
            yield return Vector3.Transform(c, e.LocalToParent);
        }
    }
}
