using AutoPBR.Tests.TestSupport;
using System.Text.Json.Nodes;

namespace AutoPBR.GeometryCompiler.Tests;

[Collection(nameof(GeometryLiftSerialDefinition))]
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class HeuristicStatusFixTests
{
    private static readonly string[] FormerHeuristicModels =
    [
        "net.minecraft.client.model.animal.squid.SquidModel",
        "net.minecraft.client.model.effects.SpinAttackEffectModel",
        "net.minecraft.client.model.monster.dragon.EnderDragonModel",
        "net.minecraft.client.model.monster.endermite.EndermiteModel",
        "net.minecraft.client.model.monster.silverfish.SilverfishModel",
        "net.minecraft.client.model.monster.slime.MagmaCubeModel"
    ];

    [Theory]
    [MemberData(nameof(FormerHeuristicModelNames))]
    public void Former_heuristic_models_lift_with_exact_cuboids(string officialJvmName)
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftFromJar(jar, officialJvmName, "createBodyLayer", maps: null,
                out var roots, out var notes, out _),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) > 0, string.Join("; ", notes));
        Assert.All(WalkCuboids(roots), c => Assert.Equal("exact", (string?)c["liftKind"]));
    }

    [Fact]
    public void SquidModel_lifts_body_and_eight_tentacle_parts()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftFromJar(jar,
                "net.minecraft.client.model.animal.squid.SquidModel",
                "createBodyLayer",
                maps: null,
                out var roots,
                out var notes,
                out _),
            string.Join("; ", notes));
        var parts = WalkParts(roots).Select(p => (string?)p["id"]).Where(id => id is not null).ToList();
        var tentacles = parts.Where(id => IsIndexedLoopPartId(id, "tentacle")).ToList();
        Assert.Contains("body", parts);
        Assert.True(tentacles.Count == 8,
            $"Expected 8 tentacle parts, got {tentacles.Count}: [{string.Join(", ", tentacles)}]; all parts: [{string.Join(", ", parts)}]; notes: {string.Join("; ", notes)}");
    }

    [Fact]
    public void EnderDragonModel_lifts_from_bytecode_not_hand_provenance()
    {
        var jar = ResolveClientJar();
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftFromJar(jar,
                "net.minecraft.client.model.monster.dragon.EnderDragonModel",
                "createBodyLayer",
                maps: null,
                out var roots,
                out var notes,
                out _),
            string.Join("; ", notes));
        Assert.True(CountCuboids(roots) > 20, string.Join("; ", notes));
        Assert.Contains(WalkCuboids(roots),
            c => ((string?)c["provenance"] ?? "").Contains("javap lift", StringComparison.Ordinal));
    }

    [Fact]
    public void SilverfishModel_loop_unrolls_seven_segments()
    {
        var jar = ResolveClientJar();
        Assert.True(ClientJarIO.TryResolveJarEntry(jar,
            "net.minecraft.client.model.monster.silverfish.SilverfishModel", null, out _, out var bytes));
        var matrices = JvmStaticIntMatrixExtractor.ExtractFromClass(bytes);
        Assert.True(matrices.ContainsKey("BODY_SIZES"));
        Assert.True(
            BytecodeGeometryMeshLift.TryLiftFromJar(jar,
                "net.minecraft.client.model.monster.silverfish.SilverfishModel",
                "createBodyLayer",
                maps: null,
                out var roots,
                out var notes,
                out _),
            string.Join("; ", notes));
        var cuboids = CountCuboids(roots);
        var segments = WalkParts(roots).Select(p => (string?)p["id"])
            .Where(id => IsIndexedLoopPartId(id, "segment")).ToList();
        Assert.True(cuboids > 0 && segments.Count >= 7,
            $"cuboids={cuboids} segments=[{string.Join(", ", segments)}]; {string.Join("; ", notes)}");
    }

    public static IEnumerable<object[]> FormerHeuristicModelNames() =>
        FormerHeuristicModels.Select(m => new object[] { m });

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        foreach (var node in roots)
        {
            if (node is not JsonObject p)
            {
                continue;
            }

            if (p["cuboids"] is JsonArray c)
            {
                n += c.Count;
            }

            if (p["children"] is JsonArray ch)
            {
                n += CountCuboids(ch);
            }
        }

        return n;
    }

    private static IEnumerable<JsonObject> WalkCuboids(JsonArray roots)
    {
        foreach (var part in WalkParts(roots))
        {
            if (part["cuboids"] is not JsonArray cuboids)
            {
                continue;
            }

            foreach (var c in cuboids)
            {
                if (c is JsonObject co)
                {
                    yield return co;
                }
            }
        }
    }

    private static IEnumerable<JsonObject> WalkParts(JsonArray roots)
    {
        foreach (var node in roots)
        {
            if (node is not JsonObject p)
            {
                continue;
            }

            yield return p;
            if (p["children"] is JsonArray ch)
            {
                foreach (var child in WalkParts(ch))
                {
                    yield return child;
                }
            }
        }
    }

    private static bool IsIndexedLoopPartId(string? id, string prefix) =>
        id is not null &&
        id.StartsWith(prefix, StringComparison.Ordinal) &&
        id.Length > prefix.Length &&
        id.AsSpan(prefix.Length).IndexOfAnyExcept("0123456789".AsSpan()) < 0;

    private static string? ResolveClientJar() =>
        GeometryIrTestTierSupport.TryClientJarPath(FindRepoRoot());

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AutoPBR.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find repo root (AutoPBR.sln).");
    }
}
