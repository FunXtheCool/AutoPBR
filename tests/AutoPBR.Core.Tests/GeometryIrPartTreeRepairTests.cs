using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Phase 5A — preview part-tree repair must not break flat Java quadruped bakes (creeper canary).
/// </summary>
public sealed class GeometryIrPartTreeRepairTests
{
    private const string CreeperJvm = "net.minecraft.client.model.monster.creeper.CreeperModel";
    private const string ChickenJvm = "net.minecraft.client.model.animal.chicken.ChickenModel";
    private const string CodJvm = "net.minecraft.client.model.animal.fish.CodModel";

    [Fact]
    public void Cod_repair_skips_nose_reparent_for_entity_space_flat_root_bind()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{CodJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(CodJvm, raw);

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
        Assert.False(PartNestedUnder(repaired, "nose", "head"));
        Assert.True(
            GeometryIrReferenceHierarchy.TryGetExpectedParentId(CodJvm, "nose", out var refParent));
        Assert.Equal("", refParent);
    }

    [Fact]
    public void Creeper_flat_bake_repair_preserves_world_part_origins()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{CreeperJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(CreeperJvm, raw);

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Feline_repair_keeps_tail2_flat_sibling_and_preview_tail_stays_attached()
    {
        const string jvm = "net.minecraft.client.model.animal.feline.AdultCatModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.False(PartNestedUnder(repaired, "tail2", "tail1"));

        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/cat/cat_calico",
            new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2)),
            jvm,
            64,
            64,
            out var failure,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(failure);

        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity() with { OfficialJvmName = jvm });
        float? tail1Y = null;
        float? tail2Y = null;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            var cy = (mesh.Elements[i].LocalToParent.M41 + mesh.Elements[i].LocalToParent.M42 +
                      mesh.Elements[i].LocalToParent.M43) / 3f;
            if (partId.Contains("tail1", StringComparison.Ordinal))
            {
                tail1Y = cy;
            }
            else if (partId.Contains("tail2", StringComparison.Ordinal))
            {
                tail2Y = cy;
            }
        }

        Assert.True(tail1Y.HasValue && tail2Y.HasValue);
        Assert.True(MathF.Abs(tail2Y.Value - tail1Y.Value) < 8f,
            $"tail2 should follow tail1 in preview space (tail1Y={tail1Y:F3} tail2Y={tail2Y:F3})");
    }

    [Fact]
    public void Creeper_repair_does_not_stack_body_y_onto_leg_origins()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{CreeperJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(CreeperJvm, shard.RootElement);
        Assert.True(
            GeometryIrMeshWalk.TryCollectPartWorldTranslations(geometryRoot, Matrix4x4.Identity, out var byPart, out var walkFail),
            walkFail);
        Assert.True(byPart.TryGetValue("right_hind_leg", out var legOrigin));
        Assert.InRange(legOrigin.Y, 17.5f, 18.5f);
        Assert.True(byPart.TryGetValue("body", out var bodyOrigin));
        Assert.InRange(bodyOrigin.Y, 5.5f, 6.5f);
        Assert.False(byPart.Values.Any(v => Math.Abs(v.Y - 24f) < 0.1f),
            "leg origins must not pick up stacked body+leg Y from harmful reparent");
    }

    [Fact]
    public void Chicken_ok_shard_still_reparents_beak_under_head()
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{ChickenJvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(ChickenJvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "beak", "head"), "beak should nest under head after repair");
    }

    [Fact]
    public void Adult_axolotl_repair_renests_hoisted_flat_legs_under_body_for_preview()
    {
        const string jvm = "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var hoistedRoot = HoistQuadrupedLegsToMeshRoot(shard.RootElement);
        Assert.False(PartNestedUnder(hoistedRoot, "left_hind_leg", "body"));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, hoistedRoot);
        Assert.True(PartNestedUnder(repaired, "left_hind_leg", "body"));

        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(refPath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(refPath));
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/axolotl/axolotl_blue",
            profile,
            jvm,
            64,
            64,
            out var failure,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(failure);

        var refCmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(
            reference.RootElement, mesh, tolerance: 0.08);
        Assert.True(refCmp.IsMatch, refCmp.Message);
    }

    [Fact]
    public void Adult_axolotl_repair_preserves_nested_leg_world_origins()
    {
        const string jvm = "net.minecraft.client.model.animal.axolotl.AdultAxolotlModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, raw);

        Assert.True(PartNestedUnder(repaired, "left_front_leg", "body"));
        Assert.True(PartNestedUnder(repaired, "top_gills", "head"));

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);

        var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
        var refPath = Path.Combine(repo, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(refPath))
        {
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(refPath));
        var mesh = CleanRoomEntityModelRuntime.TryBuildGeometryIrParityMeshForTests(
            "entity/axolotl/axolotl_blue",
            profile,
            jvm,
            64,
            64,
            out var failure,
            geometryRootOverride: repaired);
        Assert.NotNull(mesh);
        Assert.Null(failure);

        var refCmp = GeometryIrReferenceComparer.CompareReferenceToParityMesh(
            reference.RootElement, mesh, tolerance: 0.08);
        Assert.True(refCmp.IsMatch, refCmp.Message);
    }

    [Fact]
    public void Armadillo_repair_preserves_flat_root_leg_world_origins()
    {
        const string jvm = "net.minecraft.client.model.animal.armadillo.AdultArmadilloModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, raw);

        Assert.False(PartNestedUnder(repaired, "left_hind_leg", "body"));
        Assert.False(PartNestedUnder(repaired, "right_front_leg", "body"));

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.wolf.AdultWolfModel")]
    [InlineData("net.minecraft.client.model.animal.wolf.BabyWolfModel")]
    public void Wolf_repair_preserves_flat_root_leg_world_origins(string jvm)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, raw);

        Assert.False(PartNestedUnder(repaired, "left_hind_leg", "body"));
        Assert.False(PartNestedUnder(repaired, "right_front_leg", "body"));

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Theory]
    [InlineData("net.minecraft.client.model.animal.llama.LlamaModel")]
    [InlineData("net.minecraft.client.model.animal.llama.BabyLlamaModel")]
    public void Llama_flat_bake_repair_preserves_flat_root_leg_world_origins(string jvm)
    {
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, raw);

        Assert.False(PartNestedUnder(repaired, "left_hind_leg", "body"));
        Assert.False(PartNestedUnder(repaired, "right_front_leg", "body"));

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Camel_repair_preserves_flat_root_leg_world_origins()
    {
        const string jvm = "net.minecraft.client.model.animal.camel.AdultCamelModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, raw);

        Assert.False(PartNestedUnder(repaired, "left_hind_leg", "body"));
        Assert.False(PartNestedUnder(repaired, "right_front_leg", "body"));

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Baby_donkey_repair_nests_flat_leg_siblings_under_body()
    {
        const string jvm = "net.minecraft.client.model.animal.equine.BabyDonkeyModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "left_front_leg", "body"));
        Assert.True(PartNestedUnder(repaired, "right_hind_leg", "body"));
        Assert.True(PartNestedUnder(repaired, "tail_r1", "tail"));
        Assert.True(PartNestedUnder(repaired, "head", "head_parts"));
        Assert.True(PartNestedUnder(repaired, "head_r1", "head"));
        Assert.True(PartNestedUnder(repaired, "left_ear", "head"));
        Assert.True(PartNestedUnder(repaired, "neck_r1", "head_parts"));
    }

    [Fact]
    public void Baby_horse_repair_nests_ears_under_head()
    {
        const string jvm = "net.minecraft.client.model.animal.equine.BabyHorseModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "left_ear", "head"));
        Assert.True(PartNestedUnder(repaired, "right_ear", "head"));
    }

    [Fact]
    public void Adult_rabbit_repair_preserves_frontlegs_backlegs_hierarchy()
    {
        const string jvm = "net.minecraft.client.model.animal.rabbit.AdultRabbitModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var raw = shard.RootElement;
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, raw);

        Assert.True(PartNestedUnder(repaired, "left_front_leg", "frontlegs"));
        Assert.False(PartNestedUnder(repaired, "left_front_leg", "body"));
        Assert.False(PartNestedUnder(repaired, "right_hind_leg", "body"));

        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(raw, repaired, tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void Baby_horse_flat_bake_does_not_reparent_root_sibling_legs()
    {
        const string jvm = "net.minecraft.client.model.animal.equine.BabyHorseModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.False(PartNestedUnder(repaired, "left_front_leg", "body"));
    }

    [Fact]
    public void PlayerModel_repair_reparents_sleeves_and_mirrors_right_arm_offset()
    {
        const string jvm = "net.minecraft.client.model.player.PlayerModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        if (!shard.RootElement.TryGetProperty("extractionStatus", out var statusEl) ||
            statusEl.GetString() is not ("ok" or "partial"))
        {
            return;
        }

        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "right_sleeve", "right_arm"));
        Assert.True(PartNestedUnder(repaired, "left_sleeve", "left_arm"));
        Assert.False(PartTreeContainsPartId(repaired, "waist"));
        Assert.False(PartTreeContainsPartId(repaired, "inner_body"));

        var rightArm = FindPart(repaired, "right_arm");
        Assert.NotNull(rightArm);
        var tr = rightArm["pose"]?["translation"]?.AsArray();
        Assert.NotNull(tr);
        Assert.True(tr[0]!.GetValue<double>() < -4.5);
        Assert.InRange(tr[1]!.GetValue<double>(), 1.5, 2.5);
    }

    [Fact]
    public void PlayerModel_repair_resets_mis_lifted_head_body_poses_and_legs()
    {
        const string jvm = "net.minecraft.client.model.player.PlayerModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var head = FindPart(repaired, "head");
        var body = FindPart(repaired, "body");
        var rightLeg = FindPart(repaired, "right_leg");
        Assert.NotNull(head);
        Assert.NotNull(body);
        Assert.NotNull(rightLeg);
        Assert.InRange(head["pose"]!["translation"]![1]!.GetValue<double>(), -0.05, 0.05);
        Assert.InRange(body["pose"]!["translation"]![1]!.GetValue<double>(), -0.05, 0.05);
        Assert.InRange(rightLeg["pose"]!["translation"]![1]!.GetValue<double>(), 11.5, 12.5);

        var headCuboid = head["cuboids"]![0]!;
        Assert.InRange(headCuboid["from"]![1]!.GetValue<double>(), -8.5, -7.5);
        Assert.InRange(headCuboid["to"]![1]!.GetValue<double>(), -0.5, 0.5);
        Assert.Equal(16, body["cuboids"]![0]!["uvOrigin"]![0]!.GetValue<int>());
        Assert.Equal(16, body["cuboids"]![0]!["uvOrigin"]![1]!.GetValue<int>());
    }

    [Fact]
    public void ArmorStand_repair_preserves_small_head_cuboid_despite_hat_overlay()
    {
        const string jvm = "net.minecraft.client.model.object.armorstand.ArmorStandModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var head = FindPart(repaired, "head");
        Assert.NotNull(head);
        var headCuboid = head["cuboids"]![0]!;
        Assert.InRange(headCuboid["from"]![0]!.GetValue<double>(), -1.5, -0.5);
        Assert.InRange(headCuboid["to"]![0]!.GetValue<double>(), 0.5, 1.5);
        Assert.InRange(headCuboid["from"]![1]!.GetValue<double>(), -7.5, -6.5);
        Assert.InRange(headCuboid["to"]![1]!.GetValue<double>(), -0.5, 0.5);
    }

    [Fact]
    public void AdultPiglin_repair_reparents_player_wide_overlay_kit_for_brute_preview()
    {
        const string jvm = "net.minecraft.client.model.monster.piglin.AdultPiglinModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!GeometryIrTestTierSupport.TryReadCommittedShardStatus(shardPath, out var status) ||
            !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        Assert.True(PartNestedUnder(repaired, "right_pants", "right_leg"));
        Assert.True(PartNestedUnder(repaired, "left_pants", "left_leg"));
        Assert.True(PartNestedUnder(repaired, "jacket", "body"));
        Assert.True(PartNestedUnder(repaired, "right_sleeve", "right_arm"));
        Assert.False(PartNestedUnder(repaired, "jacket", "right_pants"));
        Assert.False(PartTreeContainsPartId(repaired, "waist"));
    }

    [Fact]
    public void PlayerSlim_variant_narrows_arm_cuboids()
    {
        const string jvm = "net.minecraft.client.model.player.PlayerModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        if (!File.Exists(shardPath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var slim = GeometryIrPlayerArmVariant.ApplySlimArmsIfNeeded("PlayerSlim", repaired);
        var leftArm = FindPart(slim, "left_arm");
        Assert.NotNull(leftArm);
        var to = leftArm["cuboids"]![0]!["to"]!.AsArray();
        Assert.InRange(to[0]!.GetValue<double>(), 1.9, 2.1);

        var leftSleeve = FindPart(slim, "left_sleeve");
        var rightSleeve = FindPart(slim, "right_sleeve");
        Assert.NotNull(leftSleeve);
        Assert.NotNull(rightSleeve);
        Assert.InRange(leftSleeve["cuboids"]![0]!["to"]![0]!.GetValue<double>(), 1.9, 2.1);
        Assert.InRange(rightSleeve["cuboids"]![0]!["from"]![0]!.GetValue<double>(), -2.1, -1.9);
    }

    [Fact]
    public void ZombieModel_reference_repair_flattens_head_and_arm_poses_for_preview()
    {
        const string jvm = "net.minecraft.client.model.monster.zombie.ZombieModel";
        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        var shardPath = Path.Combine(repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        var referencePath = Path.Combine(
            repo,
            "tools",
            "MinecraftGeometryReference",
            "reference-output",
            $"{jvm}.json");
        if (!File.Exists(shardPath) || !File.Exists(referencePath))
        {
            return;
        }

        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var cmp = GeometryIrReferenceComparer.CompareReferenceWorldPartOrigins(
            reference.RootElement,
            repaired,
            tolerance: 0.08);
        Assert.True(cmp.IsMatch, cmp.Message);

        var rightArm = FindPart(repaired, "right_arm");
        Assert.NotNull(rightArm);
        var armTy = rightArm["pose"]!["translation"]![1]!.GetValue<double>();
        Assert.InRange(armTy, 1.5, 2.5);
    }

    private static bool PartTreeContainsPartId(JsonElement root, string partId) => FindPart(root, partId) is not null;

    private static JsonObject? FindPart(JsonElement geometryRoot, string partId)
    {
        var node = JsonNode.Parse(geometryRoot.GetRawText());
        if (node?["roots"]?[0]?["children"] is not JsonArray kids)
        {
            return null;
        }

        return FindPartObject(kids, partId);
    }

    private static JsonObject? FindPartObject(JsonArray parts, string partId)
    {
        foreach (var n in parts)
        {
            if (n is not JsonObject o)
            {
                continue;
            }

            if (string.Equals((string?)o["id"], partId, StringComparison.Ordinal))
            {
                return o;
            }

            if (o["children"] is JsonArray kids && FindPartObject(kids, partId) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static JsonElement HoistQuadrupedLegsToMeshRoot(JsonElement geometryRoot)
    {
        var node = JsonNode.Parse(geometryRoot.GetRawText());
        if (node is not JsonObject doc || doc["roots"] is not JsonArray roots)
        {
            return geometryRoot;
        }

        foreach (var root in roots)
        {
            if (root is not JsonObject ro || ro["children"] is not JsonArray meshKids)
            {
                continue;
            }

            if (!TryFindPartObject(meshKids, "body", out var body) || body is null ||
                body["children"] is not JsonArray bodyKids)
            {
                continue;
            }

            for (var i = bodyKids.Count - 1; i >= 0; i--)
            {
                if (bodyKids[i] is not JsonObject child ||
                    child["id"]?.GetValue<string>() is not { } id ||
                    !id.Contains("leg", StringComparison.Ordinal))
                {
                    continue;
                }

                meshKids.Add(child.DeepClone());
                bodyKids.RemoveAt(i);
            }
        }

        return JsonDocument.Parse(doc.ToJsonString()).RootElement;
    }

    private static bool TryFindPartObject(JsonArray searchRoots, string partId, out JsonObject? found)
    {
        found = null;
        foreach (var n in searchRoots)
        {
            if (n is not JsonObject part)
            {
                continue;
            }

            if (string.Equals((string?)part["id"], partId, StringComparison.Ordinal))
            {
                found = part;
                return true;
            }

            if (part["children"] is JsonArray kids && TryFindPartObject(kids, partId, out found))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PartNestedUnder(JsonElement geometryRoot, string childId, string parentId)
    {
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!root.TryGetProperty("children", out var children))
            {
                continue;
            }

            if (TryFindPart(children, parentId, out var parent) &&
                parent.TryGetProperty("children", out var parentKids) &&
                parentKids.EnumerateArray().Any(ch =>
                    ch.TryGetProperty("id", out var id) &&
                    string.Equals(id.GetString(), childId, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPart(JsonElement parts, string id, out JsonElement found)
    {
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("id", out var idEl) &&
                string.Equals(idEl.GetString(), id, StringComparison.Ordinal))
            {
                found = part;
                return true;
            }

            if (part.TryGetProperty("children", out var kids) && TryFindPart(kids, id, out found))
            {
                return true;
            }
        }

        found = default;
        return false;
    }

}
