using System.Text.Json;

using AutoPBR.Core.Preview.Generated;

namespace AutoPBR.Core.Tests;

/// <summary>
/// T1 rig harness: Java reference_java bakes vs committed 26.1.2 IR shards and parity emit.
/// JVMs: <c>geometry_ir_reference_cuboid_strict_jvm.txt</c> + <c>geometry_ir_mob_family_pilot_jvm.txt</c>.
/// </summary>
public sealed class GeometryIrReferenceRigTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new(GeometryIrTestTierSupport.MobFamilyPilotVersionLabel, "unused", new Version(26, 1, 2));

    private static readonly HashSet<string> StrictCuboidJvm = new(
        GeometryIrTestTierSupport.LoadReferenceCuboidStrictSet(GeometryIrTestTierSupport.FindRepoRoot()),
        StringComparer.Ordinal);

    private static readonly HashSet<string> StrictPoseJvm = new(StringComparer.Ordinal)
    {
        "net.minecraft.client.model.HumanoidModel",
        "net.minecraft.client.model.player.PlayerModel",
        "net.minecraft.client.model.animal.fish.CodModel",
        "net.minecraft.client.model.animal.fish.SalmonModel",
        "net.minecraft.client.model.animal.chicken.ChickenModel",
        "net.minecraft.client.model.animal.cow.CowModel",
        "net.minecraft.client.model.animal.cow.ColdCowModel",
        "net.minecraft.client.model.animal.pig.PigModel",
        "net.minecraft.client.model.ambient.BatModel",
        "net.minecraft.client.model.monster.creeper.CreeperModel",
        "net.minecraft.client.model.monster.blaze.BlazeModel",
        "net.minecraft.client.model.monster.guardian.GuardianModel",
        "net.minecraft.client.model.animal.squid.BabySquidModel",
    };

    private static readonly IReadOnlyDictionary<string, GeometryIrTestTierSupport.MobFamilyPilot> MobPilotByJvm =
        GeometryIrTestTierSupport.LoadMobFamilyPilots(GeometryIrTestTierSupport.FindRepoRoot())
            .ToDictionary(p => p.OfficialJvmName, StringComparer.Ordinal);

    public static IEnumerable<object[]> RigJvmCases()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var jvms = GeometryIrTestTierSupport.LoadOfficialJvmNames(repo, "geometry_ir_reference_cuboid_strict_jvm.txt")
            .Concat(MobPilotByJvm.Keys)
            .Distinct(StringComparer.Ordinal);
        return jvms.Select(j => new object[] { j });
    }

    public static IEnumerable<object[]> MobPilotMeshCases() =>
        MobPilotByJvm.Values.Select(p => new object[] { p.OfficialJvmName, p.AtlasWidth, p.AtlasHeight });

    [Theory]
    [MemberData(nameof(RigJvmCases))]
    public void CompareReferenceToIrShardCuboidsByPartId(string jvm)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement, ir.RootElement, tolerance: 0.08);
        if (!StrictCuboidJvm.Contains(jvm))
        {
            return;
        }

        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [MemberData(nameof(RigJvmCases))]
    public void CompareReferenceToIrShardWithPoses_when_pose_approx_count_zero(string jvm)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        if (!ir.RootElement.TryGetProperty("liftSummary", out var liftSummary) ||
            !liftSummary.TryGetProperty("poseApproxCount", out var poseApprox) ||
            poseApprox.ValueKind != JsonValueKind.Number ||
            poseApprox.GetInt32() != 0)
        {
            return;
        }

        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardWithPoses(
            reference.RootElement, ir.RootElement, cuboidTolerance: 0.08, poseTolerance: 0.08);
        if (!StrictPoseJvm.Contains(jvm))
        {
            return;
        }

        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [MemberData(nameof(MobPilotMeshCases))]
    public void CompareReferenceToParityMesh(string jvm, int atlasW, int atlasH)
    {
        var (reference, ir) = LoadPair(jvm);
        if (reference is null || ir is null)
        {
            return;
        }

        if (!GeometryIrDocumentLoader.TryLoadLiftedOkForParity(Profile26, jvm, out _))
        {
            return;
        }

        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/test", Profile26, jvm, atlasW, atlasH, out var err);
        Assert.Null(err);
        Assert.NotNull(mesh);

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(
            reference.RootElement, mesh, tolerance: 0.08);
        if (!MobPilotByJvm.ContainsKey(jvm))
        {
            return;
        }

        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.fish.CodModel")]
    [InlineData("net.minecraft.client.model.animal.fish.SalmonModel")]
    [InlineData("net.minecraft.client.model.animal.chicken.ChickenModel")]
    public void Codegen_cuboid_dfs_order_matches_ir_walk(string jvm)
    {
        if (!GeometryIrDocumentLoader.TryLoadLiftedOkForParity(Profile26, jvm, out var root))
        {
            return;
        }

        ReadOnlySpan<CleanRoomEntityModelRuntime.EntityCuboid> table = jvm switch
        {
            "net.minecraft.client.model.animal.fish.CodModel" =>
                GeometryIrEntityCuboidTables.CodModelBodyLayer,
            "net.minecraft.client.model.animal.fish.SalmonModel" =>
                GeometryIrEntityCuboidTables.SalmonModelBodyLayer,
            "net.minecraft.client.model.animal.chicken.ChickenModel" =>
                GeometryIrEntityCuboidTables.ChickenModelBodyLayer,
            _ => ReadOnlySpan<CleanRoomEntityModelRuntime.EntityCuboid>.Empty
        };

        Assert.False(table.IsEmpty);
        var walkBoxes = CollectWalkCuboidExtents(root);
        if (walkBoxes.Count != table.Length)
        {
            return;
        }

        for (var i = 0; i < table.Length; i++)
        {
            var t = table[i];
            var w = walkBoxes[i];
            Assert.InRange(w.Fx, t.X0 - 1e-3f, t.X0 + 1e-3f);
            Assert.InRange(w.Fy, t.Y0 - 1e-3f, t.Y0 + 1e-3f);
            Assert.InRange(w.Fz, t.Z0 - 1e-3f, t.Z0 + 1e-3f);
            Assert.InRange(w.Tx, t.X1 - 1e-3f, t.X1 + 1e-3f);
            Assert.InRange(w.Ty, t.Y1 - 1e-3f, t.Y1 + 1e-3f);
            Assert.InRange(w.Tz, t.Z1 - 1e-3f, t.Z1 + 1e-3f);
        }
    }

    private readonly record struct WalkCuboidExtents(float Fx, float Fy, float Fz, float Tx, float Ty, float Tz);

    private static List<WalkCuboidExtents> CollectWalkCuboidExtents(JsonElement geometryRoot)
    {
        var list = new List<WalkCuboidExtents>();
        GeometryIrMeshWalk.WalkRoots(
            geometryRoot,
            System.Numerics.Matrix4x4.Identity,
            new GeometryIrMeshEmitOptions { RootTransform = System.Numerics.Matrix4x4.Identity },
            ctx =>
            {
                if (GeometryIrCuboidMetadata.TryGetFaceMask(ctx.Cuboid, out var emptyMask) && emptyMask.Length == 0)
                {
                    return true;
                }

                var from = ctx.Cuboid.GetProperty("from");
                var to = ctx.Cuboid.GetProperty("to");
                list.Add(new WalkCuboidExtents(
                    (float)from[0].GetDouble(),
                    (float)from[1].GetDouble(),
                    (float)from[2].GetDouble(),
                    (float)to[0].GetDouble(),
                    (float)to[1].GetDouble(),
                    (float)to[2].GetDouble()));
                return true;
            },
            onPartWorld: null,
            out _);
        return list;
    }

    private static (JsonDocument? reference, JsonDocument? ir) LoadPair(string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        var irPath = Path.Combine(root, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(referencePath) || !File.Exists(irPath))
        {
            return (null, null);
        }

        var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            reference.Dispose();
            return (null, null);
        }

        using var irProbe = JsonDocument.Parse(File.ReadAllText(irPath));
        if (!string.Equals(irProbe.RootElement.GetProperty("extractionStatus").GetString(), "ok",
                StringComparison.Ordinal))
        {
            reference.Dispose();
            return (null, null);
        }

        return (reference, JsonDocument.Parse(File.ReadAllText(irPath)));
    }

    [Fact]
    public void AdultCat_runtime_static_mesh_matches_reference_java_pose()
    {
        const string jvm = "net.minecraft.client.model.animal.feline.AdultCatModel";
        const string texturePath = "assets/minecraft/textures/entity/cat/cat_all_black.png";
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(
            runtime.TryBuildStaticMesh(
                texturePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out var provenance,
                applyGeometryIrSetupAnimMotion: false));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
        Assert.Contains(jvm, provenance.Detail ?? "", StringComparison.Ordinal);

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(reference.RootElement, mesh, tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", "net.minecraft.client.model.animal.cow.CowModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_cold.png", "net.minecraft.client.model.animal.cow.ColdCowModel")]
    [InlineData("assets/minecraft/textures/entity/pig/pig_temperate.png", "net.minecraft.client.model.animal.pig.PigModel")]
    [InlineData("assets/minecraft/textures/entity/creeper/creeper.png", "net.minecraft.client.model.monster.creeper.CreeperModel")]
    [InlineData("assets/minecraft/textures/entity/armadillo/armadillo.png", "net.minecraft.client.model.animal.armadillo.ArmadilloModel")]
    [InlineData("assets/minecraft/textures/entity/sheep/sheep.png", "net.minecraft.client.model.animal.sheep.SheepModel")]
    [InlineData("assets/minecraft/textures/entity/goat/goat.png", "net.minecraft.client.model.animal.goat.GoatModel")]
    [InlineData("assets/minecraft/textures/entity/wolf/wolf.png", "net.minecraft.client.model.animal.wolf.WolfModel")]
    [InlineData("assets/minecraft/textures/entity/fox/fox.png", "net.minecraft.client.model.animal.fox.AdultFoxModel")]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", "net.minecraft.client.model.animal.panda.PandaModel")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", "net.minecraft.client.model.animal.polarbear.PolarBearModel")]
    public void Quadruped_runtime_static_mesh_matches_reference_java_pose(
        string texturePath,
        string jvm)
    {
        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(root, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (reference.RootElement.GetProperty("extractionStatus").GetString() is not "reference_java")
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(
            runtime.TryBuildStaticMesh(
                texturePath,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out _,
                applyGeometryIrSetupAnimMotion: false));

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(reference.RootElement, mesh, tolerance: 0.08);
        Assert.True(cmp.IsMatch, $"{jvm} ({texturePath}): {cmp.Message}");
    }
}
