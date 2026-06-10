using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

/// <summary>Diagnostic dump: per-part preview origins vs reference_java (not gated — always writes report).</summary>
public sealed class BabyVsAdultReferenceOriginDumpTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", "unused", new Version(26, 1, 2));

    [Fact]
    public void Dump_baby_and_adult_quadruped_part_origin_gaps()
    {
        var cases = new (string tex, string jvm, int w, int h, string label)[]
        {
            ("assets/minecraft/textures/entity/cow/cow_temperate.png", "net.minecraft.client.model.animal.cow.CowModel", 64, 64, "adult_cow"),
            ("assets/minecraft/textures/entity/cow/cow_temperate_baby.png", "net.minecraft.client.model.animal.cow.BabyCowModel", 64, 64, "baby_cow"),
            ("assets/minecraft/textures/entity/fox/fox_baby.png", "net.minecraft.client.model.animal.fox.BabyFoxModel", 64, 64, "baby_fox"),
            ("assets/minecraft/textures/entity/horse/horse_black_baby.png", "net.minecraft.client.model.animal.equine.BabyHorseModel", 64, 64, "baby_horse"),
            ("assets/minecraft/textures/entity/goat/goat_baby.png", "net.minecraft.client.model.animal.goat.BabyGoatModel", 64, 64, "baby_goat"),
            ("assets/minecraft/textures/entity/cat/cat_british_shorthair_baby.png", "net.minecraft.client.model.animal.feline.BabyCatModel", 64, 64, "baby_cat"),
        };

        var root = GeometryIrTestTierSupport.FindRepoRoot();
        var sb = new StringBuilder();
        sb.AppendLine("label\tpartId\trefModelX\trefModelY\trefModelZ\tmeshPreviewX\tmeshPreviewY\tmeshPreviewZ\tgapPreview\tstatus");

        foreach (var (tex, jvm, w, h, label) in cases)
        {
            DumpCase(root, sb, tex, jvm, w, h, label);
        }

        var outPath = Path.Combine(root, "docs", "generated", "manual-explore-baby-adult-origin-dump.tsv");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, sb.ToString());
        Assert.True(File.Exists(outPath));
    }

    private static void DumpCase(
        string repoRoot,
        StringBuilder sb,
        string texturePath,
        string jvm,
        int atlasW,
        int atlasH,
        string label)
    {
        var referencePath = Path.Combine(repoRoot, "tools", "MinecraftGeometryReference", "reference-output", $"{jvm}.json");
        if (!File.Exists(referencePath))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{label}\tMISSING_REF\t0\t0\t0\t0\t0\t0\t0\t0\t0");
            return;
        }

        using var reference = JsonDocument.Parse(File.ReadAllText(referencePath));
        if (!GeometryIrMeshWalk.TryCollectBakedWorldTranslations(
                reference.RootElement, out var refWorld, out var refFail))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{label}\tREF_FAIL:{refFail}\t0\t0\t0\t0\t0\t0\t0\t0\t0");
            return;
        }

        GeometryIrParityPolicy.ResetForTests();
        var runtime = EntityModelRuntimeFactory.Create();
        if (!runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mesh, out _))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{label}\tBUILD_FAIL\t0\t0\t0\t0\t0\t0\t0\t0\t0");
            return;
        }

        var shardPath = Path.Combine(repoRoot, "docs", "generated", "geometry", "26.1.2", $"{jvm}.json");
        using var shard = JsonDocument.Parse(File.ReadAllText(shardPath));
        var repaired = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(
            repaired,
            GeometryIrMeshEmitOptions.ForParity(atlasW, atlasH) with { OfficialJvmName = jvm });
        var ler = CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale;

        foreach (var (partId, refOrigin) in refWorld.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!TryMeanPartOrigin(mesh!, partIds, partId, out var meshPreviewOrigin))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"{label}\t{partId}\t{refOrigin.X:F3}\t{refOrigin.Y:F3}\t{refOrigin.Z:F3}\tNA\tNA\tNA\tNA\tNA\tNA");
                continue;
            }

            var expectedPreview = Vector3.Transform(refOrigin, ler);
            var gap = Vector3.Distance(expectedPreview, meshPreviewOrigin);
            var status = gap <= 0.35 ? "OK" : "FAIL";
            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"{label}\t{partId}\t{refOrigin.X:F3}\t{refOrigin.Y:F3}\t{refOrigin.Z:F3}\t" +
                $"{meshPreviewOrigin.X:F3}\t{meshPreviewOrigin.Y:F3}\t{meshPreviewOrigin.Z:F3}\t{gap:F3}\t{status}");
        }
    }

    private static bool TryMeanPartOrigin(
        MergedJavaBlockModel mesh,
        List<string> partIds,
        string partId,
        out Vector3 origin)
    {
        origin = default;
        var sum = Vector3.Zero;
        var count = 0;
        for (var i = 0; i < partIds.Count && i < mesh.Elements.Count; i++)
        {
            if (!string.Equals(partIds[i], partId, StringComparison.Ordinal))
            {
                continue;
            }

            sum += Vector3.Transform(Vector3.Zero, mesh.Elements[i].LocalToParent);
            count++;
        }

        if (count == 0)
        {
            return false;
        }

        origin = sum / count;
        return true;
    }
}
