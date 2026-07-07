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
    public void VanillaJar_EntityTexturesDoNotUseRemovedFamilyFallbackRoutes()
    {
        WhenClientJarPresent("1.21.11", jar =>
        {
            var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                var route = EntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                Assert.True(
                    route is EntityPreviewRouteKind.ParityCatalogGeometryIr
                        or EntityPreviewRouteKind.ErrorPlaceholder
                        or EntityPreviewRouteKind.InvalidPath,
                    $"{path}: {route}");
            }
        });
    }

    [Fact]
    public void VanillaJar_EntityTexturesClassifyToMeshOrErrorPlaceholder()
    {
        WhenClientJarPresent("1.21.11", jar =>
        {
            var profile = new MinecraftNativeProfile("1.21.11", "unused", new Version(1, 21, 11));
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                var route = EntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                Assert.NotEqual(EntityPreviewRouteKind.InvalidPath, route);
            }
        });
    }

    [Fact]
    public void VanillaJar_26_1_2_EntityTexturesDoNotUseRemovedFamilyFallbackRoutes_WhenJarPresent()
    {
        WhenClientJarPresent("26.1.2", jar =>
        {
            var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                var route = EntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                Assert.True(
                    route is EntityPreviewRouteKind.ParityCatalogGeometryIr
                        or EntityPreviewRouteKind.ErrorPlaceholder,
                    $"{path}: {route}");
            }
        });
    }

    [Fact]
    public void VanillaJar_26_1_2_CataloguedEntityTexturesUseGeometryIr_WhenJarPresent()
    {
        WhenClientJarPresent("26.1.2", jar =>
        {
            var profile = new MinecraftNativeProfile("26.1.2", "unused", new Version(26, 1, 2));
            var irMiss = new List<string>();
            foreach (var path in EnumerateVanillaEntityPngPaths(jar))
            {
                if (!EntityTextureParityCatalog.IsCatalogued(path))
                {
                    continue;
                }

                var route = EntityModelRuntime.ClassifyEntityTextureRoute(
                    path,
                    profile,
                    idlePhase01: 0.33f,
                    animationTimeSeconds: 0.41f);
                if (route != EntityPreviewRouteKind.ParityCatalogGeometryIr)
                {
                    irMiss.Add($"{path}: {route}");
                }
            }

            Assert.True(irMiss.Count == 0, string.Join('\n', irMiss));
        });
    }
}
