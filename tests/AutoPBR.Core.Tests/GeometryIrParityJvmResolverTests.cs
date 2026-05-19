
namespace AutoPBR.Core.Tests;

public sealed class GeometryIrParityJvmResolverTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_cold.png", false, "net.minecraft.client.model.animal.cow.ColdCowModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_warm.png", false, "net.minecraft.client.model.animal.cow.WarmCowModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", false, "net.minecraft.client.model.animal.cow.CowModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_cold_baby.png", true, "net.minecraft.client.model.animal.cow.BabyCowModel")]
    [InlineData("assets/minecraft/textures/entity/pig/pig_cold_baby.png", true, "net.minecraft.client.model.animal.pig.BabyPigModel")]
    [InlineData("assets/minecraft/textures/entity/chicken/chicken_cold.png", false, "net.minecraft.client.model.animal.chicken.ColdChickenModel")]
    [InlineData("assets/minecraft/textures/entity/pig/pig_cold.png", false, "net.minecraft.client.model.animal.pig.ColdPigModel")]
    [InlineData("assets/minecraft/textures/entity/cat/cat_red_baby.png", true, "net.minecraft.client.model.animal.feline.BabyFelineModel")]
    [InlineData("assets/minecraft/textures/entity/cat/ocelot_baby.png", true, "net.minecraft.client.model.animal.feline.BabyOcelotModel")]
    [InlineData("assets/minecraft/textures/entity/fox/fox_baby.png", true, "net.minecraft.client.model.animal.fox.BabyFoxModel")]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf_baby.png", true, "net.minecraft.client.model.animal.wolf.BabyWolfModel")]
    [InlineData("assets/minecraft/textures/entity/rabbit/rabbit_white_baby.png", true, "net.minecraft.client.model.animal.rabbit.BabyRabbitModel")]
    [InlineData("assets/minecraft/textures/entity/armadillo/armadillo_baby.png", true, "net.minecraft.client.model.animal.armadillo.BabyArmadilloModel")]
    [InlineData("assets/minecraft/textures/entity/camel/camel_baby.png", true, "net.minecraft.client.model.animal.camel.BabyCamelModel")]
    [InlineData("assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png", true, "net.minecraft.client.model.animal.axolotl.BabyAxolotlModel")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear_baby.png", true, "net.minecraft.client.model.animal.polarbear.BabyPolarBearModel")]
    public void EnumerateCandidates_prefers_climate_and_baby_mesh_hosts(
        string texturePath,
        bool isBaby,
        string expectedFirstOk)
    {
        var norm = texturePath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(norm, stem);
        Assert.NotNull(rule);

        string? firstOk = null;
        foreach (var candidate in GeometryIrParityJvmResolver.EnumerateCandidates(rule, norm, stem, isBaby))
        {
            if (GeometryIrDocumentLoader.TryLoadLiftedOkForParity(Profile26, candidate, out _))
            {
                firstOk = candidate;
                break;
            }
        }

        Assert.Equal(expectedFirstOk, firstOk);
    }

    [Fact]
    public void Equipment_humanoid_leggings_builds_geometry_ir_parity_mesh()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/equipment/humanoid_leggings/diamond.png";
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance));
        Assert.True(mesh.Elements.Count >= 3);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains("EquipmentHumanoidLeggingsModel", provenance.Detail ?? "", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/axolotl/axolotl_blue.png", false)]
    [InlineData("assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png", true)]
    [InlineData("assets/minecraft/textures/entity/fish/cod.png", false)]
    [InlineData("assets/minecraft/textures/entity/fish/salmon.png", false)]
    public void Catalogued_aquatic_pilots_use_runtime_geometry_ir_not_hand_builder(
        string texturePath,
        bool isBaby)
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0.25f, 1.2f, out var mesh, out var provenance));
        Assert.True(mesh.Elements.Count > 0);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains("Model", provenance.Detail ?? "", StringComparison.Ordinal);
        _ = isBaby;
    }

    [Fact]
    public void Baby_cow_mesh_extent_is_smaller_than_adult_when_emit_applies_vanilla_baby_scale()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/cow/cow_temperate.png",
            Profile26,
            0f,
            0f,
            out var adult,
            out _));
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/cow/cow_temperate_baby.png",
            Profile26,
            0f,
            0f,
            out var baby,
            out var babyProv));
        Assert.Contains("BabyCowModel", babyProv.Detail ?? "", StringComparison.Ordinal);
        var adultSpan = MaxElementSpan(adult);
        var babySpan = MaxElementSpan(baby);
        Assert.True(babySpan < adultSpan * 0.85f, $"baby span {babySpan} should be materially smaller than adult {adultSpan}");
    }

    [Fact]
    public void Baby_dedicated_ir_hosts_do_not_resolve_adult_mesh_when_baby_shard_missing()
    {
        var norm = "assets/minecraft/textures/entity/bear/polarbear_baby.png";
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(norm, stem);
        Assert.NotNull(rule);

        var candidates = GeometryIrParityJvmResolver.EnumerateCandidates(rule!, norm, stem, isBaby: true).ToList();
        var polarIdx = candidates.IndexOf("net.minecraft.client.model.animal.polarbear.PolarBearModel");
        var babyIdx = candidates.IndexOf("net.minecraft.client.model.animal.polarbear.BabyPolarBearModel");
        Assert.True(babyIdx >= 0);
        Assert.True(polarIdx < 0 || babyIdx < polarIdx, "BabyPolarBearModel must be ordered before adult PolarBearModel");
    }

    [Fact]
    public void Manifest_baby_jvm_fields_resolve_for_catalogued_quadruped_pilots()
    {
        foreach (var path in new[]
                 {
                     "assets/minecraft/textures/entity/bear/polarbear_baby.png",
                     "assets/minecraft/textures/entity/cat/cat_red_baby.png",
                     "assets/minecraft/textures/entity/fox/fox_baby.png",
                     "assets/minecraft/textures/entity/pig/pig_cold_baby.png",
                 })
        {
            var norm = path.Replace('\\', '/').TrimStart('/');
            var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
            var rule = EntityTextureParityCatalog.ResolveRule(norm, stem);
            Assert.NotNull(rule);
            Assert.False(string.IsNullOrWhiteSpace(rule.GeometryIrOfficialJvmBaby));
            Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                Profile26, rule, norm, stem, isBaby: true, out var jvm, out _));
            Assert.Equal(rule.GeometryIrOfficialJvmBaby, jvm);
        }
    }

    private static float MaxElementSpan(MergedJavaBlockModel model)
    {
        var max = 0f;
        foreach (var el in model.Elements)
        {
            var ex = MathF.Abs(el.To[0] - el.From[0]);
            var ey = MathF.Abs(el.To[1] - el.From[1]);
            var ez = MathF.Abs(el.To[2] - el.From[2]);
            max = MathF.Max(max, MathF.Max(ex, MathF.Max(ey, ez)));
        }

        return max;
    }

    [Fact]
    public void Cow_cold_texture_builds_geometry_ir_parity_mesh()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/cow/cow_cold.png";
        Assert.True(runtime.TryBuildStaticMesh(
            path,
            Profile26,
            idlePhase01: 0.2f,
            animationTimeSeconds: 1.5f,
            out var mesh,
            out var provenance));
        Assert.True(mesh.Elements.Count >= 4);
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains("ColdCowModel", provenance.Detail ?? "", StringComparison.Ordinal);
    }
}
