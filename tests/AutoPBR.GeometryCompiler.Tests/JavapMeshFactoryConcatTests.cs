using System.Text.Json.Nodes;

using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class JavapMeshFactoryConcatTests
{
    private static readonly float[] TestProbeOneFloat = { 0.5f };
    [Fact]
    public void ConcatMeshFactoryCodeNamed_appends_static_LayerDefinition_factory_code()
    {
        const string javapC = """
              public static net.minecraft.client.model.geom.builders.LayerDefinition createBodyLayer();
                Code:
                   0: invokevirtual  // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;

              public static net.minecraft.client.model.geom.builders.MeshDefinition createMesh();
                Code:
                   0: invokevirtual  // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;

              public static net.minecraft.client.model.geom.builders.LayerDefinition createCapeLayer();
                Code:
                   1: invokevirtual  // Method net/minecraft/client/model/geom/builders/PartDefinition.addOrReplaceChild:(Ljava/lang/String;Lnet/minecraft/client/model/geom/builders/CubeListBuilder;Lnet/minecraft/client/model/geom/PartPose;)Lnet/minecraft/client/model/geom/builders/PartDefinition;
            """;

        var acc = JavapClassDisassembly.ConcatMeshFactoryCodeNamed(javapC);
        var islands = acc.Split(JavapClassDisassembly.GeometryMeshIslandBoundaryMarker,
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, islands.Length);
        Assert.True(JavapMeshBytecodeProfiles.ContainsMeshSignals(acc));
        Assert.All(islands, island => Assert.Contains("addOrReplaceChild", island, StringComparison.Ordinal));
    }

    [Fact]
    public void EnumerateInvokeStaticMeshRefs_parses_slash_qualified_owner_and_return_type()
    {
        const string line =
            "      4: invokestatic  #29                 // Method net/minecraft/client/model/player/PlayerModel.createMesh:(Lnet/minecraft/client/model/geom/builders/CubeDeformation;Z)Lnet/minecraft/client/model/geom/builders/MeshDefinition;";
        var refs = JavapMeshBytecodeProfiles.EnumerateInvokeStaticMeshRefs(line + "\n").ToList();
        Assert.Single(refs);
        Assert.Equal("createMesh", refs[0].Method);
        Assert.Equal("net.minecraft.client.model.player.PlayerModel", refs[0].OwnerJarSimple);
    }

    [Fact]
    public void EnumerateInvokeStaticMeshRefs_parses_null_owner_PartDefinition_addHead()
    {
        const string line =
            "     51: invokestatic  #60                 // Method addHead:(Lnet/minecraft/client/model/geom/builders/CubeDeformation;Lnet/minecraft/client/model/geom/builders/MeshDefinition;)Lnet/minecraft/client/model/geom/builders/PartDefinition;";
        var refs = JavapMeshBytecodeProfiles.EnumerateInvokeStaticMeshRefs(line + "\n").ToList();
        Assert.Single(refs);
        Assert.Equal("addHead", refs[0].Method);
        Assert.Null(refs[0].OwnerJarSimple);
    }

    [Fact]
    public void ExtractMethodCodeBlockBySignatureNeedle_falls_back_to_java_source_types_when_needle_uses_jvm_descriptors()
    {
        const string javap = """
            abstract class net.minecraft.client.model.monster.piglin.AbstractPiglinModel {
              public static net.minecraft.client.model.geom.builders.PartDefinition addHead(net.minecraft.client.model.geom.builders.CubeDeformation, net.minecraft.client.model.geom.builders.MeshDefinition);
                Code:
                   0: return
            }
            """;

        const string descriptorNeedle =
            " addHead(Lnet/minecraft/client/model/geom/builders/CubeDeformation;Lnet/minecraft/client/model/geom/builders/MeshDefinition;);";
        var code = JavapClassDisassembly.ExtractMethodCodeBlockBySignatureNeedle(javap, descriptorNeedle);
        Assert.NotNull(code);
        Assert.Contains("return", code, StringComparison.Ordinal);
    }

    [Fact]
    public void ContainsMeshSignals_true_when_LayerDefinition_keyword_present() =>
        Assert.True(JavapMeshBytecodeProfiles.ContainsMeshSignals("LayerDefinition.create"));

    [Fact]
    public void ContainsMeshSignals_true_when_obfuscated_fluent_addBox_uses_short_method_name()
    {
        const string javapC = """
              45: invokevirtual #97                 // Method hdl.a:(FFFFFFLhdk;)Lhdl;
            """;
        Assert.False(javapC.Contains("addBox", StringComparison.Ordinal));
        Assert.True(JavapMeshBytecodeProfiles.ContainsMeshSignals(javapC));
    }

    [Fact]
    public void ContainsMeshSignals_true_when_string_addBox_descriptor_splits_before_six_floats()
    {
        const string javapC = """
              40: invokevirtual #84                 // Method a.b.c.d.addBox:(Ljava/lang/String;
            FFFFFF)La/b/c/d;
            """;
        Assert.False(javapC.Contains("(FFFFFF)", StringComparison.Ordinal));
        Assert.True(JavapMeshBytecodeProfiles.ContainsMeshSignals(javapC));
    }

    [Fact]
    public void MeshHostClassCandidates_maps_Abstract_stem_to_concrete_Model_in_same_package()
    {
        var list = MeshHostClassCandidates.Enumerate(
                "net.minecraft.client.model.monster.zombie.AbstractZombieModel")
            .ToList();
        Assert.Contains("net.minecraft.client.model.monster.zombie.ZombieModel", list);
    }

    [Fact]
    public void MeshHostClassCandidates_maps_Like_stem_to_stripped_Model_in_same_package()
    {
        var list = MeshHostClassCandidates.Enumerate("net.minecraft.client.model.VillagerLikeModel").ToList();
        Assert.Contains("net.minecraft.client.model.VillagerModel", list);
    }

    [Fact]
    public void ApplyProbe_javapOk_strips_stale_probe_and_ldc_notes_when_part_tree_has_cuboids()
    {
        var root = new JsonObject
        {
            ["extractionNotes"] = new JsonArray(
                "javap float probe failed or missing createBodyLayer Code block.",
                "ldc float constants found in createBodyLayer bytecode; part-tree IR is still incomplete or placeholder-only."),
            ["roots"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "x",
                    ["cuboids"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["from"] = new JsonArray(0, 0, 0),
                            ["to"] = new JsonArray(1, 1, 1)
                        }
                    },
                    ["children"] = new JsonArray()
                }
            }
        };

        GeometryBytecodeMerge.ApplyProbe(root, "aa", TestProbeOneFloat, javapOk: true);
        var notes = root["extractionNotes"]!.AsArray().Select(n => n!.ToString()).ToList();
        Assert.DoesNotContain(notes, n => n.Contains("javap float probe failed", StringComparison.Ordinal));
        Assert.DoesNotContain(notes, n => n.Contains("ldc float constants found", StringComparison.Ordinal));
    }

    [Fact]
    public void EnumerateMeshFactoryPins_includes_LayerDefinition_returns()
    {
        var dir = Path.Combine(Path.GetTempPath(), "autopbr_geom_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "client_mappings.txt");
        File.WriteAllText(path, """
            net.minecraft.client.model.player.PlayerCapeModel -> aa.bb.cc.PlayerCapeModel:
            # {"fileName":"PlayerCapeModel.java","id":"sourceFile"}
                26:36:net.minecraft.client.model.geom.builders.LayerDefinition createCapeLayer() -> e
            """);

        try
        {
            var maps = MojangMappingsParser.Load(path);
            var pins = maps.EnumerateMeshFactoryPins("net.minecraft.client.model.player.PlayerCapeModel").ToList();
            Assert.Contains(pins, p => string.Equals(p.NamedMethod, "createCapeLayer", StringComparison.Ordinal));
        }
        finally
        {
            try
            {
                File.Delete(path);
                Directory.Delete(dir);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
