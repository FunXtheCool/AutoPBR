using AutoPBR.Tests.TestSupport;
using System.IO.Compression;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Inventory against pinned <c>tools/minecraft-parity/&lt;version&gt;/client.jar</c>.
/// When the jar is not present (typical CI or shallow checkouts), tests exit without asserting.
/// Excluded from default CI via <c>tests/ci.runsettings</c> (<see cref="GeometryIrTestTierSupport.MinecraftClientJarCategory"/> trait).
/// </summary>
[Trait(GeometryIrTestTierSupport.MinecraftClientJarTraitName, GeometryIrTestTierSupport.MinecraftClientJarCategory)]
public sealed class EntityTextureRoutingInventoryTests
{
    private static string? FindClientJar(string versionFolder)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "minecraft-parity", versionFolder, "client.jar");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void WhenClientJarPresent(string versionFolder, Action<string> useJar)
    {
        var jar = FindClientJar(versionFolder);
        if (string.IsNullOrEmpty(jar))
        {
            return;
        }

        useJar(jar);
    }

    private static IEnumerable<string> EnumerateVanillaEntityPngPaths(string jarPath)
    {
        using var zip = ZipFile.OpenRead(jarPath);
        foreach (var e in zip.Entries)
        {
            if (!e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!e.FullName.StartsWith("assets/minecraft/textures/entity/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return e.FullName.Replace('\\', '/');
        }
    }

    [Fact]
    public void VanillaJar_EntityTexturesHaveNoFamilyFallbacks()
    {
        WhenClientJarPresent("1.21.11", jar =>
        {
            var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
            var quad = new List<string>();
            var fly = new List<string>();
            var aqua = new List<string>();
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                var route = CleanRoomEntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                switch (route)
                {
                    case EntityPreviewRouteKind.QuadrupedFamilyFallback:
                        quad.Add(path);
                        break;
                    case EntityPreviewRouteKind.FlyingFamilyFallback:
                        fly.Add(path);
                        break;
                    case EntityPreviewRouteKind.AquaticFamilyFallback:
                        aqua.Add(path);
                        break;
                }
            }

            Assert.True(quad.Count == 0, string.Join('\n', quad));
            Assert.True(fly.Count == 0, string.Join('\n', fly));
            Assert.True(aqua.Count == 0, string.Join('\n', aqua));
        });
    }

    [Fact]
    public void VanillaJar_EntityTexturesHaveNoUnknownMisses()
    {
        WhenClientJarPresent("1.21.11", jar =>
        {
            var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
            var unknown = new List<string>();
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                var route = CleanRoomEntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                if (route == EntityPreviewRouteKind.UnknownNoMesh)
                {
                    unknown.Add(path);
                }
            }

            Assert.True(unknown.Count == 0, string.Join('\n', unknown));
        });
    }

    [Fact]
    public void VanillaJar_26_1_2_EntityTexturesHaveNoFamilyFallbacks_WhenJarPresent()
    {
        WhenClientJarPresent("26.1.2", jar =>
        {
            var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
            var quad = new List<string>();
            var fly = new List<string>();
            var aqua = new List<string>();
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                var route = CleanRoomEntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                switch (route)
                {
                    case EntityPreviewRouteKind.QuadrupedFamilyFallback:
                        quad.Add(path);
                        break;
                    case EntityPreviewRouteKind.FlyingFamilyFallback:
                        fly.Add(path);
                        break;
                    case EntityPreviewRouteKind.AquaticFamilyFallback:
                        aqua.Add(path);
                        break;
                }
            }

            Assert.True(quad.Count == 0, string.Join('\n', quad));
            Assert.True(fly.Count == 0, string.Join('\n', fly));
            Assert.True(aqua.Count == 0, string.Join('\n', aqua));
        });
    }

    [Fact]
    public void VanillaJar_26_1_2_EntityTexturesHaveNoUnknownMisses_WhenJarPresent()
    {
        WhenClientJarPresent("26.1.2", jar =>
        {
            var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
            var unknown = new List<string>();
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                var route = CleanRoomEntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                if (route == EntityPreviewRouteKind.UnknownNoMesh)
                {
                    unknown.Add(path);
                }
            }

            Assert.True(unknown.Count == 0, string.Join('\n', unknown));
        });
    }
}
