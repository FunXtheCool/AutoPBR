using System.Numerics;

using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    private static bool IsHangingSignBoardElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width > 12f && MathF.Abs(height - 10f) < 0.01f;
    }

    private static bool IsHangingSignChainElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width < 5f && MathF.Abs(height - 6f) < 0.01f;
    }

    private static bool IsHangingSignWallPlankElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width > 14f && MathF.Abs(height - 2f) < 0.01f;
    }

    private static bool IsHangingSignVerticalChainElement(ModelElement el)
    {
        var width = el.To[0] - el.From[0];
        var height = el.To[1] - el.From[1];
        return width > 10f && MathF.Abs(height - 6f) < 0.01f;
    }

    private static IEnumerable<Vector3> CollectWorldCorners(MergedJavaBlockModel model)
    {
        foreach (var el in model.Elements)
        {
            var m = el.LocalToParent;
            var fx = el.From[0];
            var fy = el.From[1];
            var fz = el.From[2];
            var tx = el.To[0];
            var ty = el.To[1];
            var tz = el.To[2];
            (float x, float y, float z)[] c =
            [
                (fx, fy, fz), (tx, fy, fz), (fx, ty, fz), (tx, ty, fz),
                (fx, fy, tz), (tx, fy, tz), (fx, ty, tz), (tx, ty, tz),
            ];
            foreach (var p in c)
            {
                yield return Vector3.Transform(new Vector3(p.x, p.y, p.z), m);
            }
        }
    }

    private static string CornerSortKey(Vector3 v) => $"{v.X:F4},{v.Y:F4},{v.Z:F4}";

    private static void TransformWorldCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        var m = el.LocalToParent;
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        var fx = el.From[0];
        var fy = el.From[1];
        var fz = el.From[2];
        var tx = el.To[0];
        var ty = el.To[1];
        var tz = el.To[2];
        ReadOnlySpan<(float x, float y, float z)> c =
        [
            (fx, fy, fz), (tx, fy, fz), (fx, ty, fz), (tx, ty, fz),
            (fx, fy, tz), (tx, fy, tz), (fx, ty, tz), (tx, ty, tz),
        ];
        foreach (var p in c)
        {
            var w = Vector3.Transform(new Vector3(p.x, p.y, p.z), m);
            min = Vector3.Min(min, w);
            max = Vector3.Max(max, w);
        }
    }
}
