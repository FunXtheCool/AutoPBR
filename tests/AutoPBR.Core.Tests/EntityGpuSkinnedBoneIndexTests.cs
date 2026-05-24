using System.Collections.Generic;

using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Xunit;

namespace AutoPBR.Core.Tests;

/// <summary>
/// GPU skinning bind-pose bake stores the element index in the last float word (bit-cast int) per
/// <see cref="MinecraftModelBaker.TryBakeBindPoseForGpuSkinning"/>; <see cref="GlMeshBuffer"/> binds it as
/// <c>glVertexAttribIPointer(4, …, offset 12 * sizeof(float))</c> for <c>layout(location = 4) in int aBoneIndex</c>.
/// </summary>
public sealed class EntityGpuSkinnedBoneIndexTests
{
    private static int DecodeBoneWord(float f) => BitConverter.SingleToInt32Bits(f);

    private static void BakeSkinned(
        string assetPath,
        out float[] verts,
        out int elementCount)
    {
        var runtime = new CleanRoomEntityModelRuntime();
        var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
        Assert.True(runtime.TryBuildStaticMesh(assetPath, profile, idlePhase01: 0f, animationTimeSeconds: 0f, out var merged));
        elementCount = merged.Elements.Count;
        Assert.True(elementCount > 0);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            pathToIdx[p] = i;
            texSizes[p] = (64, 32);
        }

        Assert.True(MinecraftModelBaker.TryBakeBindPoseForGpuSkinning(
            merged,
            "minecraft",
            pathToIdx,
            texSizes,
            out verts!,
            out _,
            out _));
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken.png")]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_cold.png")]
    public void Skinned_bake_bone_words_are_valid_and_cover_all_element_slots(string assetPath)
    {
        BakeSkinned(assetPath, out var verts, out var elementCount);
        const int stride = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex;
        Assert.Equal(13, stride);
        Assert.Equal(0, verts.Length % stride);
        var vertCount = verts.Length / stride;
        Assert.True(vertCount > 0);

        var counts = new int[elementCount];
        for (var vi = 0; vi < vertCount; vi++)
        {
            var bi = DecodeBoneWord(verts[vi * stride + 12]);
            Assert.InRange(bi, 0, elementCount - 1);
            counts[bi]++;
        }

        for (var i = 0; i < elementCount; i++)
        {
            Assert.True(counts[i] > 0, $"no vertices reference bone/element slot {i} ({assetPath})");
        }
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken.png")]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_cold.png")]
    public void Skinned_bake_emits_vertices_in_element_order_non_decreasing_bone(string assetPath)
    {
        BakeSkinned(assetPath, out var verts, out _);
        const int stride = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex;
        var vertCount = verts.Length / stride;
        var prev = DecodeBoneWord(verts[12]);
        for (var vi = 1; vi < vertCount; vi++)
        {
            var b = DecodeBoneWord(verts[vi * stride + 12]);
            Assert.True(b >= prev, $"bone index decreased at vertex {vi} ({assetPath}): {prev} -> {b}");
            prev = b;
        }
    }

    [Fact]
    public void Bone_word_round_trips_through_Int32BitsToSingle()
    {
        foreach (var i in new[] { 0, 1, 7, 63, 255, EntityGpuSkinningLimits.MaxBones - 1 })
        {
            var f = BitConverter.Int32BitsToSingle(i);
            Assert.Equal(i, DecodeBoneWord(f));
        }
    }
}
