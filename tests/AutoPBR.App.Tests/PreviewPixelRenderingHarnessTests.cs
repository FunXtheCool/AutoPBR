using System.Text.Json;
using System.Globalization;

using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.Preview;

using Avalonia.OpenGL;

using Silk.NET.OpenGL;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoPBR.App.Tests;

public sealed class PreviewPixelRenderingHarnessTests
{
    private const string EnableHarnessEnv = "AUTOPBR_RUN_PIXEL_GL_HARNESS";
    private const string EnableLiveSmokeEnv = "AUTOPBR_RUN_LIVE_GL_SMOKE";
    private const string ArtifactDirectoryEnv = "AUTOPBR_PIXEL_HARNESS_ARTIFACT_DIR";
    private const int Width = 160;
    private const int Height = 120;
    private static readonly byte[] ClearRgba = [7, 11, 19, 255];
    private static readonly JsonSerializerOptions ArtifactJsonOptions = new() { WriteIndented = true };

    [Fact]
    public void HiddenWglContext_AccelerationLanesMatchDirectPixelBaseline()
    {
        if (!IsEnabled())
        {
            return;
        }

        Assert.True(OperatingSystem.IsWindows(), "Live pixel rendering harness requires Windows WGL.");
        var diagnostics = new List<string>();
        GlPixelHarnessRun? run;
        using (var context = PreviewDesktopWglContext.TryCreate(
                   [
                       new GlVersion(GlProfileType.OpenGL, 4, 6),
                       new GlVersion(GlProfileType.OpenGL, 4, 0),
                       new GlVersion(GlProfileType.OpenGL, 3, 3),
                   ],
                   IntPtr.Zero,
                   diagnostics.Add,
                   probePresentationAdapter: false))
        {
            Assert.NotNull(context);
            run = context!.Invoke(
                () =>
                {
                    using (context.BindOnOwnerThread())
                    {
                        return RunPixelMatrix(context.Gl, context.VersionString, diagnostics);
                    }
                },
                TimeSpan.FromSeconds(45));
        }

        var artifactDirectory = ResolveArtifactDirectory(run);
        if (artifactDirectory is not null)
        {
            WriteArtifacts(run, artifactDirectory);
        }

        Assert.True(
            run.Baseline.CountPixelsOutside(ClearRgba, tolerance: 1) >= run.Baseline.PixelCount / 3,
            "Direct baseline did not render enough fixture pixels to validate submission parity.");
        Assert.NotEmpty(run.Comparisons);
        foreach (var comparison in run.Comparisons)
        {
            Assert.True(
                comparison.Passed,
                comparison.FormatDiagnostic() +
                (artifactDirectory is null ? string.Empty : $" Artifacts: {artifactDirectory}"));
        }

        if (run.Capabilities.CanUseIndirectDrawCommands)
        {
            Assert.Contains(run.Comparisons, item => item.ActualName == "indirect-per-command");
        }

        if (run.Capabilities.CanUseMultiDrawIndirectGroups)
        {
            Assert.Contains(run.Comparisons, item => item.ActualName == "multi-draw-indirect");
        }

        if (run.Capabilities.CanUseGpuCompactedDrawSubmission)
        {
            Assert.Contains(run.Comparisons, item => item.ActualName == "gpu-compacted-indirect-count");
        }

        if (run.Capabilities.CanUseMaterialTextureArrays)
        {
            Assert.Contains(run.Comparisons, item =>
                item.ExpectedName == "legacy-material-samplers" && item.ActualName == "material-texture-array");
        }
    }

    private static GlPixelHarnessRun RunPixelMatrix(GL gl, string version, List<string> diagnostics)
    {
        var capabilities = PreviewGlCapabilities.FromGl(gl, useOpenGlEs: false, version);
        diagnostics.Add(capabilities.FormatDiagnostic());
        using var target = new GlPixelRenderHarness(gl, Width, Height);
        using var mesh = CreateSubmissionFixtureMesh(gl, out var batches);
        using var commands = new GlIndirectDrawCommandBuffer(gl);
        Require(commands.Upload(batches), "Unable to upload pixel-harness indirect commands.");
        using var drawProgram = CreateProgram(gl, SubmissionVertexSource, SubmissionFragmentSource, "submission fixture");

        var snapshots = new List<GlPixelSnapshot>();
        var baseline = target.Capture("direct-draw-range", _ =>
        {
            drawProgram.Use();
            for (var i = 0; i < batches.Length; i++)
            {
                mesh.DrawRange(batches[i].FirstIndex, batches[i].IndexCount, keepBound: true);
            }

            mesh.UnbindVertexArray();
        });
        snapshots.Add(baseline);

        if (capabilities.CanUseIndirectDrawCommands)
        {
            snapshots.Add(target.Capture("indirect-per-command", _ =>
            {
                drawProgram.Use();
                for (var i = 0; i < batches.Length; i++)
                {
                    mesh.DrawIndirect(commands, i, keepBound: true);
                }

                mesh.UnbindVertexArray();
            }));
        }
        else
        {
            diagnostics.Add("Pixel harness skipped per-command indirect submission: capability unavailable.");
        }

        if (capabilities.CanUseMultiDrawIndirectGroups)
        {
            snapshots.Add(target.Capture("multi-draw-indirect", _ =>
            {
                drawProgram.Use();
                mesh.MultiDrawIndirect(commands, 0, commands.CommandCount);
            }));
        }
        else
        {
            diagnostics.Add("Pixel harness skipped grouped multi-draw submission: capability unavailable.");
        }

        if (capabilities.CanUseGpuCompactedDrawSubmission)
        {
            var shaderContext = new GlShaderCompileContext(
                gl,
                useOpenGlEs: false,
                capabilities.Vendor,
                capabilities.Renderer);
            using var compactionProgram = shaderContext.CreateComputeProgram(
                "genesis_indirect_compact.comp",
                out var compactionError,
                "pixel-harness-indirect-compact");
            Require(compactionProgram.IsValid, "Pixel harness compaction shader failed: " + compactionError);
            using var compactor = new GlGpuDrawCommandCompactor(gl);
            snapshots.Add(target.Capture("gpu-compacted-indirect-count", _ =>
            {
                Require(
                    compactor.Dispatch(compactionProgram, commands, [1u, 1u, 1u, 1u]),
                    "Pixel harness compaction dispatch failed.");
                drawProgram.Use();
                Require(
                    mesh.MultiDrawIndirectCount(
                        compactor.OutputCommands,
                        compactor.CounterBufferHandle,
                        commands.CommandCount),
                    "Pixel harness indirect-count entry point was unavailable.");
            }));
        }
        else
        {
            diagnostics.Add("Pixel harness skipped GPU-compacted indirect-count submission: capability unavailable.");
        }

        if (capabilities.CanUseMaterialTextureArrays)
        {
            AddMaterialTextureParitySnapshots(gl, target, snapshots);
        }
        else
        {
            diagnostics.Add("Pixel harness skipped material texture-array parity: capability unavailable.");
        }

        var comparisons = new List<GlPixelComparison>();
        foreach (var snapshot in snapshots)
        {
            if (snapshot != baseline && !snapshot.Name.StartsWith("material-", StringComparison.Ordinal) &&
                snapshot.Name != "legacy-material-samplers")
            {
                comparisons.Add(baseline.CompareTo(snapshot, GlPixelComparisonOptions.Exact));
            }
        }

        var legacyMaterials = snapshots.FirstOrDefault(item => item.Name == "legacy-material-samplers");
        var arrayMaterials = snapshots.FirstOrDefault(item => item.Name == "material-texture-array");
        if (legacyMaterials is not null && arrayMaterials is not null)
        {
            comparisons.Add(legacyMaterials.CompareTo(arrayMaterials, GlPixelComparisonOptions.Exact));
        }

        diagnostics.AddRange(comparisons.Select(item => item.FormatDiagnostic()));
        return new GlPixelHarnessRun(
            DateTimeOffset.UtcNow,
            version,
            capabilities,
            baseline,
            snapshots,
            comparisons,
            diagnostics.ToArray());
    }

    private static GlMeshBuffer CreateSubmissionFixtureMesh(GL gl, out PreviewDrawBatch[] batches)
    {
        var vertices = new List<float>();
        var indices = new List<uint>();
        var rectangles = new (float Left, float Bottom, float Right, float Top)[]
        {
            (-0.92f, -0.88f, -0.08f, -0.08f),
            ( 0.08f, -0.88f,  0.92f, -0.08f),
            (-0.92f,  0.08f, -0.08f,  0.88f),
            ( 0.08f,  0.08f,  0.92f,  0.88f),
        };
        batches = new PreviewDrawBatch[rectangles.Length];
        for (var rectangleIndex = 0; rectangleIndex < rectangles.Length; rectangleIndex++)
        {
            var rectangle = rectangles[rectangleIndex];
            var firstVertex = (uint)(vertices.Count / 12);
            AddVertex(vertices, rectangle.Left, rectangle.Bottom, 0, 0);
            AddVertex(vertices, rectangle.Right, rectangle.Bottom, 1, 0);
            AddVertex(vertices, rectangle.Right, rectangle.Top, 1, 1);
            AddVertex(vertices, rectangle.Left, rectangle.Top, 0, 1);
            var firstIndex = indices.Count;
            indices.AddRange(
            [
                firstVertex, firstVertex + 1, firstVertex + 2,
                firstVertex, firstVertex + 2, firstVertex + 3,
            ]);
            batches[rectangleIndex] = new PreviewDrawBatch(firstIndex, 6, 0);
        }

        var mesh = new GlMeshBuffer(gl);
        mesh.Upload(vertices.ToArray(), indices.ToArray());
        return mesh;
    }

    private static void AddVertex(List<float> vertices, float x, float y, float u, float v)
    {
        vertices.AddRange(
        [
            x, y, 0,
            0, 0, 1,
            u, v,
            1, 0, 0, 1,
        ]);
    }

    private static void AddMaterialTextureParitySnapshots(
        GL gl,
        GlPixelRenderHarness target,
        List<GlPixelSnapshot> snapshots)
    {
        byte[][] layers =
        [
            [240, 40, 30, 255, 180, 20, 10, 255, 255, 100, 30, 255, 210, 60, 20, 255],
            [20, 220, 70, 255, 10, 150, 40, 255, 90, 255, 120, 255, 40, 190, 80, 255],
            [30, 80, 240, 255, 20, 40, 170, 255, 100, 150, 255, 255, 60, 100, 220, 255],
            [230, 200, 40, 255, 160, 120, 20, 255, 255, 240, 100, 255, 200, 170, 60, 255],
        ];

        using var quad = CreateFullscreenQuadMesh(gl);
        using var legacyProgram = CreateProgram(gl, TextureVertexSource, LegacyTextureFragmentSource, "legacy material fixture");
        using var arrayProgram = CreateProgram(gl, TextureVertexSource, ArrayTextureFragmentSource, "array material fixture");
        var legacyTextures = layers.Select(layer =>
        {
            var texture = new GlTexture2D(gl);
            texture.UploadRgba(2, 2, layer, nearestFilter: true);
            return texture;
        }).ToArray();
        using var arrayTexture = new GlTexture2DArray(gl);
        try
        {
            var allLayers = layers.SelectMany(layer => layer).ToArray();
            Require(arrayTexture.UploadRgbaIfChanged(2, 2, layers.Length, allLayers, nearest: true),
                "Pixel harness texture-array upload failed.");

            snapshots.Add(target.Capture("legacy-material-samplers", _ =>
            {
                legacyProgram.Use();
                for (var unit = 0; unit < legacyTextures.Length; unit++)
                {
                    legacyTextures[unit].Bind((uint)unit);
                    var location = legacyProgram.GetUniformLocation("uTexture" + unit);
                    Require(location >= 0, "Legacy material sampler uniform was optimized out.");
                    gl.Uniform1(location, unit);
                }

                quad.Draw();
            }));

            snapshots.Add(target.Capture("material-texture-array", _ =>
            {
                arrayProgram.Use();
                arrayTexture.Bind(0);
                var location = arrayProgram.GetUniformLocation("uTextures");
                Require(location >= 0, "Material texture-array uniform was optimized out.");
                gl.Uniform1(location, 0);
                quad.Draw();
            }));
        }
        finally
        {
            foreach (var texture in legacyTextures)
            {
                texture.Dispose();
            }
        }
    }

    private static GlMeshBuffer CreateFullscreenQuadMesh(GL gl)
    {
        float[] vertices =
        [
            -1, -1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
             1, -1, 0, 0, 0, 1, 1, 0, 1, 0, 0, 1,
             1,  1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 1,
            -1,  1, 0, 0, 0, 1, 0, 1, 1, 0, 0, 1,
        ];
        var mesh = new GlMeshBuffer(gl);
        mesh.Upload(vertices, [0, 1, 2, 0, 2, 3]);
        return mesh;
    }

    private static GlShaderProgram CreateProgram(GL gl, string vertexSource, string fragmentSource, string label)
    {
        var vertex = CompileShader(gl, ShaderType.VertexShader, vertexSource, label);
        var fragment = CompileShader(gl, ShaderType.FragmentShader, fragmentSource, label);
        var program = gl.CreateProgram();
        try
        {
            gl.AttachShader(program, vertex);
            gl.AttachShader(program, fragment);
            gl.LinkProgram(program);
            gl.GetProgram(program, GLEnum.LinkStatus, out var linked);
            if (linked == 0)
            {
                throw new InvalidOperationException($"Pixel harness {label} link failed: {gl.GetProgramInfoLog(program)}");
            }

            return new GlShaderProgram(gl, program);
        }
        catch
        {
            gl.DeleteProgram(program);
            throw;
        }
        finally
        {
            gl.DeleteShader(vertex);
            gl.DeleteShader(fragment);
        }
    }

    private static uint CompileShader(GL gl, ShaderType type, string source, string label)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        if (compiled == 0)
        {
            var error = gl.GetShaderInfoLog(shader);
            gl.DeleteShader(shader);
            throw new InvalidOperationException($"Pixel harness {label} {type} compile failed: {error}");
        }

        return shader;
    }

    private static void WriteArtifacts(GlPixelHarnessRun run, string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var snapshot in run.Snapshots)
        {
            WritePng(snapshot, Path.Combine(directory, Sanitize(snapshot.Name) + ".png"));
        }

        foreach (var comparison in run.Comparisons.Where(item => !item.Passed))
        {
            var expected = run.Snapshots.Single(item => item.Name == comparison.ExpectedName);
            var actual = run.Snapshots.Single(item => item.Name == comparison.ActualName);
            var diff = new GlPixelSnapshot(
                comparison.ExpectedName + "-vs-" + comparison.ActualName + "-diff",
                expected.Width,
                expected.Height,
                expected.CreateDifferenceRgba(actual));
            WritePng(diff, Path.Combine(directory, Sanitize(diff.Name) + ".png"));
        }

        var report = new
        {
            schemaVersion = 1,
            run.TimestampUtc,
            run.VersionString,
            capabilities = run.Capabilities.FormatDiagnostic(),
            snapshots = run.Snapshots.Select(item => new
            {
                item.Name,
                item.Width,
                item.Height,
                fingerprint = item.Fingerprint.ToString("X8", CultureInfo.InvariantCulture),
            }),
            comparisons = run.Comparisons.Select(item => new
            {
                item.ExpectedName,
                item.ActualName,
                item.Passed,
                item.DifferentPixels,
                item.DifferentChannels,
                item.MaximumChannelDifference,
                item.MeanAbsoluteError,
                item.RootMeanSquareError,
                item.MismatchBounds,
                diagnostic = item.FormatDiagnostic(),
            }),
            run.Diagnostics,
        };
        File.WriteAllText(
            Path.Combine(directory, "pixel-harness-report.json"),
            JsonSerializer.Serialize(report, ArtifactJsonOptions));
    }

    private static void WritePng(GlPixelSnapshot snapshot, string path)
    {
        using var image = Image.LoadPixelData<Rgba32>(snapshot.Rgba.Span, snapshot.Width, snapshot.Height);
        image.Save(path);
    }

    private static string? ResolveArtifactDirectory(GlPixelHarnessRun run)
    {
        var configured = Environment.GetEnvironmentVariable(ArtifactDirectoryEnv);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.IsPathRooted(configured)
                ? Path.GetFullPath(configured)
                : Path.GetFullPath(Path.Combine(FindRepoRoot(), configured));
        }

        return run.Comparisons.Any(item => !item.Passed)
            ? Path.Combine(
                Path.GetTempPath(),
                "AutoPBR-pixel-harness-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture))
            : null;
    }

    private static string FindRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
    {
        foreach (var start in new[]
                 {
                     Path.GetDirectoryName(sourceFilePath),
                     AppContext.BaseDirectory,
                     Directory.GetCurrentDirectory(),
                 })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AutoPBR.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static string Sanitize(string name) =>
        string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    private static bool IsEnabled()
    {
        return IsTruthy(Environment.GetEnvironmentVariable(EnableHarnessEnv)) ||
               IsTruthy(Environment.GetEnvironmentVariable(EnableLiveSmokeEnv));
    }

    private static bool IsTruthy(string? value) =>
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed record GlPixelHarnessRun(
        DateTimeOffset TimestampUtc,
        string VersionString,
        PreviewGlCapabilities Capabilities,
        GlPixelSnapshot Baseline,
        IReadOnlyList<GlPixelSnapshot> Snapshots,
        IReadOnlyList<GlPixelComparison> Comparisons,
        string[] Diagnostics);

    private const string SubmissionVertexSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 2) in vec2 aUv;
        out vec2 vPosition;
        out vec2 vUv;
        void main()
        {
            vPosition = aPosition.xy;
            vUv = aUv;
            gl_Position = vec4(aPosition, 1.0);
        }
        """;

    private const string SubmissionFragmentSource = """
        #version 330 core
        in vec2 vPosition;
        in vec2 vUv;
        layout(location = 0) out vec4 outColor;
        void main()
        {
            vec3 baseColor;
            if (vPosition.x < 0.0 && vPosition.y < 0.0)
                baseColor = vec3(0.92, 0.18, 0.10);
            else if (vPosition.x >= 0.0 && vPosition.y < 0.0)
                baseColor = vec3(0.12, 0.82, 0.28);
            else if (vPosition.x < 0.0)
                baseColor = vec3(0.12, 0.35, 0.94);
            else
                baseColor = vec3(0.93, 0.76, 0.12);
            float checker = mod(floor(vUv.x * 8.0) + floor(vUv.y * 8.0), 2.0);
            outColor = vec4(baseColor * mix(0.72, 1.0, checker), 1.0);
        }
        """;

    private const string TextureVertexSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 2) in vec2 aUv;
        out vec2 vUv;
        void main()
        {
            vUv = aUv;
            gl_Position = vec4(aPosition, 1.0);
        }
        """;

    private const string LegacyTextureFragmentSource = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uTexture0;
        uniform sampler2D uTexture1;
        uniform sampler2D uTexture2;
        uniform sampler2D uTexture3;
        layout(location = 0) out vec4 outColor;
        void main()
        {
            int layer = (vUv.x >= 0.5 ? 1 : 0) + (vUv.y >= 0.5 ? 2 : 0);
            vec2 localUv = fract(vUv * 2.0);
            if (layer == 0) outColor = texture(uTexture0, localUv);
            else if (layer == 1) outColor = texture(uTexture1, localUv);
            else if (layer == 2) outColor = texture(uTexture2, localUv);
            else outColor = texture(uTexture3, localUv);
        }
        """;

    private const string ArrayTextureFragmentSource = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2DArray uTextures;
        layout(location = 0) out vec4 outColor;
        void main()
        {
            int layer = (vUv.x >= 0.5 ? 1 : 0) + (vUv.y >= 0.5 ? 2 : 0);
            vec2 localUv = fract(vUv * 2.0);
            outColor = texture(uTextures, vec3(localUv, float(layer)));
        }
        """;
}
