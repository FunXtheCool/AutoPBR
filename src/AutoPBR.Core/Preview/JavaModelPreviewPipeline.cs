using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal static class JavaModelPreviewPipeline
{
    /// <summary>Unique texture zip paths in model face iteration order (stable material slot order).</summary>
    public static List<string> CollectOrderedTextureZipPaths(MergedJavaBlockModel merged, string textureNamespace)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var faceOrder = new[] { "north", "south", "west", "east", "up", "down" };
        foreach (var el in merged.Elements)
        {
            foreach (var faceName in faceOrder)
            {
                if (!el.Faces.TryGetValue(faceName, out var face))
                {
                    continue;
                }

                if (!MinecraftModelBaker.TryResolveTextureZipPath(face.TextureKey, merged.Textures, textureNamespace,
                        out var texZip))
                {
                    continue;
                }

                if (seen.Add(texZip))
                {
                    ordered.Add(texZip);
                }
            }
        }

        return ordered;
    }

    public static TextureWorkItem? FindWorkItemByDiffuseZipPath(
        IReadOnlyList<TextureWorkItem> scannedTextures,
        string extractedRoot,
        string textureZipPath)
    {
        var norm = textureZipPath.Replace('\\', '/').TrimStart('/');
        foreach (var t in scannedTextures)
        {
            var rel = Path.GetRelativePath(extractedRoot, t.DiffusePath).Replace('\\', '/');
            if (string.Equals(rel, norm, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }
}
