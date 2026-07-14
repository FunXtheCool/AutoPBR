using AutoPBR.App.Rendering.OpenGL;

using Avalonia.OpenGL;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewLiveGlSmokeTests
{
    private const string EnableLiveSmokeEnv = "AUTOPBR_RUN_LIVE_GL_SMOKE";
    private const string ReportPathEnv = "AUTOPBR_P23_SMOKE_REPORT";

    [Fact]
    public void P23_HiddenWglContext_ReportsAndCompilesDesktopAccelerationLanes()
    {
        if (!IsEnabled())
        {
            return;
        }

        Assert.True(OperatingSystem.IsWindows(), "P2.3 live WGL smoke requires Windows.");

        var diagnostics = new List<string>();
        var profiles = new[]
        {
            new GlVersion(GlProfileType.OpenGL, 4, 6),
            new GlVersion(GlProfileType.OpenGL, 4, 0),
            new GlVersion(GlProfileType.OpenGL, 3, 3),
        };

        using var context = PreviewDesktopWglContext.TryCreate(
            profiles,
            IntPtr.Zero,
            diagnostics.Add,
            probePresentationAdapter: false);

        Assert.NotNull(context);
        var report = context!.Invoke(() =>
        {
            using (context.BindOnOwnerThread())
            {
                context.EnsureRenderTargetCore(64, 64);
                var gl = context.Gl;
                var caps = PreviewGlCapabilities.FromGl(gl, useOpenGlEs: false, context.VersionString);
                diagnostics.Add(caps.FormatDiagnostic());
                diagnostics.Add("[3D preview] P2.3 WGL context suffix: " + caps.FormatContextSuffix());

                CompileDesktopGenesisVariant(gl, caps, diagnostics);
                CompileComputeFroxelVariantIfSupported(gl, caps, diagnostics);

                return new LiveGlSmokeReport(
                    context.VersionString,
                    caps.FormatDiagnostic(),
                    caps.FormatContextSuffix(),
                    caps.CanUsePersistentUploadRing,
                    caps.CanUseEntitySkinningSsbo,
                    caps.CanUseMaterialDrawRecordSsbo,
                    caps.CanUseComputeFroxelInject,
                    diagnostics.ToArray());
            }
        }, TimeSpan.FromSeconds(30));

        Assert.Contains("persistentUpload=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("entitySsbo=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("materialDrawSsbo=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("computeFroxels=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        if (report.ComputeFroxels)
        {
            Assert.Contains("compute froxels", report.ContextSuffix, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains("fragment froxels", report.ContextSuffix, StringComparison.OrdinalIgnoreCase);
        }

        WriteReport(report);
    }

    private static void CompileDesktopGenesisVariant(GL gl, PreviewGlCapabilities caps, List<string> diagnostics)
    {
        var defines = OpenGlPreviewBackend.TestBuildGenesisProgramDefines(
            caps.CanUseEntitySkinningSsbo,
            caps.CanUseMaterialDrawRecordSsbo);
        var ctx = new GlShaderCompileContext(gl, useOpenGlEs: false, caps.Vendor, caps.Renderer);
        using var program = ctx.CreateProgram(
            "genesis.vert",
            "genesis.frag",
            out var error,
            "p23-smoke-genesis",
            defines);

        Assert.True(program.IsValid, "Desktop Genesis variant failed to compile: " + error);
        diagnostics.Add("[3D preview] P2.3 desktop Genesis variant compiled.");
    }

    private static void CompileComputeFroxelVariantIfSupported(
        GL gl,
        PreviewGlCapabilities caps,
        List<string> diagnostics)
    {
        if (!caps.CanUseComputeFroxelInject)
        {
            diagnostics.Add("[3D preview] P2.3 compute froxel compile skipped; capability gate is off.");
            return;
        }

        var ctx = new GlShaderCompileContext(gl, useOpenGlEs: false, caps.Vendor, caps.Renderer);
        using var program = ctx.CreateComputeProgram(
            "genesis_volume_inject.comp",
            out var error,
            "p23-smoke-volume-inject-compute");

        Assert.True(program.IsValid, "Compute froxel injector failed to compile: " + error);
        diagnostics.Add("[3D preview] P2.3 compute froxel injector compiled.");
    }

    private static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnableLiveSmokeEnv);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteReport(LiveGlSmokeReport report)
    {
        var path = Environment.GetEnvironmentVariable(ReportPathEnv);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        path = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(FindRepoRoot(), path));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path,
        [
            "P2.3 live GL smoke",
            $"Timestamp UTC: {DateTimeOffset.UtcNow:O}",
            $"WGL version: {report.VersionString}",
            report.CapabilityDiagnostic,
            "Context suffix: " + report.ContextSuffix,
            $"persistentUpload: {(report.PersistentUpload ? "on" : "off")}",
            $"entitySsbo: {(report.EntitySsbo ? "on" : "off")}",
            $"materialDrawSsbo: {(report.MaterialDrawSsbo ? "on" : "off")}",
            $"computeFroxels: {(report.ComputeFroxels ? "on" : "off")}",
            "",
            "Diagnostics:",
            .. report.Diagnostics,
            "",
            "ANGLE/GLES fallback coverage:",
            "Verified by PreviewGlCapabilitiesTests and PreviewGlslEsAdaptTests in the same test run.",
        ]);
    }

    private static string FindRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        foreach (var start in new[] { Path.GetDirectoryName(sourceFilePath), AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AutoPBR.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed record LiveGlSmokeReport(
        string VersionString,
        string CapabilityDiagnostic,
        string ContextSuffix,
        bool PersistentUpload,
        bool EntitySsbo,
        bool MaterialDrawSsbo,
        bool ComputeFroxels,
        string[] Diagnostics);
}
