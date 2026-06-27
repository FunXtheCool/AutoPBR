using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

/// <summary>Diagnose and lock lift for models that were stuck as index partial placeholders.</summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class PartialModelLiftDiagnosticsTests
{
    private static readonly string[] FormerPlaceholderModels =
    [
        "net.minecraft.client.model.animal.frog.FrogModel",
        "net.minecraft.client.model.monster.warden.WardenModel",
        "net.minecraft.client.model.animal.allay.AllayModel",
        "net.minecraft.client.model.animal.sniffer.SnifferModel",
        "net.minecraft.client.model.monster.vex.VexModel",
        "net.minecraft.client.model.player.PlayerCapeModel",
    ];

    /// <summary>Single-part slice from Frog <c>createBodyLayer</c> (26.1.2 named jar).</summary>
    private const string FrogBodyBindingSlice = """
    Code:
      30: aload_2
      31: ldc           #22                 // String body
      33: invokestatic  #118                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.create:()Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      36: iconst_3
      37: iconst_1
      38: invokevirtual #137                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      41: ldc           #141                // float -3.5f
      43: ldc           #142                // float -2.0f
      45: ldc           #143                // float -8.0f
      47: ldc           #144                // float 7.0f
      49: ldc           #145                // float 3.0f
      51: ldc           #146                // float 9.0f
      53: invokevirtual #147                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      56: bipush        23
      58: bipush        22
      60: invokevirtual #137                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.texOffs:(II)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      63: ldc           #141                // float -3.5f
      65: ldc           #151                // float -1.0f
      67: ldc           #143                // float -8.0f
      69: ldc           #144                // float 7.0f
      71: fconst_0
      72: ldc           #146                // float 9.0f
      74: invokevirtual #147                // Method net/minecraft/client/model/geom/builders/CubeListBuilder.addBox:(FFFFFF)Lnet/minecraft/client/model/geom/builders/CubeListBuilder;
      77: fconst_0
      78: ldc           #142                // float -2.0f
      80: ldc           #152                // float 4.0f
      82: invokestatic  #125                // Method net/minecraft/client/model/geom/PartPose.offset:(FFF)Lnet/minecraft/client/model/geom/PartPose;
      85: invokevirtual #131                // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
      88: astore_3
    """;

    [Fact]
    public void Mesh_definition_prologue_does_not_block_createBodyLayer_lift()
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap() ?? "javap";
        JavapClassDisassembly.TryDisassemble(javap, jar,
            "net.minecraft.client.model.animal.frog.FrogModel", out var stdout, out _);
        var block = JavapClassDisassembly.ExtractMethodCodeBlock(stdout, "createBodyLayer");
        Assert.False(string.IsNullOrEmpty(block));
        Assert.True(JavapFloatGeometryMeshLift.TryLift(block!, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) > 5);
    }

    [Fact]
    public void Frog_body_binding_slice_lifts()
    {
        Assert.True(JavapFloatGeometryMeshLift.TryLift(FrogBodyBindingSlice, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) >= 2);
    }

    [Fact]
    public void Frog_bytecode_concat_lifts_from_jar()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftFromJar(jar,
                "net.minecraft.client.model.animal.frog.FrogModel", "createBodyLayer", null, out var roots,
                out var notes, out _),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) > 0);
    }

    [Fact]
    public void Frog_full_createBodyLayer_block_lifts()
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap() ?? "javap";
        JavapClassDisassembly.TryDisassemble(javap, jar,
            "net.minecraft.client.model.animal.frog.FrogModel", out var stdout, out var err);
        var block = JavapClassDisassembly.ExtractMethodCodeBlock(stdout, "createBodyLayer");
        Assert.False(string.IsNullOrEmpty(block), err);
        var lifted = JavapFloatGeometryMeshLift.TryLift(block!, out var roots, out var notes);
        Assert.True(lifted, $"TryLift={lifted} roots={roots.Count} notes={string.Join(" | ", notes)}");
        Assert.True(CountCuboids(roots) > 5);
    }

    [Fact]
    public void Frog_javap_named_concat_resolves_and_lifts()
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap() ?? "javap";
        var disasm = GeometryLiftPipeline.TryResolveMeshDisassembly(javap, jar, null,
            "net.minecraft.client.model.animal.frog.FrogModel");
        Assert.NotNull(disasm);
        Assert.True(JavapFloatGeometryMeshLift.TryLift(disasm.Value.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) > 0);
    }

    [Fact]
    public void BabyAxolotl_right_hind_leg_does_not_inherit_body_cuboids()
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        Assert.True(GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null,
                "net.minecraft.client.model.animal.axolotl.BabyAxolotlModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        var hind = FindPartById(attempt.Roots, "right_hind_leg");
        Assert.NotNull(hind);
        var cuboids = hind!["cuboids"]!.AsArray();
        Assert.Empty(cuboids);
        var r1 = FindPartById(attempt.Roots, "right_leg_r1");
        Assert.NotNull(r1);
        Assert.Single(r1!["cuboids"]!.AsArray());
    }

    [Fact]
    public void AdultAxolotl_lift_includes_body_with_torso_cuboids()
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        Assert.True(GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null,
                "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        var body = FindPartById(attempt.Roots, "body");
        Assert.NotNull(body);
        var cuboids = body!["cuboids"]!.AsArray();
        Assert.Equal(2, cuboids.Count);
        Assert.Contains(cuboids, c =>
            Math.Abs(c!["to"]![0]!.GetValue<float>() - c!["from"]![0]!.GetValue<float>() - 8f) < 0.01f &&
            Math.Abs(c["to"]![1]!.GetValue<float>() - c["from"]![1]!.GetValue<float>() - 4f) < 0.01f &&
            Math.Abs(c["to"]![2]!.GetValue<float>() - c["from"]![2]!.GetValue<float>() - 10f) < 0.01f);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.fish.SalmonModel", "top_front_fin", "body_front")]
    [InlineData("net.minecraft.client.model.animal.axolotl.AdultAxolotlModel", "top_gills", "head")]
    public void Javap_lift_without_repair_nests_known_child_under_parent(
        string officialJvmName,
        string childId,
        string parentId)
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap() ?? "javap";
        var disasm = GeometryLiftPipeline.TryResolveMeshDisassembly(javap, jar, null, officialJvmName);
        Assert.NotNull(disasm);
        Assert.True(
            JavapFloatGeometryMeshLift.TryLift(disasm.Value.MeshConcat, out var roots, out var notes),
            string.Join("; ", notes));
        var child = FindPartById(roots, childId);
        Assert.NotNull(child);
        var parent = FindAncestorWithId(roots, child!, parentId);
        Assert.NotNull(parent);
    }

    [Fact]
    public void Lifted_tree_passes_semantic_validator_for_adult_axolotl()
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        Assert.True(GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null,
                "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel", "createBodyLayer", preferAsm: true,
                out var attempt),
            string.Join("; ", attempt.Notes));
        var tree = GeometryIrLiftTreeValidator.ValidateRoots(attempt.Roots,
            "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel");
        Assert.True(tree.IsValid, string.Join("; ", tree.Issues.Select(i => $"{i.Code}: {i.Message}")));
    }

    [Theory]
    [InlineData("net.minecraft.client.model.HumanoidModel", "createMesh")]
    [InlineData("net.minecraft.client.model.ambient.BatModel", "createBodyLayer")]
    public void Lifted_tree_probe_reports_no_duplicate_cuboids_across_parts(string officialJvmName, string factoryMethod)
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        Assert.True(GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null, officialJvmName, factoryMethod,
                preferAsm: true, out var attempt),
            string.Join("; ", attempt.Notes));
        var tree = GeometryIrLiftTreeValidator.ValidateRoots(attempt.Roots, officialJvmName);
        Assert.DoesNotContain(tree.Issues, i => i.Code == "duplicate_cuboid_across_parts");
    }

    [Theory]
    [MemberData(nameof(FormerPlaceholderModelNames))]
    public void Bytecode_lift_from_jar_produces_cuboids(string officialJvmName)
    {
        var jar = ResolveClientJar();
        var javap = JavapLocator.FindJavap();
        var ok = GeometryLiftPipeline.TryLiftWithJavapFallback(javap, jar, null, officialJvmName, "createBodyLayer",
            preferAsm: true, out var attempt);

        Assert.True(ok,
            $"{officialJvmName}: profile={attempt.LiftProfile} host={attempt.MeshHostJvmName} notes={string.Join("; ", attempt.Notes)}");

        Assert.True(CountCuboids(attempt.Roots) > 0, officialJvmName);
    }

    public static IEnumerable<object[]> FormerPlaceholderModelNames() =>
        FormerPlaceholderModels.Select(m => new object[] { m });

    private static string? ResolveClientJar() =>
        GeometryIrTestTierSupport.TryClientJarPath(FindRepoRoot());

    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "AutoPBR.sln")))
            {
                return d.FullName;
            }

            d = d.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var r in roots)
        {
            if (r is JsonObject ro)
            {
                n += CountPart(ro);
            }
        }

        return n;
    }

    private static int CountPart(JsonObject part)
    {
        var n = part["cuboids"] is JsonArray c ? c.Count : 0;
        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co)
                {
                    n += CountPart(co);
                }
            }
        }

        return n;
    }

    private static JsonObject? FindPartById(JsonArray roots, string id)
    {
        foreach (var r in roots)
        {
            if (r is JsonObject ro && TryFindPartById(ro, id, out var found))
            {
                return found;
            }
        }

        return null;
    }

    private static bool TryFindPartById(JsonObject part, string id, out JsonObject? found)
    {
        if (string.Equals((string?)part["id"], id, StringComparison.Ordinal))
        {
            found = part;
            return true;
        }

        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is JsonObject co && TryFindPartById(co, id, out found))
                {
                    return true;
                }
            }
        }

        found = null;
        return false;
    }

    private static JsonObject? FindAncestorWithId(JsonArray roots, JsonObject childPart, string ancestorId)
    {
        var childId = (string?)childPart["id"];
        foreach (var r in roots)
        {
            if (r is JsonObject ro && TryFindAncestorInPart(ro, childId, ancestorId, out var ancestor))
            {
                return ancestor;
            }
        }

        return null;
    }

    private static bool TryFindAncestorInPart(JsonObject part, string? targetChildId, string ancestorId,
        out JsonObject? ancestor)
    {
        if (part["children"] is JsonArray kids)
        {
            foreach (var ch in kids)
            {
                if (ch is not JsonObject co)
                {
                    continue;
                }

                if (string.Equals((string?)co["id"], targetChildId, StringComparison.Ordinal))
                {
                    if (string.Equals((string?)part["id"], ancestorId, StringComparison.Ordinal))
                    {
                        ancestor = part;
                        return true;
                    }

                    ancestor = null;
                    return false;
                }

                if (TryFindAncestorInPart(co, targetChildId, ancestorId, out ancestor))
                {
                    return true;
                }
            }
        }

        ancestor = null;
        return false;
    }
}
