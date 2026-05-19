using System.IO.Compression;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

internal static class ClientJarIO
{
    public static bool TryReadClass(string jarPath, string jarEntrySlash, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        try
        {
            using var zip = ZipFile.OpenRead(jarPath);
            var e = zip.GetEntry(jarEntrySlash);
            if (e is null)
            {
                return false;
            }

            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            bytes = ms.ToArray();
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static string OfficialToJarPath(string officialJvmName) =>
        officialJvmName.Replace('.', '/') + ".class";

    public static bool TryResolveJarEntry(string jarPath, string officialJvmName, string? obfuscatedJvmName,
        out string usedJarPath, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        usedJarPath = OfficialToJarPath(officialJvmName);
        if (TryReadClass(jarPath, usedJarPath, out bytes))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(obfuscatedJvmName))
        {
            var obfPath = OfficialToJarPath(obfuscatedJvmName);
            if (TryReadClass(jarPath, obfPath, out bytes))
            {
                usedJarPath = obfPath;
                return true;
            }

            // Root-only obfuscated class file (some repacks)
            var simple = MojangMappingsParser.GetJavapClassArgForObfuscated(obfuscatedJvmName) + ".class";
            if (TryReadClass(jarPath, simple, out bytes))
            {
                usedJarPath = simple;
                return true;
            }
        }

        return false;
    }
}

internal static class GeometryBytecodeMerge
{
    /// <summary>Merges bytecode-derived fields into a geometry IR JSON object.</summary>
    /// <param name="root">Geometry shard root object.</param>
    /// <param name="classSha256Hex">SHA-256 hex of the <c>.class</c> file bytes.</param>
    /// <param name="probe">Float constants lifted from the factory method bytecode.</param>
    /// <param name="javapOk">Whether <c>javap -c</c> succeeded for the target method.</param>
    /// <remarks>
    /// Records SHA-256 and float probe metadata only. Final <c>extractionStatus</c> is assigned after mesh lift
    /// (<see cref="GeometryCompilerHost"/>).
    /// </remarks>
    public static void ApplyProbe(JsonObject root, string classSha256Hex, IReadOnlyList<float> probe, bool javapOk)
    {
        root["classSha256Hex"] = classSha256Hex;
        var arr = new JsonArray();
        foreach (var f in probe)
        {
            arr.Add(JsonValue.Create(f));
        }

        root["bytecodeFloatProbe"] = arr;
        if (!javapOk)
        {
            root["extractionStatus"] = "partial";
            AddNote(root, "javap float probe failed or missing createBodyLayer Code block.");
            return;
        }

        RemoveExtractionNotesContaining(root, "javap float probe failed or missing createBodyLayer Code block.");
        if (GeometryShardTreeHasAnyCuboid(root))
        {
            RemoveExtractionNotesContaining(root,
                "ldc float constants found in createBodyLayer bytecode; part-tree IR is still incomplete or placeholder-only.");
        }

        if (!GeometryShardTreeHasAnyCuboid(root))
        {
            root["extractionStatus"] = "partial";
            if (probe.Count > 0)
            {
                AddNote(root,
                    "ldc float constants found in createBodyLayer bytecode; part-tree IR is still incomplete or placeholder-only.");
            }
        }
    }

    private static void AddNote(JsonObject root, string note)
    {
        if (root["extractionNotes"] is not JsonArray a)
        {
            a = new JsonArray();
            root["extractionNotes"] = a;
        }

        foreach (var existing in a)
        {
            if (existing is JsonValue jv && jv.TryGetValue<string>(out var s) && string.Equals(s, note, StringComparison.Ordinal))
            {
                return;
            }
        }

        a.Add(note);
    }

    /// <summary>True when the geometry IR under <c>roots</c> contains at least one cuboid.</summary>
    private static bool GeometryShardTreeHasAnyCuboid(JsonObject shard)
    {
        if (shard["roots"] is not JsonArray roots)
        {
            return false;
        }

        foreach (var r in roots)
        {
            if (PartNodeSubtreeHasCuboid(r))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PartNodeSubtreeHasCuboid(JsonNode? node)
    {
        if (node is not JsonObject o)
        {
            return false;
        }

        if (o["cuboids"] is JsonArray c && c.Count > 0)
        {
            return true;
        }

        if (o["children"] is not JsonArray ch)
        {
            return false;
        }

        foreach (var child in ch)
        {
            if (PartNodeSubtreeHasCuboid(child))
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveExtractionNotesContaining(JsonObject root, string substring)
    {
        if (root["extractionNotes"] is not JsonArray a || a.Count == 0)
        {
            return;
        }

        var next = new JsonArray();
        foreach (var n in a)
        {
            if (n is JsonValue jv && jv.TryGetValue<string>(out var s) &&
                s.Contains(substring, StringComparison.Ordinal))
            {
                continue;
            }

            next.Add(n!.DeepClone());
        }

        root["extractionNotes"] = next;
    }
}
