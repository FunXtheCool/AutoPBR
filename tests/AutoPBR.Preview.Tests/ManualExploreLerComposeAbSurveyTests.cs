using AutoPBR.Preview;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Focused LER compose A/B report for manual-explore canary textures (diagnostic output).
/// </summary>
public sealed class ManualExploreLerComposeAbSurveyTests
{
    private static readonly string[] FocusTexturePaths =
    [
        "assets/minecraft/textures/entity/allay/allay.png",
        "assets/minecraft/textures/entity/axolotl/axolotl_cyan.png",
        "assets/minecraft/textures/entity/blaze/blaze.png",
        "assets/minecraft/textures/entity/camel/camel.png",
        "assets/minecraft/textures/entity/cat/cat_tabby.png",
        "assets/minecraft/textures/entity/cow/cow_temperate.png",
        "assets/minecraft/textures/entity/cow/cow_cold.png",
        "assets/minecraft/textures/entity/creeper/creeper.png",
        "assets/minecraft/textures/entity/fox/fox.png",
        "assets/minecraft/textures/entity/panda/panda.png",
        "assets/minecraft/textures/entity/bear/polarbear.png",
        "assets/minecraft/textures/entity/pig/pig_temperate.png",
        "assets/minecraft/textures/entity/pig/pig_cold.png",
        "assets/minecraft/textures/entity/pig/pig_warm.png",
        "assets/minecraft/textures/entity/sheep/sheep.png",
    ];

    private readonly ITestOutputHelper _output;

    public ManualExploreLerComposeAbSurveyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Manual_explore_focus_mobs_ler_compose_ab_report()
    {
        var profile = ParityCatalogIrSurveyHelper.ResolveDefaultProfile();
        _output.WriteLine("texture\tjvm\tler_basis\tprobe\tdefault_leg_minus_head\tright_leg_minus_head\trecommended\tmismatch\tbuild_ok\tdriver");
        foreach (var path in FocusTexturePaths)
        {
            var row = ParityCatalogEntityPreviewDiagnostics.SurveyPath(path, profile);
            var mismatch = row.LerComposeProbeAvailable &&
                             !string.IsNullOrEmpty(row.LerComposeRecommended) &&
                             !string.Equals(
                                 row.LerComposeRecommended,
                                 row.LerBasis.ToString(),
                                 StringComparison.Ordinal);
            var shortJvm = row.ResolvedGeometryJvm is { } jvm
                ? jvm[(jvm.LastIndexOf('.') + 1)..]
                : "";
            _output.WriteLine(string.Join('\t',
                path,
                shortJvm,
                row.LerBasis,
                row.LerComposeProbeAvailable ? "1" : "0",
                row.LerDefaultLegMinusHeadY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                row.LerRightComposeLegMinusHeadY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                row.LerComposeRecommended,
                mismatch ? "YES" : "",
                row.BuildSucceeded ? "1" : "0",
                row.DriverKind));
        }

        var writePath = Environment.GetEnvironmentVariable("AUTOPBR_WRITE_LER_COMPOSE_AB_FOCUS");
        if (!string.IsNullOrWhiteSpace(writePath))
        {
            var repo = GeometryIrTestTierSupport.FindRepoRoot();
            var fullPath = Path.IsPathRooted(writePath)
                ? writePath
                : Path.Combine(repo, writePath);
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var lines = new List<string> { "texture\tjvm\tler_basis\tprobe\tdefault_leg_minus_head\tright_leg_minus_head\trecommended\tmismatch\tbuild_ok\tdriver" };
            foreach (var path in FocusTexturePaths)
            {
                var row = ParityCatalogEntityPreviewDiagnostics.SurveyPath(path, profile);
                var mismatch = row.LerComposeProbeAvailable &&
                                 !string.IsNullOrEmpty(row.LerComposeRecommended) &&
                                 !string.Equals(row.LerComposeRecommended, row.LerBasis.ToString(), StringComparison.Ordinal);
                var shortJvm = row.ResolvedGeometryJvm is { } jvm
                    ? jvm[(jvm.LastIndexOf('.') + 1)..]
                    : "";
                lines.Add(string.Join('\t',
                    path,
                    shortJvm,
                    row.LerBasis,
                    row.LerComposeProbeAvailable ? "1" : "0",
                    row.LerDefaultLegMinusHeadY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    row.LerRightComposeLegMinusHeadY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                    row.LerComposeRecommended,
                    mismatch ? "YES" : "",
                    row.BuildSucceeded ? "1" : "0",
                    row.DriverKind));
            }

            File.WriteAllLines(fullPath, lines);
            _output.WriteLine($"Wrote {fullPath}");
        }

        var detailed = ParityCatalogIrSurveyHelper.RunDetailed(profile);
        var mismatches = detailed.Rows
            .Where(r => r.LerComposeProbeAvailable &&
                        !string.IsNullOrEmpty(r.LerComposeRecommended) &&
                        !string.Equals(r.LerComposeRecommended, r.LerBasis.ToString(), StringComparison.Ordinal))
            .ToList();
        _output.WriteLine($"Catalog-wide LER basis mismatches (probe disagrees with policy): {mismatches.Count}");
        foreach (var row in mismatches)
        {
            _output.WriteLine(
                $"  {row.TexturePath}\t{row.ResolvedGeometryJvm}\tpolicy={row.LerBasis}\trecommended={row.LerComposeRecommended}\tdefault={row.LerDefaultLegMinusHeadY:0.###}\tright={row.LerRightComposeLegMinusHeadY:0.###}");
        }
    }
}
