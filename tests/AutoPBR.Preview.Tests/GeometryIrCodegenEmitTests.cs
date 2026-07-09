using AutoPBR.Preview.Generated;

namespace AutoPBR.Core.Tests;

public sealed class GeometryIrCodegenEmitTests
{
    private const string CodJvm = "net.minecraft.client.model.animal.fish.CodModel";

    [Fact]
    public void Cod_codegen_table_has_same_cuboid_count_as_packaged_shard()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        Assert.True(GeometryIrDocumentLoader.TryLoadLiftedOkForParity(profile, CodJvm, out var root));
        Assert.Equal(GeometryIrEntityCuboidTables.CodModelBodyLayer.Length, CountShardCuboids(root));
    }

    [Fact]
    public void Cod_codegen_emit_matches_ir_parity_mesh()
    {
        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var p = EntityModelRuntime.BabyProfile.Adult;
        const string texRef = "entity/fish/cod";

        var ir = EntityModelRuntime.TryBuildCodGeometryIrMeshForTests(texRef, profile, p, tailSway: 0f, out _);
        Assert.NotNull(ir);

        var codegen = EntityModelRuntime.TryBuildCodGeometryIrMeshForTests(
            texRef, profile, p, tailSway: 0f, preferCodegen: true, out _);
        Assert.NotNull(codegen);

        GeometryIrMeshParityGoldenTests.AssertMeshesEquivalent(ir, codegen, tol: 1e-3f);
    }

    private static int CountShardCuboids(System.Text.Json.JsonElement root)
    {
        var n = 0;
        if (!root.TryGetProperty("roots", out var roots))
        {
            return 0;
        }

        foreach (var part in roots.EnumerateArray())
        {
            n += CountPartCuboids(part);
        }

        return n;
    }

    private static int CountPartCuboids(System.Text.Json.JsonElement part)
    {
        var n = 0;
        if (part.TryGetProperty("cuboids", out var cuboids))
        {
            n += cuboids.GetArrayLength();
        }

        if (part.TryGetProperty("children", out var children))
        {
            foreach (var ch in children.EnumerateArray())
            {
                n += CountPartCuboids(ch);
            }
        }

        return n;
    }
}
