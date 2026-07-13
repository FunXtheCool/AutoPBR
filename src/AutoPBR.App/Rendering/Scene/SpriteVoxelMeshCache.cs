using System.IO.Hashing;

using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.Scene;

/// <summary>CPU-side cache for per-texel sprite voxel meshes (Explore item browsing).</summary>
internal static class SpriteVoxelMeshCache
{
    private readonly record struct CacheKey(ulong AlbedoFp, int Width, int Height, float Thickness, float AlphaCutoff);

    private const int MaxEntries = 96;

    private static readonly object Sync = new();
    private static readonly Dictionary<CacheKey, PreviewMesh> Cache = new();
    private static readonly LinkedList<CacheKey> Lru = new();

    public static PreviewMesh GetOrBuild(
        ReadOnlySpan<byte> rgba,
        int width,
        int height,
        float thickness,
        float alphaCutoff,
        string name = "sprite_voxels")
    {
        var key = new CacheKey(Fingerprint(rgba), width, height, thickness, alphaCutoff);
        lock (Sync)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                TouchLru(key);
                return cached;
            }
        }

        var built = SpriteVoxelMeshBuilder.Build(rgba, width, height, thickness, alphaCutoff, name);
        lock (Sync)
        {
            if (Cache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            Cache[key] = built;
            TouchLru(key);
            EvictIfNeeded();
            return built;
        }
    }

    private static void TouchLru(CacheKey key)
    {
        Lru.Remove(key);
        Lru.AddFirst(key);
    }

    private static void EvictIfNeeded()
    {
        while (Cache.Count > MaxEntries && Lru.Last is not null)
        {
            var evict = Lru.Last.Value;
            Lru.RemoveLast();
            Cache.Remove(evict);
        }
    }

    private static ulong Fingerprint(ReadOnlySpan<byte> rgba)
    {
        if (rgba.IsEmpty)
        {
            return 0;
        }

        var hash = new XxHash64();
        hash.Append(rgba);
        return hash.GetCurrentHashAsUInt64();
    }
}
