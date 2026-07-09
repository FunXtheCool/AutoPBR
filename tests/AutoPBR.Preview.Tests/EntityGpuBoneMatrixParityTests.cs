using System.Numerics;

using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed class EntityGpuBoneMatrixParityTests
{
    private static bool IsFinite(in Matrix4x4 m) =>
        !(float.IsNaN(m.M11) || float.IsInfinity(m.M11) || float.IsNaN(m.M44) || float.IsInfinity(m.M44));

    [Fact]
    public void Fast_bone_fill_stays_finite_axolotl_blue_over_animation_window()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/axolotl/axolotl_blue.png";
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        const float idle = 0.31f;
        var scratch = new List<Matrix4x4>();
        for (var step = 0; step < 200; step++)
        {
            var t = step * 0.051f;
            scratch.Clear();
            Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, t, scratch, out var n, null));
            Assert.True(n > 0);
            foreach (var bone in scratch)
            {
                Assert.True(IsFinite(bone), $"Non-finite bone at t={t}");
            }
        }
    }

    private static bool AllClose(in Matrix4x4 a, in Matrix4x4 b, float eps = 2e-4f)
    {
        return Math.Abs(a.M11 - b.M11) <= eps && Math.Abs(a.M12 - b.M12) <= eps &&
               Math.Abs(a.M13 - b.M13) <= eps && Math.Abs(a.M14 - b.M14) <= eps &&
               Math.Abs(a.M21 - b.M21) <= eps && Math.Abs(a.M22 - b.M22) <= eps &&
               Math.Abs(a.M23 - b.M23) <= eps && Math.Abs(a.M24 - b.M24) <= eps &&
               Math.Abs(a.M31 - b.M31) <= eps && Math.Abs(a.M32 - b.M32) <= eps &&
               Math.Abs(a.M33 - b.M33) <= eps && Math.Abs(a.M34 - b.M34) <= eps &&
               Math.Abs(a.M41 - b.M41) <= eps && Math.Abs(a.M42 - b.M42) <= eps &&
               Math.Abs(a.M43 - b.M43) <= eps && Math.Abs(a.M44 - b.M44) <= eps;
    }

    [Fact]
    public void Fast_bone_fill_matches_merged_LocalToParent_axolotl_blue_at_clock()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/axolotl/axolotl_blue.png";
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        const float idle = 0.31f;
        const float anim = 1.847f;

        var scratch = new List<Matrix4x4>();
        Assert.True(runtime.TryFillBoneMatricesFast(path, profile, idle, anim, scratch, out var boneCount, null));
        Assert.True(runtime.TryBuildStaticMesh(path, profile, idle, anim, out var merged, out var provenance,
            applyGeometryIrSetupAnimMotion: true));
        if (provenance.Kind != PreviewMeshDriverKind.RuntimeGeometryIrJson)
        {
            return;
        }

        Assert.Equal(merged.Elements.Count, boneCount);
        Assert.True(boneCount > 0);
        for (var i = 0; i < boneCount; i++)
        {
            Assert.True(AllClose(scratch[i], merged.Elements[i].LocalToParent),
                $"Bone {i} mismatch (fast vs merged).");
        }
    }
}
