using System.Numerics;

namespace AutoPBR.Core.Preview;

internal static class EntityEmulatedGpuSkinningMath
{
    /// <summary>
    /// Component-wise cuboid scale (same as <see cref="MinecraftModelBaker"/> <c>W()</c> on vertex positions).
    /// </summary>
    internal static Vector3 PreviewCuboidNormalizeTexelPosition(in Vector3 texelModelPos) =>
        new(texelModelPos.X / 16f - 0.5f, texelModelPos.Y / 16f - 0.5f, texelModelPos.Z / 16f - 0.5f);

    /// <summary>
    /// Lifts the skinned mesh so the lowest preview-space vertex clears <paramref name="floorY"/>.
    /// Bind-pose interleaved positions are in <b>pre-cuboid-scale</b> texel model space (same as after <c>LocalToParent</c>,
    /// before <c>x/16−½</c>); the lift applies the same preview scale as the vertex shader tail.
    /// </summary>
    public static float ComputeMeshSpaceGroundLift(
        ReadOnlySpan<float> interleavedSkinned,
        float floorY,
        float clearance)
    {
        if (interleavedSkinned.Length < MinecraftModelBaker.FloatsPerSkinnedVertex)
        {
            return 0f;
        }

        var stride = MinecraftModelBaker.FloatsPerSkinnedVertex;
        var minY = float.PositiveInfinity;
        for (var i = 0; i + stride - 1 < interleavedSkinned.Length; i += stride)
        {
            var p = new Vector3(interleavedSkinned[i], interleavedSkinned[i + 1], interleavedSkinned[i + 2]);
            var ty = PreviewCuboidNormalizeTexelPosition(p).Y;
            minY = MathF.Min(minY, ty);
        }

        if (!float.IsFinite(minY))
        {
            return 0f;
        }

        var targetMinY = floorY + MathF.Max(0f, clearance);
        return MathF.Max(0f, targetMinY - minY);
    }
}
