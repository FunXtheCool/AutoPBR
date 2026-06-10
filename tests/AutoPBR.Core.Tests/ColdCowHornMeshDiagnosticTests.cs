using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class ColdCowHornMeshDiagnosticTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

    [Fact]
    public void Dump_cold_cow_horn_head_body_local_to_parent_and_baked_centroids()
    {
        const string jvm = "net.minecraft.client.model.animal.cow.ColdCowModel";
        const string texture = "assets/minecraft/textures/entity/cow/cow_cold.png";

        var runtime = new CleanRoomEntityModelRuntime();
        Assert.True(runtime.TryBuildStaticMesh(texture, Profile26, 0f, 0f, out var mesh, out var prov, applyGeometryIrSetupAnimMotion: false));

        var repo = GeometryIrTestTierSupport.FindRepoRoot();
        using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repo, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json")));
        var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            geometryRoot,
            GeometryIrMeshEmitOptions.ForParity(64, 64) with { OfficialJvmName = jvm });

        Assert.Equal(PreviewMeshDriverKind.RuntimeGeometryIrJson, prov.Kind);

        var lines = new List<string> { $"driver={prov.Kind} detail={prov.Detail}" };
        foreach (var pid in new[] { "head", "right_horn", "left_horn", "body" })
        {
            for (var i = 0; i < mesh.Elements.Count; i++)
            {
                if (!string.Equals(partIds[i], pid, StringComparison.Ordinal))
                {
                    continue;
                }

                var el = mesh.Elements[i];
                var pivot = new Vector3(el.LocalToParent.M41, el.LocalToParent.M42, el.LocalToParent.M43);
                var center = new Vector3(
                    (el.From[0] + el.To[0]) * 0.5f,
                    (el.From[1] + el.To[1]) * 0.5f,
                    (el.From[2] + el.To[2]) * 0.5f);
                var worldCenter = Vector3.Transform(center, el.LocalToParent);
                lines.Add($"{pid}[{i}] pivot={pivot} cuboidCenter={center} worldCenter={worldCenter}");
            }
        }

        Assert.Fail(string.Join("\n", lines));
    }

    private static string Fmt(float[] v) => $"<{v[0]:F2},{v[1]:F2},{v[2]:F2}>";
}
