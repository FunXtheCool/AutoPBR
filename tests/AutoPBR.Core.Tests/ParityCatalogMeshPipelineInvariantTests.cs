using AutoPBR.Preview;
using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Catalog-wide preview pipeline invariants: production geometry IR emit is deterministic and CPU/GPU bind bakes agree.
/// Object-entity preview orientation and promoted pilot bind alignment lives in
/// <see cref="ObjectEntityBlockStateParityTests"/> and <see cref="GeometryIrReferenceRigTests"/>.
/// </summary>
public sealed class ParityCatalogMeshPipelineInvariantTests(ITestOutputHelper output)
{
    private static readonly MinecraftNativeProfile Profile26 = ParityCatalogMeshPipelineSurvey.DefaultProfile26();

    [Fact]
    public void Catalog_runtime_geometry_ir_emit_is_deterministic_at_bind_pose()
    {
        GeometryIrParityPolicy.ResetForTests();
        var paths = EntityTextureParityCatalog.GetCataloguedDiffusePathsWithManifestRules();
        var failures = new List<string>();
        foreach (var path in paths)
        {
            if (!ParityCatalogMeshPipelineSurvey.TryAssertRuntimeDeterminism(
                    path,
                    Profile26,
                    idlePhase01: 0f,
                    animationTimeSeconds: 0f,
                    applyGeometryIrSetupAnimMotion: false,
                    out var failure))
            {
                failures.Add($"{path}: {failure}");
            }
        }

        output.WriteLine($"Determinism failures: {failures.Count} / {paths.Count}");
        foreach (var f in failures.Take(20))
        {
            output.WriteLine($"  {f}");
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Catalog_bind_pose_cpu_bake_matches_gpu_bind_pose_vertices()
    {
        GeometryIrParityPolicy.ResetForTests();
        var paths = EntityTextureParityCatalog.GetCataloguedDiffusePathsWithManifestRules();
        var failures = new List<string>();
        foreach (var path in paths)
        {
            var result = ParityCatalogMeshPipelineSurvey.MeasureCpuGpuBindParity(path, Profile26);
            if (!result.Succeeded)
            {
                failures.Add($"{path}: {result.Failure} (maxDelta={result.MaxVertexDelta:G6})");
            }
        }

        output.WriteLine($"CPU/GPU bind failures: {failures.Count} / {paths.Count}");
        foreach (var f in failures.Take(20))
        {
            output.WriteLine($"  {f}");
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Catalog_runtime_mesh_matches_production_geometry_ir_builder_hook()
    {
        GeometryIrParityPolicy.ResetForTests();
        var runtime = EntityModelRuntimeFactory.Create();
        var paths = EntityTextureParityCatalog.GetCataloguedDiffusePathsWithManifestRules();
        var failures = new List<string>();
        foreach (var path in paths)
        {
            if (!runtime.TryBuildStaticMesh(
                    path,
                    Profile26,
                    idlePhase01: 0f,
                    animationTimeSeconds: 0f,
                    out var runtimeMesh,
                    out var provenance,
                    applyGeometryIrSetupAnimMotion: false,
                    pairDoubleChestPreviewHalves: false))
            {
                failures.Add($"{path}: runtime build failed");
                continue;
            }

            if (provenance.Kind != PreviewMeshDriverKind.RuntimeGeometryIrJson)
            {
                continue;
            }

            if (!EntityModelRuntime.TryBuildParityCatalogGeometryIrMeshForTests(
                    path,
                    Profile26,
                    idlePhase01: 0f,
                    animationTimeSeconds: 0f,
                    applyGeometryIrSetupAnimMotion: false,
                    out var hookMesh,
                    out _))
            {
                failures.Add($"{path}: production IR hook failed");
                continue;
            }

            var cmp = GeometryIrMeshParityComparer.Compare(runtimeMesh, hookMesh, tolerance: 1e-5f);
            if (!cmp.IsMatch)
            {
                failures.Add($"{path}: {cmp.Message} (maxDelta={cmp.MaxCornerDelta:G6})");
            }
        }

        output.WriteLine($"Runtime vs IR hook failures: {failures.Count}");
        foreach (var f in failures.Take(20))
        {
            output.WriteLine($"  {f}");
        }

        Assert.Empty(failures);
    }
}
