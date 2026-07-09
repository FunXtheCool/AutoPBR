namespace AutoPBR.Core.Tests;

public sealed class RendererStateGapDiagnosticsTests
{
    [Fact]
    public void Gap_audit_marks_new_idle_pilots_as_renderer_state_backed()
    {
        var rows = RendererStateGapDiagnostics.Audit();

        AssertStatus(rows, "net.minecraft.client.model.animal.chicken.ChickenModel", RendererStateGapDiagnostics.HasRendererState);
        AssertStatus(rows, "net.minecraft.client.model.animal.rabbit.RabbitModel", RendererStateGapDiagnostics.HasRendererState);
        AssertStatus(rows, "net.minecraft.client.model.animal.armadillo.ArmadilloModel", RendererStateGapDiagnostics.HasRendererState);
        AssertStatus(rows, "net.minecraft.client.model.ambient.BatModel", RendererStateGapDiagnostics.HasRendererState);
        AssertStatus(rows, "net.minecraft.client.model.animal.fish.CodModel", RendererStateGapDiagnostics.HasRendererState);
    }

    [Fact]
    public void Gap_audit_keeps_remaining_lift_gaps_and_hard_cases_explicit()
    {
        var rows = RendererStateGapDiagnostics.Audit();

        var equine = Assert.Single(rows, r =>
            r.ModelJvmName == "net.minecraft.client.model.animal.equine.AbstractEquineModel");
        Assert.Equal(RendererStateGapDiagnostics.NeedsBooleanOrPoseState, equine.Status);
        Assert.Contains("isInWater", equine.NonLivingWalkStateFields);

        var dragon = Assert.Single(rows, r =>
            r.ModelJvmName == "net.minecraft.client.model.monster.dragon.EnderDragonModel");
        Assert.Equal(RendererStateGapDiagnostics.BlockedLayered, dragon.Status);
    }

    [Fact]
    public void Promotion_gates_accept_represented_or_waived_renderer_state_fields()
    {
        var gates = RendererStateGapDiagnostics.EvaluatePromotionGates();
        Assert.Empty(gates.Where(g => !g.IsGateOk)
            .Select(g => $"{g.RendererJvmName} -> {g.ModelJvmName}: {string.Join(",", g.MissingFields)}"));

        var copper = Assert.Single(gates, g =>
            g.RendererJvmName == "net.minecraft.client.renderer.entity.CopperGolemRenderer");
        Assert.Contains("interactionGetItem", copper.WaivedFields);
    }

    [Fact]
    public void New_idle_preview_drivers_synthesize_expected_state_fields()
    {
        var armadillo = PreviewRenderStateSynthesis.ForArmadillo(2.5f, 0.3f, 0.2f);
        Assert.True(armadillo["rollOutAnimationState"] >= 0f);
        Assert.Equal(RendererStatePreviewResolver.InactiveAnimationStateSentinel, armadillo["peekAnimationState"]);

        var chicken = PreviewRenderStateSynthesis.ForChicken(1f, 0.3f, 0.2f);
        Assert.True(chicken["flapSpeed"] > 0f);

        var cod = PreviewRenderStateSynthesis.ForCod(1f, 0.3f, 0.2f);
        Assert.Equal(1f, cod["isInWater"]);
    }

    private static void AssertStatus(
        IEnumerable<RendererStateGapDiagnostics.SetupAnimRendererStateGapRow> rows,
        string model,
        string expected)
    {
        var row = Assert.Single(rows, r => r.ModelJvmName == model);
        Assert.Equal(expected, row.Status);
    }
}
