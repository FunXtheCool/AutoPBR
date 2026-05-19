using System.Numerics;

namespace AutoPBR.Core.Preview;

/// <summary>
/// When active, <see cref="CleanRoomEntityModelRuntime.RigBuilder"/> records per-<c>AddBox</c> bone matrices only
/// (same order as full mesh elements) for GPU skinning without allocating cuboid faces.
/// </summary>
internal static class EntityRigPoseCapture
{
    [ThreadStatic]
    private static List<Matrix4x4>? t_target;

    public static bool IsActive => t_target is not null;

    public static IDisposable Use(List<Matrix4x4> target)
    {
        var prev = t_target;
        t_target = target;
        return new Pop(prev);
    }

    public static void Append(in Matrix4x4 meshLocal) => t_target!.Add(meshLocal);

    private sealed class Pop : IDisposable
    {
        private readonly List<Matrix4x4>? _prev;

        public Pop(List<Matrix4x4>? prev) => _prev = prev;

        public void Dispose() => t_target = _prev;
    }
}
