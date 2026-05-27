using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class ParityCatalogMeshDriverKindSurveyTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    private readonly ITestOutputHelper? _output;

    public ParityCatalogMeshDriverKindSurveyTests(ITestOutputHelper output) => _output = output;
    [Fact]
    public void ResolveAutoLatestModern_ignores_ir_payload_folders_and_prefers_26_1_2_label()
    {
        var nativeRoot = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
        var profile = MinecraftNativeProfileResolver.ResolveAutoLatestModern(nativeRoot);
        if (profile is null)
        {
            return;
        }

        Assert.Equal("26.1.2", profile.Name);
        Assert.NotEqual("setup-anim", profile.Name, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Survey_mesh_driver_breakdown()
    {
        var nativeRoot = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
        var profile = MinecraftNativeProfileResolver.ResolveAutoLatestModern(nativeRoot)
                      ?? new MinecraftNativeProfile(
                          NativeIrVersionLabels.ModernGeometryLabel,
                          nativeRoot,
                          new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        var paths = EntityTextureParityCatalog.GetCataloguedDiffusePathsWithManifestRules();
        var ir = 0;
        var cleanRoom = 0;
        var failed = new List<string>();
        var cleanRoomPaths = new List<string>();
        foreach (var path in paths)
        {
            if (!runtime.TryBuildStaticMesh(path, profile, 0f, 0f, out _, out var provenance))
            {
                failed.Add(path);
                continue;
            }

            if (provenance.Kind == PreviewMeshDriverKind.RuntimeGeometryIrJson)
            {
                ir++;
            }
            else if (provenance.Kind == PreviewMeshDriverKind.CleanRoom)
            {
                cleanRoom++;
                cleanRoomPaths.Add(path);
            }
        }

        _output?.WriteLine($"Total catalogued: {paths.Count}");
        _output?.WriteLine($"RuntimeGeometryIrJson: {ir}");
        _output?.WriteLine($"CleanRoom: {cleanRoom}");
        _output?.WriteLine($"Build failed: {failed.Count}");
        foreach (var p in cleanRoomPaths)
        {
            _output?.WriteLine($"  CleanRoom: {p}");
        }

        foreach (var p in failed)
        {
            _output?.WriteLine($"  FAILED: {p}");
        }
    }

    [Fact]
    public void Catalogued_manifest_paths_majority_use_runtime_geometry_ir()
    {
        var nativeRoot = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
        var profile = MinecraftNativeProfileResolver.ResolveAutoLatestModern(nativeRoot)
                      ?? new MinecraftNativeProfile(
                          NativeIrVersionLabels.ModernGeometryLabel,
                          nativeRoot,
                          new Version(26, 1, 2));
        var runtime = EntityModelRuntimeFactory.Create();
        var paths = EntityTextureParityCatalog.GetCataloguedDiffusePathsWithManifestRules();
        var ir = 0;
        var cleanRoom = 0;
        var failed = new List<string>();
        foreach (var path in paths)
        {
            if (!runtime.TryBuildStaticMesh(path, profile, 0f, 0f, out _, out var provenance))
            {
                failed.Add(path);
                continue;
            }

            if (provenance.Kind == PreviewMeshDriverKind.RuntimeGeometryIrJson)
            {
                ir++;
            }
            else if (provenance.Kind == PreviewMeshDriverKind.CleanRoom)
            {
                cleanRoom++;
            }
        }

        Assert.Empty(failed);
        Assert.Equal(0, cleanRoom);
        Assert.Equal(paths.Count, ir);
    }

    [Fact]
    public void Catalogued_manifest_paths_milestone_builders_use_runtime_geometry_ir()
    {
        var survey = ParityCatalogIrSurveyHelper.Run();
        Assert.Empty(survey.CleanRoomPaths.Where(p =>
            !p.Contains("/horse/", StringComparison.OrdinalIgnoreCase) &&
            !p.Contains("/cat/", StringComparison.OrdinalIgnoreCase) &&
            !p.Contains("/wolf/", StringComparison.OrdinalIgnoreCase) &&
            !p.Contains("/villager/", StringComparison.OrdinalIgnoreCase) &&
            !p.Contains("/player/", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Cow_temperate_uses_runtime_geometry_ir_with_app_style_profile()
    {
        var nativeRoot = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
        var resolved = MinecraftNativeProfileResolver.ResolveForPreview(nativeRoot, inputZipPath: null, extractedPackDir: null);
        var profile = resolved is { Name: var n } && NativeIrVersionLabels.IsRecognizedProfileName(n)
            ? resolved
            : MinecraftNativeProfileResolver.ResolveAutoLatestModern(nativeRoot)
              ?? new MinecraftNativeProfile(
                  NativeIrVersionLabels.ModernGeometryLabel,
                  nativeRoot,
                  new Version(26, 1, 2));

        var runtime = EntityModelRuntimeFactory.Create();
        const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
        Assert.True(runtime.TryBuildStaticMesh(path, profile, 0.2f, 1f, out _, out var provenance));
        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, provenance.Kind);
    }

    [Theory]
    [InlineData("assets/minecraft/textures/entity/armadillo/armadillo.png", false, "ArmadilloModel")]
    [InlineData("assets/minecraft/textures/entity/armadillo/armadillo_baby.png", true, "BabyArmadilloModel")]
    [InlineData("assets/minecraft/textures/entity/breeze/breeze.png", false, "BreezeModel")]
    [InlineData("assets/minecraft/textures/entity/cat/cat_tabby.png", false, "AdultCatModel")]
    [InlineData("assets/minecraft/textures/entity/cat/cat_tabby_baby.png", true, "BabyFelineModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate.png", false, "CowModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_temperate_baby.png", true, "BabyCowModel")]
    [InlineData("assets/minecraft/textures/entity/cow/cow_cold.png", false, "ColdCowModel")]
    [InlineData("assets/minecraft/textures/entity/panda/panda.png", false, "PandaModel")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear.png", false, "PolarBearModel")]
    [InlineData("assets/minecraft/textures/entity/bear/polarbear_baby.png", true, "BabyPolarBearModel")]
    [InlineData("assets/minecraft/textures/entity/fox/fox.png", false, "FoxModel")]
    [InlineData("assets/minecraft/textures/entity/fox/fox_baby.png", true, "BabyFoxModel")]
    public void Gain_path_pilot_entities_use_runtime_geometry_ir(
        string texturePath,
        bool expectBaby,
        string expectedJvmSuffix)
    {
        var row = ParityCatalogEntityPreviewDiagnostics.SurveyPath(texturePath, Profile26);
        Assert.Equal(expectBaby, row.IsBaby);
        Assert.True(
            row.BuildSucceeded,
            $"{row.TexturePath}: build failed ({row.IrFailureReason})");
        Assert.True(
            row.DriverKind == PreviewMeshDriverKind.RuntimeGeometryIrJson,
            $"{row.TexturePath}: driver={row.DriverKind} suppress={row.SuppressesHandFallback} ir={row.IrFailureReason} jvm={row.ResolvedGeometryJvm} detail={row.ProvenanceDetail}");
        Assert.Contains(expectedJvmSuffix, row.ResolvedGeometryJvm ?? row.ProvenanceDetail ?? "", StringComparison.Ordinal);
        Assert.True(row.SuppressesHandFallback);
        Assert.NotEqual(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.Skip, row.LerBasis);
        _output?.WriteLine(
            $"{row.TexturePath}\t{row.DriverKind}\t{row.ResolvedGeometryJvm}\tsuppress={row.SuppressesHandFallback}\tsetup={row.SetupAnimWouldEvaluate}\tstate={row.SetupAnimStateSource}\tler={row.LerBasis}\tdefAnim={row.DefinitionAnimationJvm}");
    }

    [Fact]
    public void Survey_rows_include_viewport_failure_classification_inputs()
    {
        var cow = ParityCatalogEntityPreviewDiagnostics.SurveyPath(
            "assets/minecraft/textures/entity/cow/cow_temperate.png",
            Profile26);
        Assert.Equal(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.RightComposeLocalChain, cow.LerBasis);
        Assert.True(cow.HasSetupAnimDocument);
        Assert.Equal("living-walk", cow.SetupAnimStateSource);

        var breeze = ParityCatalogEntityPreviewDiagnostics.SurveyPath(
            "assets/minecraft/textures/entity/breeze/breeze.png",
            Profile26,
            animationTimeSeconds: 2.5f,
            applyGeometryIrSetupAnimMotion: true);
        Assert.Equal(CleanRoomEntityModelRuntime.GeometryIrLerBasisKind.StandardWorldRoot, breeze.LerBasis);
        Assert.True(breeze.HasSetupAnimDocument);
        Assert.Equal("renderer-state", breeze.SetupAnimStateSource);
    }

    [Fact]
    public void Survey_gain_checklist_table_for_catalog()
    {
        var detailed = ParityCatalogIrSurveyHelper.RunDetailed(Profile26);
        var table = ParityCatalogEntityPreviewDiagnostics.FormatGainChecklistTable(detailed.Rows);
        _output?.WriteLine(table);

        var regressions = detailed.Rows
            .Where(r => r.SuppressesHandFallback && !r.BuildSucceeded)
            .Select(r => r.TexturePath)
            .ToList();
        Assert.Empty(regressions);

        var silentIrMiss = detailed.Rows
            .Where(r => r.SuppressesHandFallback && r.BuildSucceeded && r.DriverKind == PreviewMeshDriverKind.CleanRoom)
            .Select(r => $"{r.TexturePath} ({r.IrFailureReason})")
            .ToList();
        Assert.Empty(silentIrMiss);
    }

    [Fact]
    public void Catalogued_paths_with_suppressed_fallback_never_fail_build_silently()
    {
        var detailed = ParityCatalogIrSurveyHelper.RunDetailed();
        foreach (var row in detailed.Rows.Where(r => r.SuppressesHandFallback))
        {
            Assert.True(
                row.BuildSucceeded && row.DriverKind == PreviewMeshDriverKind.RuntimeGeometryIrJson,
                $"{row.TexturePath}: driver={row.DriverKind} reason={row.IrFailureReason}");
        }
    }
}
