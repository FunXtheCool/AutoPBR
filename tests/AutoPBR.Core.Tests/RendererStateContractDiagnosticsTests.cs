namespace AutoPBR.Core.Tests;

public sealed class RendererStateContractDiagnosticsTests
{
    [Fact]
    public void Discover_current_renderer_state_shards_reports_all_hand_pilots()
    {
        var rows = RendererStateContractDiagnostics.Discover();

        Assert.Equal(16, rows.Count);
        Assert.Empty(rows.Where(r => !r.IsContractOk)
            .Select(r => $"{r.ShardFileName}: {string.Join(", ", r.ContractIssues)}"));
        Assert.All(rows, r => Assert.Equal(RendererStateContractDiagnostics.HandPilotSource, r.SourceCategory));
        Assert.All(rows, r => Assert.Equal(RendererStateDocumentLoader.VersionLabel, r.VersionLabel));

        Assert.Contains(rows, r => r.DriverCategory == RendererStateContractDiagnostics.ClipCycleDriverCategory);
        Assert.Contains(rows, r => r.DriverCategory == RendererStateContractDiagnostics.PhaseCycleDriverCategory);
        Assert.Contains(rows, r => r.DriverCategory == RendererStateContractDiagnostics.WalkCycleDriverCategory);
        Assert.Contains(rows, r => r.DriverCategory == RendererStateContractDiagnostics.ScalarDriverCategory);

        var allay = Assert.Single(rows, r =>
            r.OfficialRendererJvmName == "net.minecraft.client.renderer.entity.AllayRenderer");
        Assert.Equal("scalar", allay.FieldCategory);
        Assert.Equal(4, allay.ScalarRenderStateFields.Count);

        var nautilus = Assert.Single(rows, r =>
            r.OfficialRendererJvmName == "net.minecraft.client.renderer.entity.NautilusRenderer");
        Assert.Equal("living-only", nautilus.FieldCategory);
        Assert.Equal(RendererStateContractDiagnostics.WalkCycleDriverCategory, nautilus.DriverCategory);

        var chicken = Assert.Single(rows, r =>
            r.OfficialRendererJvmName == "net.minecraft.client.renderer.entity.ChickenRenderer");
        Assert.Equal("scalar", chicken.FieldCategory);
        Assert.Contains("flapSpeed", chicken.ScalarRenderStateFields);
    }

    [Fact]
    public void Discovery_report_tracks_unique_model_bindings_and_contract_counts()
    {
        var rows = RendererStateContractDiagnostics.Discover();

        var duplicateModels = rows
            .SelectMany(r => r.ModelJvmNames.Select(model => (model, r.OfficialRendererJvmName)))
            .GroupBy(x => x.model, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(x => x.OfficialRendererJvmName))}")
            .ToArray();
        Assert.Empty(duplicateModels);

        Assert.All(
            rows.Where(r => r.DriverCategory == RendererStateContractDiagnostics.ClipCycleDriverCategory),
            r => Assert.NotEmpty(r.AnimationStateFields));

        var table = RendererStateContractDiagnostics.FormatReportTable(rows);
        Assert.StartsWith("renderer\tdriver\tdriver_category\tsource\tfield_category", table);
        Assert.Contains("net.minecraft.client.renderer.entity.BreezeRenderer\tbreeze_clip_cycle\tclip-cycle", table);
        Assert.Contains("\t1\t", table);
    }

    [Fact]
    public void Discovery_contract_flags_malformed_shards()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"autopbr-renderer-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(
                Path.Combine(dir, "BrokenRenderer.json"),
                """
                {
                  "schemaVersion": 1,
                  "versionLabel": "26.1.2",
                  "officialJvmName": "DifferentRenderer",
                  "previewDriver": "custom_unhandled_driver"
                }
                """);

            var row = Assert.Single(RendererStateContractDiagnostics.DiscoverDirectory(dir, "26.1.2"));

            Assert.False(row.IsContractOk);
            Assert.Contains("officialJvmName_file_mismatch", row.ContractIssues);
            Assert.Contains("renderStateType_missing", row.ContractIssues);
            Assert.Contains("modelJvmNames_empty", row.ContractIssues);
            Assert.Contains("extractionStatus_missing", row.ContractIssues);
            Assert.Contains("previewDriver_unclassified", row.ContractIssues);
            Assert.Equal(RendererStateContractDiagnostics.UnknownSource, row.SourceCategory);
            Assert.Equal(RendererStateContractDiagnostics.UnknownDriverCategory, row.DriverCategory);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
