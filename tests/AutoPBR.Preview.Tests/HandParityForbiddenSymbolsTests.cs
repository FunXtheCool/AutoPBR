namespace AutoPBR.Core.Tests;

public sealed class HandParityForbiddenSymbolsTests
{
    private static readonly string[] ForbiddenIdentifierPatterns = [
        @"\bComputeQuadrupedLegPitchRad\s*\(",
        @"\bComputeStandardQuadrupedFourLegPitchRad\s*\(",
        @"\bComputeChickenWingZRotRad\s*\(",
        @"\bComputeChickenHeadLookRadians\s*\(",
        @"\bBreezeVanillaIdleWindKeyframes\b",
        @"\bNautilusVanillaSwimmingKeyframes\b",
        @"\bTrySampleBreezeIdleWindPositions\b",
        @"\bTrySampleNautilusSwimming\b"
    ];

    [Fact]
    public void Production_sources_do_not_reintroduce_hand_setupAnim_parity_symbols()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AutoPBR.Core"));
        var hits = new List<string>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (file.EndsWith("VanillaAnimationIrPreviewSampler.cs", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith("DefinitionAnimationPreviewSampling.Catalog.cs", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith("DefinitionAnimationPreviewSampling.Catalog.Breeze.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var pattern in ForbiddenIdentifierPatterns)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern))
                {
                    hits.Add($"{Path.GetRelativePath(root, file)}: {pattern}");
                }
            }
        }

        Assert.True(hits.Count == 0, "Forbidden hand-parity symbols found:\n" + string.Join("\n", hits));
    }
}
