using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public class GlslIncludeResolverTests
{
    [Fact]
    public void ResolveInlinesIncludesWithBeginEndMarkers()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main.glsl"] = "before\n//!include \"helper.glsl\"\nafter\n",
            ["helper.glsl"] = "HELPER_BODY\n"
        };

        var output = GlslIncludeResolver.Resolve("main.glsl", n => files[n]);
        // Entry file does NOT get begin/end markers (so #version stays the first token).
        Assert.DoesNotContain("// --- begin main.glsl ---", output);
        Assert.DoesNotContain("// --- end main.glsl ---", output);
        // Nested include DOES get markers.
        Assert.Contains("// --- begin helper.glsl ---", output);
        Assert.Contains("// --- end helper.glsl ---", output);
        Assert.Contains("HELPER_BODY", output);
        Assert.Contains("before", output);
        Assert.Contains("after", output);
        var helperIdx = output.IndexOf("HELPER_BODY", StringComparison.Ordinal);
        var beforeIdx = output.IndexOf("before", StringComparison.Ordinal);
        var afterIdx = output.IndexOf("after", StringComparison.Ordinal);
        Assert.True(beforeIdx < helperIdx && helperIdx < afterIdx);
    }

    [Fact]
    public void ResolveKeepsEntryVersionDirectiveAsFirstToken()
    {
        // Critical: #version must remain the first non-whitespace token after include flattening,
        // otherwise GLSL ES drivers (and the strip pass in GlslSourceAdapter) will reject the source.
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["entry.frag"] = "#version 330 core\n//!include \"x.glsl\"\nMAIN_BODY\n",
            ["x.glsl"] = "X_BODY\n"
        };
        var output = GlslIncludeResolver.Resolve("entry.frag", n => files[n]);
        var trimmed = output.TrimStart();
        Assert.StartsWith("#version 330 core", trimmed, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveIsIncludeGuardedAgainstCycles()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a.glsl"] = "//!include \"b.glsl\"\nA_TAIL\n",
            ["b.glsl"] = "//!include \"a.glsl\"\nB_TAIL\n"
        };

        var output = GlslIncludeResolver.Resolve("a.glsl", n => files[n]);
        Assert.Contains("A_TAIL", output);
        Assert.Contains("B_TAIL", output);
        Assert.Contains("skip duplicate include", output);
    }

    [Fact]
    public void ResolveResolvesRelativePathsWithinSubfolders()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["genesis.frag"] = "//!include \"common/brdf.glsl\"\n",
            ["common/brdf.glsl"] = "//!include \"common.glsl\"\nBRDF_BODY\n",
            ["common/common.glsl"] = "COMMON_BODY\n"
        };

        var output = GlslIncludeResolver.Resolve("genesis.frag", n => files[n]);
        Assert.Contains("COMMON_BODY", output);
        Assert.Contains("BRDF_BODY", output);
        Assert.Contains("// --- begin common/brdf.glsl ---", output);
        Assert.Contains("// --- begin common/common.glsl ---", output);
        // Entry file (genesis.frag) MUST NOT get the begin/end marker.
        Assert.DoesNotContain("// --- begin genesis.frag ---", output);
    }

    [Fact]
    public void ResolveAbsolutePathStartsAtShadersRoot()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["genesis.frag"] = "//!include \"common/brdf.glsl\"\n",
            ["common/brdf.glsl"] = "//!include \"/common/common.glsl\"\nBRDF\n",
            ["common/common.glsl"] = "COMMON\n"
        };

        var output = GlslIncludeResolver.Resolve("genesis.frag", n => files[n]);
        Assert.Contains("COMMON", output);
        Assert.Contains("BRDF", output);
    }

    [Fact]
    public void ResolveDepthCapTriggersOnSelfReference()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Build a long chain that exceeds MaxIncludeDepth: a0 -> a1 -> ... -> a10
        for (var i = 0; i <= 10; i++)
        {
            var next = i < 10 ? $"//!include \"a{i + 1}.glsl\"\n" : string.Empty;
            files[$"a{i}.glsl"] = next + $"BODY_{i}\n";
        }

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GlslIncludeResolver.Resolve("a0.glsl", n => files[n]));
        Assert.Contains("include depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveMissingFileSurfacesAClearError()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main.glsl"] = "//!include \"nope.glsl\"\n"
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GlslIncludeResolver.Resolve("main.glsl", n =>
                files.TryGetValue(n, out var v)
                    ? v
                    : throw new FileNotFoundException(n)));
        Assert.Contains("nope.glsl", ex.Message);
    }

    [Fact]
    public void ResolveNonDirectiveCommentsAreLeftAlone()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main.glsl"] = "// regular comment\n//!includes \"x\"\n//include \"x\"\nDONE\n"
        };

        var output = GlslIncludeResolver.Resolve("main.glsl", n => files[n]);
        Assert.Contains("// regular comment", output);
        Assert.Contains("//!includes \"x\"", output);
        Assert.Contains("//include \"x\"", output);
        Assert.Contains("DONE", output);
    }

    [Fact]
    public void ResolveAtmosphereIncludeFromCommonFolder()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["atmo_skyview.frag"] = "//!include \"common/atmosphere.glsl\"\nMAIN\n",
            ["common/atmosphere.glsl"] = "ATM_BODY\n"
        };

        var output = GlslIncludeResolver.Resolve("atmo_skyview.frag", n => files[n]);
        Assert.Contains("ATM_BODY", output);
        Assert.Contains("MAIN", output);
        Assert.Contains("// --- begin common/atmosphere.glsl ---", output);
    }
}
