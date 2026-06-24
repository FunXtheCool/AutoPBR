namespace AutoPBR.Core.Preview;

/// <summary>
/// Java <c>ModelPart.Cube</c> (26.1.2) UV rectangles keyed by <see cref="JavaDirection"/>.
/// </summary>
internal static class EntityCuboidJavaUvConvention
{
    internal enum JavaDirection
    {
        Down,
        Up,
        North,
        South,
        East,
        West,
    }

    /// <summary>Texel bounds <c>[u0, v0, u1, v1]</c> for each Java direction (from javap of <c>ModelPart$Cube</c>).</summary>
    internal static float[] GetUvRect(JavaDirection direction, int u, int v, int w, int h, int d, bool mirrorU = false)
    {
        _ = mirrorU;
        var rect = direction switch
        {
            JavaDirection.Down => new float[] { u + d, v, u + d + w, v + d },
            JavaDirection.Up => new float[] { u + d + w, v + d, u + d + w + w, v },
            JavaDirection.West => new float[] { u, v + d, u + d, v + d + h },
            JavaDirection.North => new float[] { u + d, v + d, u + d + w, v + d + h },
            JavaDirection.East => new float[] { u + d + w, v + d, u + d + w + d, v + d + h },
            JavaDirection.South => new float[] { u + d + w + d, v + d, u + d + w + d + w, v + d + h },
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        if (mirrorU && rect[0] != rect[2])
        {
            (rect[0], rect[2]) = (rect[2], rect[0]);
        }

        return rect;
    }

    /// <summary>Face dictionary key for a Java direction (matches RigBuilder face keys).</summary>
    internal static string TemplateSlotName(JavaDirection direction) => PhysicalFaceName(direction);

    /// <summary>Physical plane name consumed by <see cref="MinecraftModelBaker"/> face emission.</summary>
    internal static string PhysicalFaceName(JavaDirection direction) => direction.ToString().ToLowerInvariant();
}
