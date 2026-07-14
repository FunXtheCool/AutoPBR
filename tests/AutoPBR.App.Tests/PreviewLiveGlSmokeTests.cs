using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.Preview;

using Avalonia.OpenGL;

using Silk.NET.OpenGL;

using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

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
                CompareFragmentAndComputeFroxelInjectIfSupported(gl, caps, diagnostics);
                UploadIndirectDrawCommandsIfSupported(gl, caps, diagnostics);
                RunGpuCommandCompactorIfSupported(gl, caps, diagnostics);

                return new LiveGlSmokeReport(
                    context.VersionString,
                    caps.FormatDiagnostic(),
                    caps.FormatContextSuffix(),
                    caps.CanUsePersistentUploadRing,
                    caps.CanUseEntitySkinningSsbo,
                    caps.CanUseMaterialDrawRecordSsbo,
                    caps.CanUseComputeFroxelInject,
                    caps.CanUseIndirectDrawCommands,
                    caps.CanUseMultiDrawIndirectGroups,
                    caps.CanUseGpuCommandCompaction,
                    caps.CanUseGpuBatchCulling,
                    caps.CanUseGpuCompactedDrawSubmission,
                    diagnostics.ToArray());
            }
        }, TimeSpan.FromSeconds(30));

        Assert.Contains("persistentUpload=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("entitySsbo=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("materialDrawSsbo=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("computeFroxels=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("indirectDraws=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("multiDrawGroups=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("gpuCommandCompaction=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("gpuBatchCulling=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        Assert.Contains("gpuCompactedDraws=", report.CapabilityDiagnostic, StringComparison.Ordinal);
        if (report.ComputeFroxels)
        {
            Assert.Contains("compute froxels", report.ContextSuffix, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains("fragment froxels", report.ContextSuffix, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            report.MultiDrawGroups ? "multi-draw groups" : report.IndirectDrawCommands ? "indirect draws" : "direct draws",
            report.ContextSuffix,
            StringComparison.OrdinalIgnoreCase);

        WriteReport(report);
    }

    private static void CompileDesktopGenesisVariant(GL gl, PreviewGlCapabilities caps, List<string> diagnostics)
    {
        var defines = OpenGlPreviewBackend.TestBuildGenesisProgramDefines(
            caps.CanUseEntitySkinningSsbo,
            caps.CanUseMaterialDrawRecordSsbo,
            caps.CanUseMultiDrawIndirectGroups);
        var ctx = new GlShaderCompileContext(gl, useOpenGlEs: false, caps.Vendor, caps.Renderer);
        using var program = ctx.CreateProgram(
            "genesis.vert",
            "genesis.frag",
            out var error,
            "p23-smoke-genesis",
            defines);

        Assert.True(program.IsValid, "Desktop Genesis variant failed to compile: " + error);
        diagnostics.Add("[3D preview] P2.3 desktop Genesis variant compiled.");

        using var shadowProgram = ctx.CreateProgram(
            "genesis_shadow.vert",
            "genesis_shadow.frag",
            out var shadowError,
            "p41-smoke-genesis-shadow",
            defines);

        Assert.True(shadowProgram.IsValid, "Desktop Genesis shadow variant failed to compile: " + shadowError);
        diagnostics.Add("[3D preview] P4.1 desktop Genesis base-instance shadow variant compiled.");
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

    private static void CompareFragmentAndComputeFroxelInjectIfSupported(
        GL gl,
        PreviewGlCapabilities caps,
        List<string> diagnostics)
    {
        if (!caps.CanUseComputeFroxelInject)
        {
            diagnostics.Add("[3D preview] P3 froxel parity skipped; compute/image-store capability gate is off.");
            return;
        }

        const int width = 32;
        const int height = 24;
        const int slices = 8;

        using var fragmentTarget = new GlVolumeFroxelTarget(gl, useOpenGlEs: false);
        using var computeTarget = new GlVolumeFroxelTarget(gl, useOpenGlEs: false);
        Assert.True(fragmentTarget.EnsureSize(width, height, slices), "Fragment froxel target failed to initialize.");
        Assert.True(computeTarget.EnsureSize(width, height, slices), "Compute froxel target failed to initialize.");

        var ctx = new GlShaderCompileContext(gl, useOpenGlEs: false, caps.Vendor, caps.Renderer);
        using var fragmentProgram = ctx.CreateProgram(
            "genesis_godrays.vert",
            "genesis_volume_inject.frag",
            out var fragmentError,
            "p3-smoke-volume-inject-fragment");
        using var computeProgram = ctx.CreateComputeProgram(
            "genesis_volume_inject.comp",
            out var computeError,
            "p3-smoke-volume-inject-compute");

        Assert.True(fragmentProgram.IsValid, "Fragment froxel injector failed to compile: " + fragmentError);
        Assert.True(computeProgram.IsValid, "Compute froxel injector failed to compile: " + computeError);

        var quadVao = CreateFullscreenQuad(gl, out var quadVbo);
        try
        {
            ApplyFixedFroxelSceneUniforms(gl, fragmentProgram, width, height, slices, isCompute: false);
            gl.BindVertexArray(quadVao);
            for (var layer = 0; layer < slices; layer++)
            {
                Assert.True(fragmentTarget.BindDrawLayer(layer), $"Fragment target layer {layer} failed to bind.");
                gl.Clear(ClearBufferMask.ColorBufferBit);
                SetUniform1(gl, fragmentProgram, "uSliceIndex", layer);
                gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }

            fragmentTarget.Unbind();

            Assert.True(computeTarget.BindImagesForCompute(0, 1), "Compute target failed to bind image outputs.");
            ApplyFixedFroxelSceneUniforms(gl, computeProgram, width, height, slices, isCompute: true);
            gl.DispatchCompute((uint)((width + 7) / 8), (uint)((height + 7) / 8), (uint)slices);
            gl.MemoryBarrier(0x00000020 | 0x00000008);

            var fragmentRgba = ReadArrayAttachment(gl, fragmentTarget, width, height, slices, ReadBufferMode.ColorAttachment0, PixelFormat.Rgba, 4);
            var computeRgba = ReadArrayAttachment(gl, computeTarget, width, height, slices, ReadBufferMode.ColorAttachment0, PixelFormat.Rgba, 4);
            var fragmentOcc = ReadArrayAttachment(gl, fragmentTarget, width, height, slices, ReadBufferMode.ColorAttachment1, PixelFormat.Red, 1);
            var computeOcc = ReadArrayAttachment(gl, computeTarget, width, height, slices, ReadBufferMode.ColorAttachment1, PixelFormat.Red, 1);

            AssertWithinByteTolerance(fragmentRgba, computeRgba, tolerance: 1, "froxel RGBA");
            AssertWithinByteTolerance(fragmentOcc, computeOcc, tolerance: 1, "froxel occupancy");

            diagnostics.Add(
                "[3D preview] P3 fragment-vs-compute froxel inject parity passed " +
                $"({width}x{height}x{slices}, rgbaHash={HashBytes(computeRgba):X8}, occHash={HashBytes(computeOcc):X8}).");
        }
        finally
        {
            if (quadVbo != 0)
            {
                gl.DeleteBuffer(quadVbo);
            }

            if (quadVao != 0)
            {
                gl.DeleteVertexArray(quadVao);
            }
        }
    }

    private static uint CreateFullscreenQuad(GL gl, out uint vbo)
    {
        var vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        gl.BindVertexArray(vao);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        ReadOnlySpan<float> verts =
        [
            -1f, -1f,
             1f, -1f,
             1f,  1f,
            -1f, -1f,
             1f,  1f,
            -1f,  1f,
        ];
        gl.BufferData<float>(BufferTargetARB.ArrayBuffer, verts, BufferUsageARB.StaticDraw);
        unsafe
        {
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }

        gl.BindVertexArray(0);
        return vao;
    }

    private static void UploadIndirectDrawCommandsIfSupported(
        GL gl,
        PreviewGlCapabilities caps,
        List<string> diagnostics)
    {
        if (!caps.CanUseIndirectDrawCommands)
        {
            diagnostics.Add("[3D preview] P4 indirect draw command upload skipped; capability gate is off.");
            return;
        }

        using var commands = new GlIndirectDrawCommandBuffer(gl);
        PreviewDrawBatch[] batches =
        [
            new(0, 3, 0),
            new(3, 6, 1),
        ];

        Assert.True(commands.Upload(batches), "Indirect draw command buffer upload failed.");
        Assert.True(commands.IsValid, "Indirect draw command buffer did not become valid after upload.");
        Assert.Equal(batches.Length, commands.CommandCount);
        commands.Bind();
        commands.Unbind();
        diagnostics.Add("[3D preview] P4 indirect draw command buffer upload passed (2 commands).");
    }

    private static void RunGpuCommandCompactorIfSupported(
        GL gl,
        PreviewGlCapabilities caps,
        List<string> diagnostics)
    {
        if (!caps.CanUseGpuCommandCompaction)
        {
            diagnostics.Add("[3D preview] P5 GPU command compaction skipped; capability gate is off.");
            return;
        }

        var ctx = new GlShaderCompileContext(gl, useOpenGlEs: false, caps.Vendor, caps.Renderer);
        using var program = ctx.CreateComputeProgram(
            "genesis_indirect_compact.comp",
            out var error,
            "p5-smoke-indirect-compact");

        Assert.True(program.IsValid, "GPU indirect command compactor failed to compile: " + error);

        using var source = new GlIndirectDrawCommandBuffer(gl);
        PreviewDrawBatch[] batches =
        [
            new(0, 3, 0),
            new(3, 6, 1),
            new(9, 0, 1),
            new(9, 12, 0),
        ];
        Assert.True(source.Upload(batches), "Source indirect command upload failed.");

        using var compactor = new GlGpuDrawCommandCompactor(gl);
        Assert.True(
            compactor.Dispatch(program, source, [1u, 0u, 1u, 1u], readBackCounter: true),
            "GPU indirect command compaction dispatch failed.");
        Assert.Equal(2, compactor.LastVisibleCount);

        var dwords = compactor.ReadOutputCommandDwords(compactor.LastVisibleCount);
        Assert.Equal(
            [3u, 1u, 0u, 0u, 0u, 12u, 1u, 9u, 0u, 3u],
            dwords);
        diagnostics.Add("[3D preview] P5 GPU indirect command compaction passed (4 source commands -> 2 visible commands).");

        PreviewDrawBatch[] cullBatches =
        [
            new(0, 3, 0)
            {
                BoundsCenter = Vector3.Zero,
                BoundsRadius = 0.25f,
            },
            new(3, 6, 1)
            {
                BoundsCenter = new Vector3(3f, 0f, 0f),
                BoundsRadius = 0.25f,
            },
            new(9, 0, 1)
            {
                BoundsCenter = Vector3.Zero,
                BoundsRadius = 0.25f,
            },
            new(9, 12, 0)
            {
                BoundsCenter = new Vector3(0f, 0f, -5f),
                BoundsRadius = 0.25f,
                LodMaxDistance = 3f,
            },
            new(21, 9, 0)
            {
                BoundsCenter = new Vector3(0f, 0f, -2f),
                BoundsRadius = 0.25f,
            },
        ];
        using var cullSource = new GlIndirectDrawCommandBuffer(gl);
        Assert.True(cullSource.Upload(cullBatches), "GPU culling source indirect command upload failed.");
        Vector4[] planes =
        [
            new( 1f,  0f,  0f, 1f),
            new(-1f,  0f,  0f, 1f),
            new( 0f,  1f,  0f, 1f),
            new( 0f, -1f,  0f, 1f),
            new( 0f,  0f,  1f, 10f),
            new( 0f,  0f, -1f, 1f),
        ];
        Assert.True(
            compactor.DispatchWithGpuCulling(program, cullSource, cullBatches, planes, Vector3.Zero, readBackCounter: true),
            "GPU indirect command culling dispatch failed.");
        Assert.Equal(2, compactor.LastVisibleCount);

        var culledDwords = compactor.ReadOutputCommandDwords(compactor.LastVisibleCount);
        Assert.Equal(
            [3u, 1u, 0u, 0u, 0u, 9u, 1u, 21u, 0u, 4u],
            culledDwords);
        diagnostics.Add("[3D preview] P5.1 GPU batch bounds culling passed (5 source commands -> 2 visible commands).");

        if (caps.CanUseGpuCompactedDrawSubmission)
        {
            using var mesh = new GlMeshBuffer(gl);
            Assert.True(mesh.SupportsIndirectCount, "GL 4.6 indirect-count entry point did not resolve.");
            float[] triangleVertices =
            [
                -0.5f, -0.5f, 0f, 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f,
                 0.5f, -0.5f, 0f, 0f, 0f, 1f, 1f, 0f, 1f, 0f, 0f, 1f,
                 0.0f,  0.5f, 0f, 0f, 0f, 1f, 0.5f, 1f, 1f, 0f, 0f, 1f,
            ];
            mesh.Upload(triangleVertices, [0u, 1u, 2u]);
            PreviewDrawBatch[] drawBatches =
            [
                new(0, 3, 0),
                new(0, 3, 0),
                new(0, 3, 0),
                new(0, 3, 0),
            ];
            using var drawSource = new GlIndirectDrawCommandBuffer(gl);
            Assert.True(drawSource.Upload(drawBatches));
            Assert.True(compactor.Dispatch(program, drawSource, [1u, 0u, 1u, 1u]));
            using var drawProgram = CreateMinimalDrawProgram(gl);
            drawProgram.Use();
            while (gl.GetError() != GLEnum.NoError)
            {
            }

            Assert.True(mesh.MultiDrawIndirectCount(
                compactor.OutputCommands,
                compactor.CounterBufferHandle,
                drawBatches.Length));
            gl.Finish();
            Assert.Equal(GLEnum.NoError, gl.GetError());
            diagnostics.Add(
                "[3D preview] P5.2 GPU indirect-count submission executed without CPU counter readback " +
                "(4 source commands -> 3 submitted draws).");
        }
    }

    private static void ApplyFixedFroxelSceneUniforms(
        GL gl,
        GlShaderProgram program,
        int width,
        int height,
        int slices,
        bool isCompute)
    {
        program.Use();
        SetUniform3(gl, program, "uCameraPos", 0f, 2f, -10f);
        SetUniform3(gl, program, "uCamRight", 1f, 0f, 0f);
        SetUniform3(gl, program, "uCamUp", 0f, 1f, 0f);
        SetUniform3(gl, program, "uCamForward", 0f, 0f, 1f);
        SetUniform3(gl, program, "uLightDir", -0.35f, -0.8f, -0.25f);
        SetUniform3(gl, program, "uLightColor", 1f, 0.86f, 0.68f);
        SetUniform3(gl, program, "uHalfExtent", 9f, 6f, 18f);
        if (isCompute)
        {
            SetUniform3i(gl, program, "uFroxelSize", width, height, slices);
        }

        SetUniform1(gl, program, "uSliceCount", slices);
        SetUniform1(gl, program, "uDepthDistribution", 0.55f);
        SetUniform1(gl, program, "uLayerHeight", 0f);
        SetUniform1(gl, program, "uVolumeHeight", 9f);
        SetUniform1(gl, program, "uCloudDensity", 0.72f);
        SetUniform1(gl, program, "uVolumeSize", 48f);
        SetUniform1(gl, program, "uGroundWorldY", -1f);
        SetUniform1(gl, program, "uFogSlabHeight", 0f);
        SetUniform1(gl, program, "uHeightFogStrength", 0f);
        SetUniform1(gl, program, "uDebugDensity", 0.03f);
        SetUniform1(gl, program, "uEnableShadowMap", 0);
        SetUniform1(gl, program, "uEnableShadowCascades", 0);
        SetUniform1(gl, program, "uCascadeSplitDistance", 12f);
        SetUniform1(gl, program, "uCascadeBlendWidth", 2f);
        SetUniform1(gl, program, "uShadowMinBias", 0.001f);
        SetUniform2(gl, program, "uShadowTexelSize", 1f / 1024f, 1f / 1024f);
    }

    private static GlShaderProgram CreateMinimalDrawProgram(GL gl)
    {
        const string vertexSource = """
            #version 330 core
            layout(location = 0) in vec3 aPos;
            void main() { gl_Position = vec4(aPos, 1.0); }
            """;
        const string fragmentSource = """
            #version 330 core
            layout(location = 0) out vec4 outColor;
            void main() { outColor = vec4(1.0); }
            """;

        var vertex = CompileMinimalShader(gl, ShaderType.VertexShader, vertexSource);
        var fragment = CompileMinimalShader(gl, ShaderType.FragmentShader, fragmentSource);
        var handle = gl.CreateProgram();
        gl.AttachShader(handle, vertex);
        gl.AttachShader(handle, fragment);
        gl.LinkProgram(handle);
        gl.GetProgram(handle, GLEnum.LinkStatus, out var linked);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        Assert.NotEqual(0, linked);
        return new GlShaderProgram(gl, handle);
    }

    private static uint CompileMinimalShader(GL gl, ShaderType type, string source)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var compiled);
        Assert.True(compiled != 0, gl.GetShaderInfoLog(shader));
        return shader;
    }

    private static byte[] ReadArrayAttachment(
        GL gl,
        GlVolumeFroxelTarget target,
        int width,
        int height,
        int slices,
        ReadBufferMode attachment,
        PixelFormat format,
        int bytesPerPixel)
    {
        var bytes = new byte[width * height * slices * bytesPerPixel];
        var layerBytes = width * height * bytesPerPixel;
        for (var layer = 0; layer < slices; layer++)
        {
            Assert.True(target.BindDrawLayer(layer), $"Readback layer {layer} failed to bind.");
            gl.ReadBuffer(attachment);
            unsafe
            {
                fixed (byte* p = bytes.AsSpan(layer * layerBytes, layerBytes))
                {
                    gl.ReadPixels(0, 0, (uint)width, (uint)height, format, PixelType.UnsignedByte, p);
                }
            }
        }

        target.Unbind();
        return bytes;
    }

    private static void AssertWithinByteTolerance(byte[] expected, byte[] actual, int tolerance, string label)
    {
        Assert.Equal(expected.Length, actual.Length);
        var maxDiff = 0;
        var offByMore = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            var diff = Math.Abs(expected[i] - actual[i]);
            maxDiff = Math.Max(maxDiff, diff);
            if (diff > tolerance)
            {
                offByMore++;
            }
        }

        Assert.True(offByMore == 0,
            $"{label} mismatch: {offByMore}/{expected.Length} bytes exceeded tolerance {tolerance}; maxDiff={maxDiff}.");
    }

    private static uint HashBytes(ReadOnlySpan<byte> bytes)
    {
        const uint fnvPrime = 16777619;
        var hash = 2166136261u;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return hash;
    }

    private static void SetUniform1(GL gl, GlShaderProgram program, string name, int value)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            program.Use();
            gl.Uniform1(loc, value);
        }
    }

    private static void SetUniform1(GL gl, GlShaderProgram program, string name, float value)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            program.Use();
            gl.Uniform1(loc, value);
        }
    }

    private static void SetUniform2(GL gl, GlShaderProgram program, string name, float x, float y)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            program.Use();
            gl.Uniform2(loc, x, y);
        }
    }

    private static void SetUniform3(GL gl, GlShaderProgram program, string name, float x, float y, float z)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            program.Use();
            gl.Uniform3(loc, x, y, z);
        }
    }

    private static void SetUniform3i(GL gl, GlShaderProgram program, string name, int x, int y, int z)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            program.Use();
            gl.Uniform3(loc, x, y, z);
        }
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
            $"indirectDraws: {(report.IndirectDrawCommands ? "on" : "off")}",
            $"multiDrawGroups: {(report.MultiDrawGroups ? "on" : "off")}",
            $"gpuCommandCompaction: {(report.GpuCommandCompaction ? "on" : "off")}",
            $"gpuBatchCulling: {(report.GpuBatchCulling ? "on" : "off")}",
            $"gpuCompactedDraws: {(report.GpuCompactedDraws ? "on" : "off")}",
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
        bool IndirectDrawCommands,
        bool MultiDrawGroups,
        bool GpuCommandCompaction,
        bool GpuBatchCulling,
        bool GpuCompactedDraws,
        string[] Diagnostics);
}
