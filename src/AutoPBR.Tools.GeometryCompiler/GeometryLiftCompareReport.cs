using System.Globalization;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

internal static class GeometryLiftCompareReport
{
    public static void WriteToStderr(string officialJvmName, JsonArray asmRoots, JsonArray javapRoots)
    {
        var asmCuboids = CountCuboids(asmRoots);
        var javapCuboids = CountCuboids(javapRoots);
        var asmParts = CollectPartIds(asmRoots);
        var javapParts = CollectPartIds(javapRoots);

        var onlyAsm = asmParts.Except(javapParts, StringComparer.Ordinal).ToList();
        var onlyJavap = javapParts.Except(asmParts, StringComparer.Ordinal).ToList();

        Console.Error.WriteLine(
            string.Create(CultureInfo.InvariantCulture,
                $"geometry_lift_compare {officialJvmName}: asm_cuboids={asmCuboids} javap_cuboids={javapCuboids} asm_parts={asmParts.Count} javap_parts={javapParts.Count}"));

        if (onlyAsm.Count > 0)
        {
            Console.Error.WriteLine($"  parts_only_asm: {string.Join(", ", onlyAsm.Take(8))}");
        }

        if (onlyJavap.Count > 0)
        {
            Console.Error.WriteLine($"  parts_only_javap: {string.Join(", ", onlyJavap.Take(8))}");
        }

        if (asmCuboids != javapCuboids)
        {
            Console.Error.WriteLine(
                string.Create(CultureInfo.InvariantCulture,
                    $"  cuboid_count_delta: asm={asmCuboids} javap={javapCuboids}"));
        }
    }

    public static bool AreStructurallyAligned(JsonArray asmRoots, JsonArray javapRoots, out string? mismatch)
    {
        mismatch = null;
        var asmCuboids = CountCuboids(asmRoots);
        var javapCuboids = CountCuboids(javapRoots);
        if (asmCuboids != javapCuboids)
        {
            mismatch = $"cuboid count asm={asmCuboids} javap={javapCuboids}";
            return false;
        }

        var asmParts = CollectPartIds(asmRoots);
        var javapParts = CollectPartIds(javapRoots);
        if (!asmParts.SetEquals(javapParts))
        {
            mismatch = "part id set differs";
            return false;
        }

        if (!AreCuboidGeometryAligned(asmRoots, javapRoots, out var geomMismatch))
        {
            mismatch = geomMismatch;
            return false;
        }

        return true;
    }

    /// <summary>Compares cuboid origin/size/UV fingerprints in tree-walk order.</summary>
    public static bool AreCuboidGeometryAligned(JsonArray asmRoots, JsonArray javapRoots, out string? mismatch)
    {
        mismatch = null;
        var asmFp = CollectCuboidFingerprints(asmRoots);
        var javapFp = CollectCuboidFingerprints(javapRoots);
        if (asmFp.Count != javapFp.Count)
        {
            mismatch = $"cuboid fingerprint count asm={asmFp.Count} javap={javapFp.Count}";
            return false;
        }

        for (var i = 0; i < asmFp.Count; i++)
        {
            if (asmFp[i] == javapFp[i])
            {
                continue;
            }

            mismatch = string.Create(CultureInfo.InvariantCulture,
                $"cuboid geometry differs at index {i}: asm={asmFp[i]} javap={javapFp[i]}");
            return false;
        }

        return true;
    }

    private static int CountCuboids(JsonArray roots)
    {
        var n = 0;
        Walk(roots, ref n);
        return n;
    }

    private static void Walk(JsonArray nodes, ref int n)
    {
        foreach (var node in nodes)
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
                Walk(ch, ref n);
            }
        }
    }

    private static HashSet<string> CollectPartIds(JsonArray roots)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        WalkIds(roots, set);
        return set;
    }

    private static void WalkIds(JsonArray nodes, HashSet<string> set)
    {
        foreach (var node in nodes)
        {
            if (node is not JsonObject p)
            {
                continue;
            }

            if (p["id"]?.GetValue<string>() is { } id)
            {
                set.Add(id);
            }

            if (p["children"] is JsonArray ch)
            {
                WalkIds(ch, set);
            }
        }
    }

    private static List<string> CollectCuboidFingerprints(JsonArray roots)
    {
        var list = new List<string>();
        WalkCuboidFingerprints(roots, list);
        return list;
    }

    private static void WalkCuboidFingerprints(JsonArray nodes, List<string> fingerprints)
    {
        foreach (var node in nodes)
        {
            if (node is not JsonObject p)
            {
                continue;
            }

            if (p["cuboids"] is JsonArray cuboids)
            {
                foreach (var cNode in cuboids)
                {
                    if (cNode is not JsonObject c)
                    {
                        continue;
                    }

                    fingerprints.Add(CuboidFingerprint(c));
                }
            }

            if (p["children"] is JsonArray ch)
            {
                WalkCuboidFingerprints(ch, fingerprints);
            }
        }
    }

    private static string CuboidFingerprint(JsonObject cuboid)
    {
        static string F(JsonObject? o, string key) =>
            o?[key]?.GetValue<float>().ToString("R", CultureInfo.InvariantCulture) ?? "0";

        var from = cuboid["from"] as JsonObject;
        var to = cuboid["to"] as JsonObject;
        var uv = cuboid["uv"] as JsonObject;
        return string.Create(CultureInfo.InvariantCulture,
            $"{F(from, "x")},{F(from, "y")},{F(from, "z")}|{F(to, "x")},{F(to, "y")},{F(to, "z")}|{F(uv, "u")},{F(uv, "v")}");
    }
}
