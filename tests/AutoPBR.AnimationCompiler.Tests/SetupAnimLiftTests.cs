using System.Text.Json.Nodes;

using AutoPBR.Tests.TestSupport;
using AutoPBR.Tools.AnimationCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class SetupAnimLiftTests
{
    private static string ClientJarPath =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tools", "minecraft-parity", "26.1.2", "client.jar"));

    [Fact]
    public void TryLift_QuadrupedModel_has_four_leg_cos_assignments()
    {
        var disasm = Disassemble("net.minecraft.client.model.QuadrupedModel");
        Assert.True(SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.QuadrupedModel", out var shard, out var notes), string.Join("; ", notes));
        var assignments = shard["assignments"]!.AsArray();
        Assert.True(assignments.Count >= 6, $"expected head+4 legs, got {assignments.Count}");
        Assert.Contains(assignments, a => MatchPartProp(a, "rightHindLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "leftFrontLeg", "xRot"));
        Assert.DoesNotContain(notes, n => n.Contains("Unsupported fload", StringComparison.Ordinal));
    }

    [Fact]
    public void TryLift_EntityModel_has_typed_setupAnim_or_inheritance_only()
    {
        var disasm = Disassemble("net.minecraft.client.model.EntityModel");
        if (!SetupAnimLift.TryExtractTypedSetupAnimCode(disasm, out _, out _))
        {
            return;
        }

        Assert.True(SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.EntityModel", out var shard, out _));
        Assert.NotNull(shard["setupAnimMethod"]);
    }

    [Fact]
    public void TryLift_HumanoidModel_inherits_entity_and_has_arm_swing()
    {
        var disasm = Disassemble("net.minecraft.client.model.HumanoidModel");
        Assert.True(SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.HumanoidModel", out var shard, out var notes), string.Join("; ", notes));
        Assert.Equal("net.minecraft.client.model.EntityModel", (string?)shard["inheritsSetupAnimFrom"]);
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "rightArm", "xRot") || MatchPartProp(a, "leftArm", "xRot"));
    }

    [Fact]
    public void TryLift_HumanoidModel_has_walk_limbs_and_no_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.HumanoidModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.HumanoidModel", out var shard, out var notes),
            string.Join("; ", notes));
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "rightArm", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "leftArm", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "rightLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "leftLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "head", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "head", "yRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_ChickenModel_has_legs_and_render_state_head_look()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.chicken.ChickenModel");
        Assert.True(SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.chicken.ChickenModel", out var shard, out var chickenNotes), string.Join("; ", chickenNotes));
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "rightLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "leftLeg", "xRot"));
    }

    [Fact]
    public void TryLift_FrogModel_has_baked_playback_wiring()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.frog.FrogModel");
        Assert.True(SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.frog.FrogModel", out var shard, out _));
        Assert.True(shard["bakedAnimations"] is JsonArray { Count: > 0 });
        Assert.True(shard["playbackSteps"] is JsonArray { Count: > 0 });
    }

    [Fact]
    public void TryLift_AbstractEquineModel_cached_javap_snapshot_without_blocking_notes()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            ".tmpbuild", "abstract_equine_javap.txt"));
        if (!File.Exists(path))
        {
            return;
        }

        var disasm = File.ReadAllText(path);
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.equine.AbstractEquineModel", out var shard, out var notes),
            string.Join("; ", notes));
        AssertAbstractEquineLiftOk(shard, notes);
    }

    [Fact]
    public void TryLift_AbstractEquineModel_has_tail_and_legs_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.equine.AbstractEquineModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.equine.AbstractEquineModel", out var shard, out var notes),
            string.Join("; ", notes));
        AssertAbstractEquineLiftOk(shard, notes);
    }

    private static void AssertAbstractEquineLiftOk(JsonObject shard, List<string> notes)
    {
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "tail", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "headParts", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "body", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "leftHindLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "rightHindLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "leftFrontLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "rightFrontLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "rightFrontLeg", "y"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_EquineSaddleModel_inherits_abstract_equine_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.equine.EquineSaddleModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.equine.EquineSaddleModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Equal("net.minecraft.client.model.animal.equine.AbstractEquineModel", (string?)shard["inheritsSetupAnimFrom"]);
        var blocking = notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n));
        Assert.Equal(0, blocking);
    }

    [Fact]
    public void TryLift_AbstractFelineModel_hoists_from_AdultFeline_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.feline.AbstractFelineModel");
        Assert.False(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.feline.AbstractFelineModel", out _, out _));
        Assert.True(
            SetupAnimLift.TryHoistAbstractHostSetupAnim(
                JavapLocator.FindJavap()!,
                ClientJarPath,
                "net.minecraft.client.model.animal.feline.AbstractFelineModel",
                out var shard,
                out var notes),
            string.Join("; ", notes));
        AssertAbstractFelineLiftOk(shard, notes);
    }

    [Fact]
    public void TryLift_AdultFelineModel_has_legs_head_tail_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.feline.AdultFelineModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.feline.AdultFelineModel", out var shard, out var notes),
            string.Join("; ", notes));
        AssertAbstractFelineLiftOk(shard, notes);
        Assert.Equal("net.minecraft.client.model.animal.feline.AbstractFelineModel", (string?)shard["inheritsSetupAnimFrom"]);
    }

    [Fact]
    public void TryLift_BabyFelineModel_has_legs_head_tail_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.feline.BabyFelineModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.feline.BabyFelineModel", out var shard, out var notes),
            string.Join("; ", notes));
        AssertAbstractFelineLiftOk(shard, notes);
        Assert.Equal("net.minecraft.client.model.animal.feline.AbstractFelineModel", (string?)shard["inheritsSetupAnimFrom"]);
    }

    private static void AssertAbstractFelineLiftOk(JsonObject shard, List<string> notes)
    {
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "head", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "head", "yRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "tail2", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "rightHindLeg", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "leftFrontLeg", "xRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_AllayModel_has_wings_arms_and_no_equine_legs()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.allay.AllayModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.allay.AllayModel", out var shard, out var notes),
            string.Join("; ", notes));
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "right_wing", "yRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "left_wing", "yRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "body", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "right_arm", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "root", "y"));
        Assert.DoesNotContain(assignments, a => MatchPartProp(a, "rightHindLeg", "xRot"));
        Assert.DoesNotContain(assignments, a => MatchPartProp(a, "headParts", "xRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));

        var armXJson = assignments.First(a => MatchPartProp(a, "right_arm", "xRot"))!.AsObject()["expr"]!.ToJsonString();
        Assert.Contains("\"lerp\"", armXJson, StringComparison.Ordinal);
    }

    [Fact]
    public void TryLift_AbstractPiglinModel_inherits_humanoid_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.piglin.AbstractPiglinModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.piglin.AbstractPiglinModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Equal("net.minecraft.client.model.HumanoidModel", (string?)shard["inheritsSetupAnimFrom"]);
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_VexModel_has_wings_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.vex.VexModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.vex.VexModel", out var shard, out var notes),
            string.Join("; ", notes));
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "leftWing", "zRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "rightWing", "zRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_SpiderModel_has_eight_leg_peer_chain_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.spider.SpiderModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.spider.SpiderModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "rightHindLeg", "yRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_IronGolemModel_has_arms_head_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.golem.IronGolemModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.golem.IronGolemModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "rightArm", "xRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_ArmadilloModel_has_playback_wiring_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.armadillo.ArmadilloModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.armadillo.ArmadilloModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.True(shard["playbackSteps"] is JsonArray { Count: > 0 });
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_RavagerModel_has_head_body_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.ravager.RavagerModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.ravager.RavagerModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "head", "xRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_SquidModel_has_tentacle_xRot_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.squid.SquidModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.squid.SquidModel", out var shard, out var notes),
            string.Join("; ", notes));
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "tentacle0", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "tentacle7", "xRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_NautilusModel_has_swim_playback_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.nautilus.NautilusModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.nautilus.NautilusModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.True(shard["playbackSteps"] is JsonArray { Count: > 0 });
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_BlazeModel_has_head_and_rods_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.blaze.BlazeModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.blaze.BlazeModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "head", "xRot"));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "head", "yRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_EndermiteModel_has_segment_wiggle_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.endermite.EndermiteModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.endermite.EndermiteModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "segment0", "yRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_MagmaCubeModel_has_segment_y_from_bodyCubes_loop()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.slime.MagmaCubeModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.slime.MagmaCubeModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "cube0", "y"));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "cube7", "y"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryWriteRendererDrivenSlimeShard_has_effect_only_inheritance()
    {
        var disasm = Disassemble("net.minecraft.client.model.monster.slime.SlimeModel");
        Assert.False(SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.monster.slime.SlimeModel", out _, out _));
        Assert.True(
            SetupAnimLift.TryWriteRendererDrivenSlimeShard(
                "net.minecraft.client.model.monster.slime.SlimeModel",
                disasm,
                out var shard,
                out var notes));
        Assert.Equal("net.minecraft.client.model.EntityModel", (string?)shard["inheritsSetupAnimFrom"]);
        Assert.True(shard["setupAnimEffectOnly"]!.GetValue<bool>());
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_SpinAttackEffectModel_has_box_yRot_assignments()
    {
        var disasm = Disassemble("net.minecraft.client.model.effects.SpinAttackEffectModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.effects.SpinAttackEffectModel", out var shard, out var notes),
            string.Join("; ", notes));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "box0", "yRot"));
        Assert.Contains(shard["assignments"]!.AsArray(), a => MatchPartProp(a, "box1", "yRot"));
        Assert.Equal(0, notes.Count(n => !SetupAnimLift.IsNonBlockingNote(n)));
    }

    [Fact]
    public void TryLift_DolphinModel_has_body_swim_xRot_without_blocking_notes()
    {
        var disasm = Disassemble("net.minecraft.client.model.animal.dolphin.DolphinModel");
        Assert.True(
            SetupAnimLift.TryLift(disasm, "net.minecraft.client.model.animal.dolphin.DolphinModel", out var shard, out var notes),
            string.Join("; ", notes));
        var assignments = shard["assignments"]!.AsArray();
        Assert.Contains(assignments, a => MatchPartProp(a, "body", "xRot"));
        Assert.Contains(assignments, a => MatchPartProp(a, "tail", "xRot"));
        Assert.DoesNotContain(notes, n => n.Contains("Could not resolve model part field", StringComparison.Ordinal));
        Assert.DoesNotContain(notes, n => n.Contains("Expression stack did not reduce", StringComparison.Ordinal));
    }

    private static string Disassemble(string officialJvmName)
    {
        var cachePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            ".tmpbuild",
            $"{officialJvmName.Replace('.', '_')}_setupanim_javap.txt"));
        if (File.Exists(cachePath))
        {
            return File.ReadAllText(cachePath);
        }

        var jar = GeometryIrTestTierSupport.TryClientJarPath(GeometryIrTestTierSupport.FindRepoRoot());
        if (jar is null)
        {
            return string.Empty;
        }

        var javap = JavapLocator.FindJavap();
        Assert.False(string.IsNullOrWhiteSpace(javap), "javap not found on PATH");
        Assert.True(JavapRunner.TryDisassemble(javap, jar, officialJvmName, out var disasm, out var err), err ?? "javap failed");
        var cacheDir = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(cachePath, disasm);
        }

        return disasm;
    }

    private static bool MatchPartProp(JsonNode? node, string part, string prop) =>
        node is JsonObject o &&
        string.Equals((string?)o["partField"], part, StringComparison.Ordinal) &&
        string.Equals((string?)o["property"], prop, StringComparison.Ordinal);
}
