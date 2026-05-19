using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

internal static class MinecraftModelMerger
{
    public static bool TryMerge(IAssetSource source, string modelZipPath, out MergedJavaBlockModel merged)
    {
        merged = null!;
        var chainTexts = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!CollectChainJsonTexts(source, modelZipPath, visited, chainTexts))
        {
            return false;
        }

        var textures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var jsonText in chainTexts)
        {
            using var doc = JsonDocument.Parse(jsonText,
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            MergeTextures(doc.RootElement, textures);
        }

        List<ModelElement>? elements = null;
        for (var i = chainTexts.Count - 1; i >= 0; i--)
        {
            using var doc = JsonDocument.Parse(chainTexts[i],
                new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (!root.TryGetProperty("elements", out var elProp) || elProp.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var parsed = ParseElements(elProp);
            if (parsed.Count > 0)
            {
                elements = parsed;
                break;
            }
        }

        if (elements is null || elements.Count == 0)
        {
            return false;
        }

        merged = new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = textures
        };
        return true;
    }

    private static bool CollectChainJsonTexts(
        IAssetSource source,
        string modelZipPath,
        HashSet<string> visited,
        List<string> chainTexts)
    {
        if (!visited.Add(modelZipPath))
        {
            return false;
        }

        if (!source.TryReadText(modelZipPath, out var text))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(text,
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;
        if (root.TryGetProperty("parent", out var p) && p.ValueKind == JsonValueKind.String)
        {
            var parentRef = p.GetString();
            if (!string.IsNullOrEmpty(parentRef))
            {
                if (parentRef.StartsWith("builtin/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!TryResolveModelPath(modelZipPath, parentRef, out var parentZipPath) ||
                    !CollectChainJsonTexts(source, parentZipPath, visited, chainTexts))
                {
                    return false;
                }
            }
        }

        chainTexts.Add(text);
        return true;
    }

    internal static bool TryResolveModelPath(string currentModelZipPath, string parentRef, out string parentZipPath)
    {
        parentZipPath = string.Empty;
        var parts = currentModelZipPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var defaultNs = parts.Length > 1 && parts[0].Equals("assets", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : "minecraft";

        var ns = defaultNs;
        var rel = parentRef.Replace('\\', '/');
        if (rel.Contains(':', StringComparison.Ordinal))
        {
            var colon = rel.IndexOf(':');
            ns = rel[..colon];
            rel = rel[(colon + 1)..];
        }

        rel = rel.TrimStart('/');
        if (!rel.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
        {
            rel = "models/" + rel;
        }

        if (!rel.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            rel += ".json";
        }

        parentZipPath = $"assets/{ns}/{rel}";
        return true;
    }

    private static void MergeTextures(JsonElement root, Dictionary<string, string> textures)
    {
        if (!root.TryGetProperty("textures", out var t) || t.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in t.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                textures[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }
        }
    }

    private static List<ModelElement> ParseElements(JsonElement elementsArray)
    {
        var list = new List<ModelElement>();
        foreach (var el in elementsArray.EnumerateArray())
        {
            if (!el.TryGetProperty("from", out var fromP) || !el.TryGetProperty("to", out var toP))
            {
                continue;
            }

            var from = ReadVec3(fromP);
            var to = ReadVec3(toP);
            if (from is null || to is null)
            {
                continue;
            }

            if (!el.TryGetProperty("faces", out var facesObj) || facesObj.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var faces = new Dictionary<string, ModelFace>(StringComparer.OrdinalIgnoreCase);
            foreach (var faceProp in facesObj.EnumerateObject())
            {
                if (faceProp.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var fv = faceProp.Value;
                if (!fv.TryGetProperty("texture", out var texEl) || texEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var texKey = texEl.GetString() ?? string.Empty;
                float[]? uv = null;
                if (fv.TryGetProperty("uv", out var uvEl) && uvEl.ValueKind == JsonValueKind.Array)
                {
                    var arr = uvEl.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
                    if (arr.Length >= 4)
                    {
                        uv = arr[..4];
                    }
                }

                var rotation = 0;
                if (fv.TryGetProperty("rotation", out var rotEl) && rotEl.ValueKind == JsonValueKind.Number &&
                    rotEl.TryGetInt32(out var parsedRot))
                {
                    rotation = ((parsedRot % 360) + 360) % 360;
                    if (rotation is not (0 or 90 or 180 or 270))
                    {
                        rotation = 0;
                    }
                }

                faces[faceProp.Name] = new ModelFace { TextureKey = texKey, Uv = uv, RotationDegrees = rotation };
            }

            if (faces.Count > 0)
            {
                var localToParent = TryParseElementRotation(el, out var pose) ? pose : Matrix4x4.Identity;
                list.Add(new ModelElement { From = from, To = to, Faces = faces, LocalToParent = localToParent });
            }
        }

        return list;
    }

    /// <summary>
    /// Block/item model element <c>rotation</c> (Minecraft Wiki): pivot <c>origin</c> (default 8,8,8), <c>axis</c>, <c>angle</c> in degrees.
    /// <c>rescale</c> is accepted but only rotation is applied (vanilla UV rescale not replicated in preview).
    /// </summary>
    private static bool TryParseElementRotation(JsonElement element, out Matrix4x4 localToParent)
    {
        localToParent = Matrix4x4.Identity;
        if (!element.TryGetProperty("rotation", out var rot) || rot.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var ox = 8f;
        var oy = 8f;
        var oz = 8f;
        if (rot.TryGetProperty("origin", out var originEl) && originEl.ValueKind == JsonValueKind.Array)
        {
            var o = ReadVec3(originEl);
            if (o is not null)
            {
                ox = o[0];
                oy = o[1];
                oz = o[2];
            }
        }

        if (!rot.TryGetProperty("axis", out var axisEl) || axisEl.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var axis = axisEl.GetString();
        if (string.IsNullOrEmpty(axis))
        {
            return false;
        }

        if (!rot.TryGetProperty("angle", out var angleEl) ||
            (angleEl.ValueKind != JsonValueKind.Number && angleEl.ValueKind != JsonValueKind.String))
        {
            return false;
        }

        if (!TryReadJsonFloat(angleEl, out var angleDeg))
        {
            return false;
        }

        var rad = angleDeg * (MathF.PI / 180f);
        var rMatrix = axis.Trim().ToLowerInvariant() switch
        {
            "x" => Matrix4x4.CreateRotationX(rad),
            "y" => Matrix4x4.CreateRotationY(rad),
            "z" => Matrix4x4.CreateRotationZ(rad),
            _ => default(Matrix4x4?)
        };
        if (rMatrix is null)
        {
            return false;
        }

        var t = Matrix4x4.CreateTranslation(ox, oy, oz);
        var tn = Matrix4x4.CreateTranslation(-ox, -oy, -oz);
        localToParent = Matrix4x4.Multiply(t, Matrix4x4.Multiply(rMatrix.Value, tn));
        return true;
    }

    private static bool TryReadJsonFloat(JsonElement el, out float value)
    {
        value = 0f;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetSingle(out value),
            JsonValueKind.String => float.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture,
                out value),
            _ => false
        };
    }

    private static float[]? ReadVec3(JsonElement arr)
    {
        if (arr.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var v = new float[3];
        var i = 0;
        foreach (var x in arr.EnumerateArray())
        {
            if (i >= 3)
            {
                break;
            }

            v[i++] = (float)x.GetDouble();
        }

        return i == 3 ? v : null;
    }

}
