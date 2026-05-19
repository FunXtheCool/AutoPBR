using System.Text.Json.Nodes;
using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>Regression locks for remaining partial JVMs (baby piglin, cape, zombie nautilus coral).</summary>
public sealed class PartialJvmMeshLiftRegressionTests
{
    private static string? ClientJar =>
        File.Exists(Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar"))
            ? Path.Combine(Program.FindRepoRoot(), "tools", "minecraft-parity", "26.1.2", "client.jar")
            : null;

    private const string PlayerCapeCapeBindingSlice = """
    Code:
      16: aload_1
      17: ldc           #46                 // String body
      19: invokevirtual #47                 // Method net/minecraft/client/model/geom/builders/PartDefinition.getChild:(Ljava/lang/String;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
      22: astore_2
      23: aload_2
      24: ldc           #13                 // String cape
      26: invokestatic  #50                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      29: iconst_0
      30: iconst_0
      31: invokevirtual #56                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      34: ldc           #60                 // float -5.0f
      36: fconst_0
      37: ldc           #61                 // float -1.0f
      39: ldc           #62                 // float 10.0f
      41: ldc           #63                 // float 16.0f
      43: fconst_1
      44: getstatic     #23                 // Field net/minecraft/client/model/geom/builders/CubeDeformation.NONE:Lnet/minecraft/client/model/geom/builders/CubeDeformation;
      47: fconst_1
      48: ldc           #64                 // float 0.5f
      50: invokevirtual #65                 // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFFLnet/minecraft/client/model/geom/builders/CubeDeformation;FF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      53: fconst_0
      54: fconst_0
      55: fconst_2
      56: fconst_0
      57: ldc           #71                 // float 3.1415927f
      59: fconst_0
      60: invokestatic  #72                 // Method net/minecraft/client/model/geom/PartPose.offsetAndRotation:(FFFFFF)Lnet/minecraft/client/model/geom/PartPose;
      63: invokevirtual #78                 // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
    """;

    [Fact]
    public void PlayerCape_createCapeLayer_concat_lifts_with_cape_cuboid()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.player.PlayerCapeModel";
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createCapeLayer", out var resolved));
        Assert.Contains("addOrReplaceChild", resolved.MeshConcat, StringComparison.Ordinal);
        ClientJarIO.TryResolveJarEntry(jar, jvm, null, out _, out var capeBytes);
        var capeOnly = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(capeBytes, ["createCapeLayer"], out var capeOnlyOk);
        Assert.True(capeOnlyOk);
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(capeOnly, null, out var capeOnlyRoots, out var capeOnlyNotes),
            string.Join("; ", capeOnlyNotes));
        Assert.NotNull(FindPartById(capeOnlyRoots, "cape"));
        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, jvm, "createCapeLayer",
                out var roots, out var notes),
            string.Join("; ", notes));
        var cape = FindPartById(roots, "cape");
        Assert.NotNull(cape);
        Assert.NotEmpty(cape!["cuboids"]!.AsArray());
        var cub = cape!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal(-5d, cub["from"]![0]!.GetValue<double>(), 3);
    }

    [Fact]
    public void PlayerCape_cape_binding_slice_lifts_reference_cuboid()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(PlayerCapeCapeBindingSlice, out var roots, out var notes),
            string.Join("; ", notes));
        var cape = FindPartById(roots, "cape");
        Assert.NotNull(cape);
        var cub = cape!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.Equal(-5d, cub["from"]![0]!.GetValue<double>(), 3);
        Assert.Equal(0d, cub["from"]![1]!.GetValue<double>(), 3);
        Assert.Equal(-1d, cub["from"]![2]!.GetValue<double>(), 3);
        Assert.Equal(5d, cub["to"]![0]!.GetValue<double>(), 3);
        Assert.Equal(16d, cub["to"]![1]!.GetValue<double>(), 3);
        Assert.Equal(0d, cub["to"]![2]!.GetValue<double>(), 3);
    }

    [Fact]
    public void ZombieNautilusCoral_deep_concat_includes_createBodyMesh_and_shell()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.nautilus.ZombieNautilusCoralModel";
        Assert.True(BytecodeMeshResolution.TryResolve(jar, null, jvm, "createBodyLayer", out var resolved));
        Assert.Contains("createBodyMesh", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.Contains("shell", resolved.MeshConcat, StringComparison.Ordinal);
        Assert.True(ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.animal.nautilus.NautilusModel", null, out _, out var nautilusBytes));
        var bodyMeshOnly = BytecodeGeometryMeshLift.BuildSyntheticMeshConcat(nautilusBytes, ["createBodyMesh"], out var meshOk);
        Assert.True(meshOk, "NautilusModel.createBodyMesh disassembly");
        Assert.True(BytecodeGeometryMeshLift.TryLiftConcat(bodyMeshOnly, null, out var meshRoots, out var meshNotes),
            string.Join("; ", meshNotes));
        Assert.NotNull(FindPartById(meshRoots, "shell"));
        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));
        Assert.NotNull(FindPartById(roots, "shell"));
        Assert.NotNull(FindPartById(roots, "body"));
        Assert.NotNull(FindPartById(roots, "corals"));
    }

    [Fact]
    public void BabyPiglin_lift_has_empty_hat_cuboids()
    {
        var jar = ClientJar;
        if (jar is null)
        {
            return;
        }

        const string jvm = "net.minecraft.client.model.monster.piglin.BabyPiglinModel";
        Assert.True(GeometryLiftPipeline.TryLiftRoots(JavapLocator.FindJavap(), jar, null, jvm, "createBodyLayer",
                out var roots, out var notes),
            string.Join("; ", notes));
        var head = FindPartById(roots, "head");
        Assert.NotNull(head);
        var hat = FindPartById(roots, "hat");
        Assert.NotNull(hat);
        Assert.Empty(hat!["cuboids"]!.AsArray());
    }

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var node in roots)
        {
            if (node is not JsonObject root)
            {
                continue;
            }

            var found = Walk(root, id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static JsonObject? Walk(JsonObject part, string id)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            return part;
        }

        if (part["children"] is not JsonArray kids)
        {
            return null;
        }

        foreach (var ch in kids)
        {
            if (ch is JsonObject co && Walk(co, id) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
