using System.Text.Json.Nodes;


namespace AutoPBR.GeometryCompiler.Tests;

public sealed class GeometryCompilerPerfTests
{
    private static string? FindRepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "AutoPBR.sln")))
            {
                return d.FullName;
            }

            d = d.Parent;
        }

        return null;
    }

    private static string? FindOptionalClientJar()
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            return null;
        }

        var jar = Path.Combine(root, "tools", "minecraft-parity", "26.1.2", "client.jar");
        return File.Exists(jar) ? jar : null;
    }

    [Fact]
    public void Float_probe_with_precomputed_mesh_does_not_require_javap()
    {
        var mesh = "// float -1.5f\n";
        var ok = JavapMethodFloatProbe.TryRun(null, "", "ignored", "createBodyLayer", null, null, "ignored", mesh, null,
            out var floats, out var err);
        Assert.True(ok, err);
        Assert.Single(floats);
        Assert.Equal(-1.5f, floats[0]);
    }

    [Fact]
    public void Javap_disassembly_cache_records_second_lookup_as_hit()
    {
        JavapClassDisassembly.ClearDisassemblyCacheForTests();
        GeometryCompilerStats.Reset();
        var javap = JavapLocator.FindJavap();
        if (string.IsNullOrWhiteSpace(javap))
        {
            return;
        }

        var tmpJar = Path.Combine(Path.GetTempPath(), $"autopbr-empty-{Guid.NewGuid():n}.jar");
        File.WriteAllText(tmpJar, "");
        try
        {
            const string bogus = "com.autopbr.NotARealGeometryClass";
            Assert.False(JavapClassDisassembly.TryDisassemble(javap, tmpJar, bogus, out _, out _));
            var invAfterFirst = GeometryCompilerStats.JavapSubprocessInvocations;
            Assert.False(JavapClassDisassembly.TryDisassemble(javap, tmpJar, bogus, out _, out _));
            Assert.Equal(invAfterFirst, GeometryCompilerStats.JavapSubprocessInvocations);
            Assert.True(GeometryCompilerStats.DisasmCacheHits > 0);
        }
        finally
        {
            try
            {
                File.Delete(tmpJar);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void Batch_parallel_matches_serial_geometry_index_when_client_jar_present()
    {
        var jar = FindOptionalClientJar();
        if (jar is null)
        {
            return;
        }

        var listPath = Path.Combine(Path.GetTempPath(), $"autopbr-geom-list-{Guid.NewGuid():n}.txt");
        File.WriteAllLines(listPath,
        [
            "net/minecraft/client/model/animal/cow/CowModel.class",
            "net/minecraft/client/model/BlazeModel.class"
        ]);
        var version = $"parallel-test-{Guid.NewGuid():n}";
        var outSerial = Path.Combine(Path.GetTempPath(), $"autopbr-geom-s-{Guid.NewGuid():n}");
        var outParallel = Path.Combine(Path.GetTempPath(), $"autopbr-geom-p-{Guid.NewGuid():n}");
        try
        {
            Directory.CreateDirectory(outSerial);
            Directory.CreateDirectory(outParallel);

            var hostSerial =
                new GeometryCompilerHost(jar, null, version, outSerial, null, quiet: true, emitStats: false);
            Assert.Equal(0, hostSerial.RunBatch(listPath, "createBodyLayer"));
            var indexSerial = File.ReadAllText(Path.Combine(outSerial, $"geometry-index-{version}.json"));

            var hostParallel =
                new GeometryCompilerHost(jar, null, version, outParallel, null, 4, quiet: true, emitStats: false);
            Assert.Equal(0, hostParallel.RunBatch(listPath, "createBodyLayer"));
            var indexParallel = File.ReadAllText(Path.Combine(outParallel, $"geometry-index-{version}.json"));

            Assert.Equal(NormalizeJson(indexSerial), NormalizeJson(indexParallel));

            var cowSerial =
                File.ReadAllText(
                    Path.Combine(outSerial, "geometry", version, "net.minecraft.client.model.animal.cow.CowModel.json"));
            var cowParallel =
                File.ReadAllText(
                    Path.Combine(outParallel, "geometry", version,
                        "net.minecraft.client.model.animal.cow.CowModel.json"));
            Assert.Equal(NormalizeJson(cowSerial), NormalizeJson(cowParallel));
        }
        finally
        {
            try
            {
                File.Delete(listPath);
            }
            catch
            {
                // ignore
            }

            TryDeleteDir(outSerial);
            TryDeleteDir(outParallel);
        }
    }

    private static string NormalizeJson(string json) => JsonNode.Parse(json)!.ToJsonString();

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // ignore
        }
    }
}
