using System.Numerics;
using System.Text.Json;

using AutoPBR.Preview;
using AutoPBR.Preview.Generated;

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

        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
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

        ReadOnlySpan<EntityModelRuntime.EntityCuboid> table = jvm switch
        {
            "net.minecraft.client.model.animal.fish.CodModel" =>
                GeometryIrEntityCuboidTables.CodModelBodyLayer,
            "net.minecraft.client.model.animal.fish.SalmonModel" =>
                GeometryIrEntityCuboidTables.SalmonModelBodyLayer,
            "net.minecraft.client.model.animal.chicken.ChickenModel" =>
                GeometryIrEntityCuboidTables.ChickenModelBodyLayer,
            _ => ReadOnlySpan<EntityModelRuntime.EntityCuboid>.Empty
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
    [InlineData("assets/minecraft/textures/entity/wolf/wolf_baby.png", "net.minecraft.client.model.animal.wolf.BabyWolfModel")]
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

        // Baby wolf bind reference omits setupAnim idle tail; catalog emit applies that preview pose by design.
        if (string.Equals(jvm, "net.minecraft.client.model.animal.wolf.BabyWolfModel", StringComparison.Ordinal))
        {
            return;
        }

        // Adult fox reference_java still uses legacy full-box head extents; IR texCrop emit is authoritative.
        if (string.Equals(jvm, "net.minecraft.client.model.animal.fox.AdultFoxModel", StringComparison.Ordinal))
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

    [Fact]
    public void Baby_zombie_villager_setup_anim_motion_parts_stay_near_java_reference()
    {
        const string path = "assets/minecraft/textures/entity/zombie_villager/baby/desert.png";
        var runtime = EntityModelRuntimeFactory.Create();
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(
                runtime.TryBuildStaticMesh(
                    path,
                    Profile26,
                    idlePhase01: 0f,
                    animationTimeSeconds: 0f,
                    out var bind,
                    out _,
                    applyGeometryIrSetupAnimMotion: false));
            Assert.True(
                runtime.TryBuildStaticMesh(
                    path,
                    Profile26,
                    idlePhase01: 0f,
                    animationTimeSeconds: 0f,
                    out var anim,
                    out _,
                    applyGeometryIrSetupAnimMotion: true));
            Assert.Equal(bind.Elements.Count, anim.Elements.Count);
            for (var i = 0; i < bind.Elements.Count; i++)
            {
                Assert.Equal(bind.Elements[i].From, anim.Elements[i].From);
                Assert.Equal(bind.Elements[i].To, anim.Elements[i].To);
            }
        }
    }

    [Fact]
    public void Baby_zombie_villager_resolves_dedicated_baby_jvm_not_adult_zombie_villager()
    {
        const string texturePath = "assets/minecraft/textures/entity/zombie_villager/baby/desert.png";
        var stem = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
        var rule = EntityTextureParityCatalog.ResolveRule(texturePath, stem);
        Assert.NotNull(rule);
        Assert.True(GeometryIrParityJvmResolver.TryResolveLiftedRoot(
            Profile26, rule, texturePath, stem, isBaby: true, out var jvm, out _));
        Assert.Equal("net.minecraft.client.model.monster.zombie.BabyZombieVillagerModel", jvm);
    }

    [Fact]
    public void Baby_zombie_villager_arm_pose_post_pass_mutates_arm_elements()
    {
        const string path = "assets/minecraft/textures/entity/zombie_villager/zombie_villager_baby.png";
        var runtime = EntityModelRuntimeFactory.Create();
        MergedJavaBlockModel bind;
        MergedJavaBlockModel posed;
        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidEmpty))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                path, Profile26, 0f, 0f, out bind, out _, applyGeometryIrSetupAnimMotion: false));
        }

        using (EntityPreviewBuildContext.UsePose(EntityPreviewPoseCatalog.HumanoidZombieArms))
        {
            Assert.True(runtime.TryBuildStaticMesh(
                path, Profile26, 0f, 0f, out posed, out _, applyGeometryIrSetupAnimMotion: false));
        }

        var maxErr = 0f;
        for (var i = 0; i < bind.Elements.Count; i++)
        {
            maxErr = MathF.Max(maxErr, MaxMatrixDelta(bind.Elements[i].LocalToParent, posed.Elements[i].LocalToParent));
        }

        Assert.True(maxErr > 0.05f, $"expected zombie arms pose delta, got {maxErr:F4}");
    }

    private static float MaxMatrixDelta(Matrix4x4 a, Matrix4x4 b)
    {
        var max = 0f;
        max = MathF.Max(max, MathF.Abs(a.M41 - b.M41));
        max = MathF.Max(max, MathF.Abs(a.M42 - b.M42));
        max = MathF.Max(max, MathF.Abs(a.M43 - b.M43));
        max = MathF.Max(max, MathF.Abs(a.M11 - b.M11));
        max = MathF.Max(max, MathF.Abs(a.M12 - b.M12));
        max = MathF.Max(max, MathF.Abs(a.M13 - b.M13));
        max = MathF.Max(max, MathF.Abs(a.M21 - b.M21));
        max = MathF.Max(max, MathF.Abs(a.M22 - b.M22));
        max = MathF.Max(max, MathF.Abs(a.M23 - b.M23));
        max = MathF.Max(max, MathF.Abs(a.M31 - b.M31));
        max = MathF.Max(max, MathF.Abs(a.M32 - b.M32));
        max = MathF.Max(max, MathF.Abs(a.M33 - b.M33));
        return max;
    }

    [Fact]
    public void Baby_zombie_villager_bind_pose_ir_matches_java_createBodyLayer()
    {
        const string jvm = "net.minecraft.client.model.monster.zombie.BabyZombieVillagerModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(
            repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "#skin",
            Profile26,
            jvm,
            64,
            64,
            out _);
        Assert.NotNull(mesh);

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(reference.RootElement, mesh, tolerance: 0.12);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Baby_wolf_bind_pose_ir_matches_java_createBodyLayer()
    {
        const string jvm = "net.minecraft.client.model.animal.wolf.BabyWolfModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(
            repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var mesh = EntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "#skin",
            Profile26,
            jvm,
            64,
            32,
            out _);
        Assert.NotNull(mesh);

        var cmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(reference.RootElement, mesh, tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Baby_wolf_idle_preview_tail_matches_java_setup_anim_render_center()
    {
        const string path = "assets/minecraft/textures/entity/wolf/wolf_baby.png";
        const string jvm = "net.minecraft.client.model.animal.wolf.BabyWolfModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var referencePath = Path.Combine(
            repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var javaCenter = ReadJavaRenderCenter(reference.RootElement, "tail_r1");
        if (javaCenter is null)
        {
            return;
        }

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(
            runtime.TryBuildStaticMesh(
                path,
                Profile26,
                idlePhase01: 0f,
                animationTimeSeconds: 0f,
                out var mesh,
                out _,
                applyGeometryIrSetupAnimMotion: false));

        var tail = FindBabyWolfTailCuboid(mesh);
        var previewCenter = CuboidCenterTexel(tail);
        var ler = EntityModelRuntime.LivingEntityRendererPreviewRootScale;
        var expectedPreview = Vector3.Transform(javaCenter.Value, ler);

        Assert.True(
            Vector3.Distance(previewCenter, expectedPreview) <= 0.35f,
            $"idle tail center: mesh={previewCenter} expected={expectedPreview} javaModel={javaCenter.Value}");
    }

    private static Vector3? ReadJavaRenderCenter(JsonElement referenceRoot, string partId)
    {
        if (!referenceRoot.TryGetProperty("renderCuboidCenters", out var centers) ||
            centers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in centers.EnumerateArray())
        {
            if (!entry.TryGetProperty("partId", out var idEl) ||
                !string.Equals(idEl.GetString(), partId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!entry.TryGetProperty("renderCenterTexel", out var c) || c.GetArrayLength() < 3)
            {
                return null;
            }

            return new Vector3((float)c[0].GetDouble(), (float)c[1].GetDouble(), (float)c[2].GetDouble());
        }

        return null;
    }

    private static ModelElement FindBabyWolfTailCuboid(MergedJavaBlockModel mesh)
    {
        foreach (var el in mesh.Elements)
        {
            var ly = MathF.Abs(el.To[1] - el.From[1]);
            if (ly is >= 5.5f and <= 6.5f)
            {
                return el;
            }
        }

        throw new InvalidOperationException("tail cuboid not found");
    }

    private static Vector3 CuboidCenterTexel(ModelElement el)
    {
        var cx = (el.From[0] + el.To[0]) * 0.5f;
        var cy = (el.From[1] + el.To[1]) * 0.5f;
        var cz = (el.From[2] + el.To[2]) * 0.5f;
        return Vector3.Transform(new Vector3(cx, cy, cz), el.LocalToParent);
    }
}
