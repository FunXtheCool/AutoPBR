using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

using AutoPBR.Core.Models;
using AutoPBR.Preview;
using AutoPBR.Tests.TestSupport;

namespace AutoPBR.Core.Tests;

public sealed partial class ObjectEntityBlockStateParityTests
{
    [Fact]
    public void DecoratedPot_resolves_eight_cuboids_from_preview_composite_shard()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        Assert.Equal(8, model.Elements.Count);
    }

    [Fact]
    public void DecoratedPot_world_bounds_match_javap_closed_pot_assembly()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        var corners = CollectWorldCorners(model).ToList();
        var minX = corners.Min(c => c.X);
        var maxX = corners.Max(c => c.X);
        var minY = corners.Min(c => c.Y);
        var maxY = corners.Max(c => c.Y);
        var minZ = corners.Min(c => c.Z);
        var maxZ = corners.Max(c => c.Z);
        Assert.InRange(maxX - minX, 0f, 17f);
        Assert.InRange(maxZ - minZ, 0f, 17f);
        Assert.True(maxY - minY < 42f, $"height span {maxY - minY:G3} exceeds javap pot envelope");
        Assert.True(minY < 4f, $"expected neck Rx(PI) to pull geometry down; minY={minY:G3}");
    }

    [Fact]
    public void DecoratedPot_neck_pose_hoists_geometry_above_flat_hand_lift_origin()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/heartbreak_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            minY = MathF.Min(minY, cMin.Y);
            maxY = MathF.Max(maxY, cMax.Y);
        }

        Assert.True(maxY - minY > 12f, $"expected closed pot height span; got {maxY - minY:G3}");
        Assert.True(maxY > minY + 8f, $"expected upright closed pot; minY={minY:G3} maxY={maxY:G3}");
    }

    [Fact]
    public void DecoratedPot_hand_lift_shard_cuboids_match_javap_reference_tree()
    {
        const string jvm = "net.minecraft.client.model.DecoratedPotModel.previewComposite";
        Assert.True(ParityCatalogHandLiftGeometryIrCatalog.TryGetOkRoot(jvm, out var shard));
        using var reference = BuildDecoratedPotJavapReferenceRoot();
        var cmp = GeometryIrReferenceComparer.CompareReferenceToIrShardCuboidsByPartId(
            reference.RootElement,
            shard,
            tolerance: 0.05);
        Assert.True(cmp.IsMatch, cmp.Message);
    }

    [Fact]
    public void DecoratedPot_baked_vertices_stay_within_element_world_bounds()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        Assert.Equal(2, ordered.Count);
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            texSizes[ordered[i]] = ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase)
                ? (32, 32)
                : (16, 16);
        }

        var maxSlop = 0f;
        foreach (var el in mesh.Elements)
        {
            var single = new MergedJavaBlockModel
            {
                Elements = [el],
                Textures = mesh.Textures,
            };
            Assert.True(MinecraftModelBaker.TryBake(single, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));
            TransformWorldCorners(el, out var elMin, out var elMax);
            const int stride = MinecraftModelBaker.FloatsPerVertex;
            const float pad = 0.05f;
            elMin -= new Vector3(pad);
            elMax += new Vector3(pad);
            for (var vi = 0; vi < verts.Length; vi += stride)
            {
                var px = verts[vi] * 16f + 8f;
                var py = verts[vi + 1] * 16f + 8f;
                var pz = verts[vi + 2] * 16f + 8f;
                var dx = MathF.Max(0f, MathF.Max(elMin.X - px, px - elMax.X));
                var dy = MathF.Max(0f, MathF.Max(elMin.Y - py, py - elMax.Y));
                var dz = MathF.Max(0f, MathF.Max(elMin.Z - pz, pz - elMax.Z));
                maxSlop = MathF.Max(maxSlop, MathF.Sqrt(dx * dx + dy * dy + dz * dz));
            }
        }

        Assert.True(maxSlop <= 0.35f, $"detached baked verts maxSlop={maxSlop:F3} texels");
    }

    private static JsonDocument BuildDecoratedPotJavapReferenceRoot()
    {
        const float pi = 3.141592654f;
        var doc = new JsonObject
        {
            ["roots"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "root",
                    ["pose"] = new JsonObject
                    {
                        ["translation"] = new JsonArray { 0, 0, 0 },
                        ["rotationEulerRad"] = new JsonArray { 0, 0, 0 },
                    },
                    ["cuboids"] = new JsonArray(),
                    ["children"] = new JsonArray
                    {
                        RefPart("neck", 0, 37, 16, pi, 0, 0,
                            RefCuboid(4, 17, 4, 12, 20, 12, 0, 0),
                            RefCuboid(5, 20, 5, 11, 21, 11, 0, 5)),
                        RefPart("top", 1, 16, 1, 0, 0, 0, RefCapCuboid()),
                        RefPart("bottom", 1, 0, 1, 0, 0, 0, RefCapCuboid()),
                        RefPart("back", 15, 16, 1, 0, 0, pi, RefSideCuboid()),
                        RefPart("left", 1, 16, 1, 0, -pi / 2f, pi, RefSideCuboid()),
                        RefPart("right", 15, 16, 15, 0, pi / 2f, pi, RefSideCuboid()),
                        RefPart("front", 1, 16, 15, pi, 0, 0, RefSideCuboid()),
                    }
                }
            }
        };
        return JsonDocument.Parse(doc.ToJsonString());
    }

    private static JsonObject RefPart(
        string id,
        float tx, float ty, float tz,
        float rx, float ry, float rz,
        params JsonObject[] cuboids)
    {
        var cuboidArray = new JsonArray();
        foreach (var c in cuboids)
        {
            cuboidArray.Add(c);
        }

        return new JsonObject
        {
            ["id"] = id,
            ["pose"] = new JsonObject
            {
                ["translation"] = new JsonArray { tx, ty, tz },
                ["rotationEulerRad"] = new JsonArray { rx, ry, rz },
            },
            ["cuboids"] = cuboidArray,
            ["children"] = new JsonArray()
        };
    }

    private static JsonObject RefCuboid(
        float x0, float y0, float z0, float x1, float y1, float z1, int u, int v,
        int? uvW = null, int? uvH = null, int? uvD = null)
    {
        var c = new JsonObject
        {
            ["from"] = new JsonArray { x0, y0, z0 },
            ["to"] = new JsonArray { x1, y1, z1 },
            ["uvOrigin"] = new JsonArray { u, v },
        };
        if (uvW is not null && uvH is not null && uvD is not null)
        {
            c["uvSpan"] = new JsonArray { uvW.Value, uvH.Value, uvD.Value };
        }

        return c;
    }

    private static JsonObject RefSideCuboid() => RefCuboid(0, 0, 0, 14, 16, 0, 1, 0, 14, 16, 0);

    private static JsonObject RefCapCuboid()
    {
        var c = RefCuboid(0, 0, 0, 14, 0, 14, -14, 13, 14, 0, 14);
        c["faceMask"] = new JsonArray { "down" };
        return c;
    }

    [Fact]
    public void DecoratedPot_preview_orientation_has_neck_above_base_ring()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var neckMaxY = float.MinValue;
        var bottomCapMaxY = float.MaxValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isCap = el.Faces.TryGetValue("up", out var upFace) &&
                        upFace.Uv is { Length: 4 } &&
                        upFace.Uv[0] >= 13.5f &&
                        upFace.Uv[2] <= 28.5f &&
                        upFace.Uv[1] >= 12.5f &&
                        upFace.Uv[3] <= 27.5f;
            if (isCap)
            {
                bottomCapMaxY = MathF.Min(bottomCapMaxY, cMax.Y);
            }
            else
            {
                neckMaxY = MathF.Max(neckMaxY, cMax.Y);
            }
        }

        Assert.True(neckMaxY > bottomCapMaxY + 4f,
            $"expected neck above base ring; neckMaxY={neckMaxY:G3} bottomCapMaxY={bottomCapMaxY:G3}");
    }

    [Fact]
    public void DecoratedPot_cap_rings_seal_side_body_seams()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var sideMinY = float.MaxValue;
        var sideMaxY = float.MinValue;
        var topCapMaxY = float.MinValue;
        var bottomCapMinY = float.MaxValue;
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("up"))
            {
                if (cMax.Y > 8f)
                {
                    topCapMaxY = MathF.Max(topCapMaxY, cMax.Y);
                }
                else
                {
                    bottomCapMinY = MathF.Min(bottomCapMinY, cMin.Y);
                }

                continue;
            }

            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("north"))
            {
                sideMinY = MathF.Min(sideMinY, cMin.Y);
                sideMaxY = MathF.Max(sideMaxY, cMax.Y);
            }
        }

        // javap caps stay on y=0/16 planes; sides extend Y into cap rings for vertical rim seal only.
        var topGap = sideMaxY - topCapMaxY;
        var bottomGap = bottomCapMinY - sideMinY;
        Assert.True(topGap <= 0.05f,
            $"top cap should meet side rim; sideMaxY={sideMaxY:G3} topCapMaxY={topCapMaxY:G3} gap={topGap:G3}");
        Assert.True(bottomGap <= 0.05f,
            $"bottom cap should meet side base; bottomCapMinY={bottomCapMinY:G3} sideMinY={sideMinY:G3} gap={bottomGap:G3}");
    }

    [Fact]
    public void DecoratedPot_cap_ring_shoulder_y_aligns_with_side_top_edge()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        float? topCapMaxY = null;
        var sideTopYs = new List<float>();
        foreach (var el in model.Elements)
        {
            TransformWorldCorners(el, out var cMin, out var cMax);
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("up") &&
                cMax.Y > 8f)
            {
                topCapMaxY = cMax.Y;
                continue;
            }

            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                el.Faces.ContainsKey("north"))
            {
                sideTopYs.Add(cMax.Y);
            }
        }

        Assert.NotNull(topCapMaxY);
        Assert.Equal(4, sideTopYs.Count);
        foreach (var sideTopY in sideTopYs)
        {
            Assert.InRange(MathF.Abs(sideTopY - topCapMaxY.Value), 0f, 0.01f);
        }
    }

    [Fact]
    public void DecoratedPot_side_panels_extend_vertically_without_horizontal_protrusion()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        const float minSideHeight = 16f + EntityModelRuntime.DecoratedPotPreviewVerticalSealOverlap - 0.01f;
        const float maxHorizontalSpan = 14f + 0.02f;
        const float maxSideDepth = 0.52f;
        var sideCount = 0;
        foreach (var el in model.Elements)
        {
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) ||
                !el.Faces.ContainsKey("north"))
            {
                continue;
            }

            sideCount++;
            TransformWorldCorners(el, out var cMin, out var cMax);
            var xSpan = cMax.X - cMin.X;
            var zSpan = cMax.Z - cMin.Z;
            var ySpan = cMax.Y - cMin.Y;
            var horizontalSpan = MathF.Max(xSpan, zSpan);
            var thinSpan = MathF.Min(MathF.Min(xSpan, zSpan), ySpan);
            Assert.InRange(horizontalSpan, 14f - 0.02f, maxHorizontalSpan);
            Assert.True(ySpan >= minSideHeight,
                $"side panel height {ySpan:G3} should extend into cap rings (>= {minSideHeight:G3})");
            Assert.True(thinSpan <= maxSideDepth,
                $"side panel depth {thinSpan:G3} should stay within preview inward extrude (<= {maxSideDepth:G3})");
        }

        Assert.Equal(4, sideCount);
    }

    [Fact]
    public void DecoratedPot_body_ring_footprint_matches_javap_without_horizontal_overshoot()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var sideMinX = float.MaxValue;
        var sideMaxX = float.MinValue;
        var sideMinZ = float.MaxValue;
        var sideMaxZ = float.MinValue;
        foreach (var el in model.Elements)
        {
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) ||
                !el.Faces.ContainsKey("north"))
            {
                continue;
            }

            TransformWorldCorners(el, out var cMin, out var cMax);
            sideMinX = MathF.Min(sideMinX, cMin.X);
            sideMaxX = MathF.Max(sideMaxX, cMax.X);
            sideMinZ = MathF.Min(sideMinZ, cMin.Z);
            sideMaxZ = MathF.Max(sideMaxZ, cMax.Z);
        }

        // javap sides: 14-wide sheets on the 1..15 perimeter — no preview widen past those planes.
        Assert.InRange(sideMinX, 0.99f, 1.01f);
        Assert.InRange(sideMaxX, 14.99f, 15.01f);
        Assert.InRange(sideMinZ, 0.99f, 1.01f);
        Assert.InRange(sideMaxZ, 14.99f, 15.01f);
    }

    [Fact]
    public void DecoratedPot_side_exterior_plane_stays_at_local_z0_after_thicken()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var sideCount = 0;
        foreach (var el in model.Elements)
        {
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) ||
                !el.Faces.ContainsKey("north"))
            {
                continue;
            }

            sideCount++;
            Assert.InRange(el.From[2], -0.01f, 0.01f);
            Assert.InRange(el.To[2], 0.45f, 0.55f);
        }

        Assert.Equal(4, sideCount);
    }

    [Fact]
    public void DecoratedPot_cap_rings_keep_javap_horizontal_footprint()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        const float maxHorizontalSpan = 14f + 0.02f;
        const float maxCapThickness = 0.52f;
        var capCount = 0;
        foreach (var el in model.Elements)
        {
            if (!el.Faces.TryGetValue("up", out var capFace) ||
                capFace.Uv is not { Length: 4 } ||
                capFace.Uv[0] < 13.5f || capFace.Uv[2] > 28.5f ||
                capFace.Uv[1] < 12.5f || capFace.Uv[3] > 27.5f)
            {
                continue;
            }

            var texKey = capFace.TextureKey ?? "";
            if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            capCount++;
            TransformWorldCorners(el, out var cMin, out var cMax);
            var xSpan = cMax.X - cMin.X;
            var zSpan = cMax.Z - cMin.Z;
            var ySpan = cMax.Y - cMin.Y;
            Assert.InRange(xSpan, 14f - 0.02f, maxHorizontalSpan);
            Assert.InRange(zSpan, 14f - 0.02f, maxHorizontalSpan);
            Assert.True(ySpan <= maxCapThickness,
                $"cap down-face extrude should stay thin (y thickness {ySpan:G3} <= {maxCapThickness:G3})");
        }

        Assert.Equal(2, capCount);
    }

    [Fact]
    public void DecoratedPot_baked_rim_vertices_overlap_side_and_cap_sheets()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            var maps = CreatePreviewMaps(16, 16);
            if (ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase))
            {
                maps = CreatePreviewMaps(32, 32);
            }

            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i],
                maps.Width,
                maps.Height,
                provenance);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sideYs = new List<float>();
        var capTopYs = new List<float>();
        var capBottomYs = new List<float>();
        var floatOffset = 0;
        for (var ei = 0; ei < mesh.Elements.Count; ei++)
        {
            var el = mesh.Elements[ei];
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            var isCap = texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                        el.Faces.ContainsKey("up");
            var isSide = !texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                         el.Faces.ContainsKey("north");
            if (!isCap && !isSide)
            {
                floatOffset += CountElementFloats(el, stride);
                continue;
            }

            TransformWorldCorners(el, out var cMin, out var cMax);
            var isTopCap = cMax.Y > 8f;
            var vertCount = CountElementFloats(el, stride);
            for (var vi = 0; vi < vertCount / stride; vi++)
            {
                var y = verts[floatOffset + vi * stride + 1];
                if (isCap)
                {
                    if (isTopCap)
                    {
                        capTopYs.Add(y);
                    }
                    else
                    {
                        capBottomYs.Add(y);
                    }
                }
                else
                {
                    sideYs.Add(y);
                }
            }

            floatOffset += vertCount;
        }

        Assert.NotEmpty(sideYs);
        Assert.NotEmpty(capTopYs);
        var sideTopMax = sideYs.Max();
        var sideBottomMin = sideYs.Min();
        var topOverlap = sideTopMax - capTopYs.Min();
        var bottomOverlap = capBottomYs.Max() - sideBottomMin;
        Assert.True(topOverlap >= -0.05f,
            $"top rim should overlap; sideTopMax={sideTopMax:G3} capTopMin={capTopYs.Min():G3} overlap={topOverlap:G3}");
        Assert.True(bottomOverlap >= -0.05f,
            $"bottom rim should overlap; capBottomMax={capBottomYs.Max():G3} sideBottomMin={sideBottomMin:G3} overlap={bottomOverlap:G3}");
    }

    [Fact]
    public void DecoratedPot_baked_top_rim_y_overlap_uses_production_atlas_path()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            var maps = CreatePreviewMaps(16, 16);
            if (ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase))
            {
                maps = CreatePreviewMaps(32, 32);
            }

            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i],
                maps.Width,
                maps.Height,
                provenance);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        const int stride = MinecraftModelBaker.FloatsPerVertex;
        var sideTopYs = new List<float>();
        var capTopYs = new List<float>();
        var floatOffset = 0;
        for (var ei = 0; ei < mesh.Elements.Count; ei++)
        {
            var el = mesh.Elements[ei];
            var texKey = el.Faces.Values.FirstOrDefault()?.TextureKey ?? "";
            var isCap = texKey.Contains("base", StringComparison.OrdinalIgnoreCase) && el.Faces.ContainsKey("up");
            var isSide = !texKey.Contains("base", StringComparison.OrdinalIgnoreCase) && el.Faces.ContainsKey("north");
            if (!isCap && !isSide)
            {
                floatOffset += CountElementFloats(el, stride);
                continue;
            }

            TransformWorldCorners(el, out var cMin, out var cMax);
            var isTopCap = cMax.Y > 8f;
            var vertCount = CountElementFloats(el, stride);
            for (var vi = 0; vi < vertCount / stride; vi++)
            {
                var y = verts[floatOffset + vi * stride + 1];
                if (isCap && isTopCap)
                {
                    capTopYs.Add(y);
                }
                else if (isSide)
                {
                    sideTopYs.Add(y);
                }
            }

            floatOffset += vertCount;
        }

        Assert.NotEmpty(sideTopYs);
        Assert.NotEmpty(capTopYs);
        var topOverlap = sideTopYs.Max() - capTopYs.Min();
        Assert.True(topOverlap >= -0.02f,
            $"top rim baked verts should overlap; sideTopMax={sideTopYs.Max():G3} capTopMin={capTopYs.Min():G3} overlap={topOverlap:G3}");
    }

    private static int CountElementFloats(ModelElement el, int stride)
    {
        var count = 0;
        foreach (var faceName in new[] { "north", "south", "west", "east", "up", "down" })
        {
            if (el.Faces.ContainsKey(faceName))
            {
                count += 4 * stride;
            }
        }

        return count;
    }

    [Fact]
    public void DecoratedPot_cap_and_side_faces_use_texcrop_uv_on_base_and_pattern()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var model), path);

        var capFaces = new List<float[]>();
        var sideFaces = new List<float[]>();
        foreach (var el in model.Elements)
        {
            foreach (var (faceName, face) in el.Faces)
            {
                if (face.Uv is not { Length: 4 })
                {
                    continue;
                }

                var texKey = face.TextureKey ?? "#skin";
                if (texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                    faceName.Equals("up", StringComparison.OrdinalIgnoreCase) &&
                    face.Uv[0] >= 13.5f && face.Uv[2] <= 28.5f &&
                    face.Uv[1] >= 12.5f && face.Uv[3] <= 27.5f)
                {
                    capFaces.Add(face.Uv);
                }
                else if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase) &&
                         faceName.Equals("north", StringComparison.OrdinalIgnoreCase))
                {
                    sideFaces.Add(face.Uv);
                }
            }
        }

        Assert.Equal(2, capFaces.Count);
        foreach (var uv in capFaces)
        {
            Assert.InRange(uv[0], 13.9f, 14.1f);
            Assert.InRange(uv[1], 12.9f, 13.1f);
            Assert.InRange(uv[2], 27.9f, 28.1f);
            Assert.InRange(uv[3], 26.9f, 27.1f);
        }

        Assert.Equal(4, sideFaces.Count);
        foreach (var uv in sideFaces)
        {
            Assert.InRange(uv[0], 0.9f, 1.1f);
            Assert.InRange(uv[1], -0.1f, 0.1f);
            Assert.InRange(uv[2], 14.9f, 15.1f);
            Assert.InRange(uv[3], 15.9f, 16.1f);
        }
    }

    [Fact]
    public void DecoratedPot_bake_atlas_uses_javap_per_texture_dimensions()
    {
        const string pattern = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        const string baseTex = "assets/minecraft/textures/entity/decorated_pot/decorated_pot_base.png";
        var provenance = new PreviewMeshProvenance(
            PreviewMeshDriverKind.RuntimeGeometryIrJson,
            "net.minecraft.client.model.DecoratedPotModel.previewComposite");

        Assert.Equal((16, 16), EntityGeometryIrTextureAtlas.ResolveForBake(pattern, 16, 16, provenance));
        Assert.Equal((32, 32), EntityGeometryIrTextureAtlas.ResolveForBake(baseTex, 32, 32, provenance));
        // Manifest rows still declare 64×64; bake must follow javap sheet sizes instead.
        Assert.Equal((16, 16), EntityGeometryIrTextureAtlas.ResolveForBake(pattern, 64, 64, provenance));
        Assert.Equal((32, 32), EntityGeometryIrTextureAtlas.ResolveForBake(baseTex, 64, 64, provenance));
    }

    [Fact]
    public void DecoratedPot_baked_side_normalized_uv_uses_16x16_atlas()
    {
        const string path = "assets/minecraft/textures/entity/decorated_pot/angler_pottery_pattern.png";
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(path, Profile26, 0f, 0f, out var mesh, out var provenance), path);

        var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(mesh, "minecraft");
        var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < ordered.Count; i++)
        {
            pathToIdx[ordered[i]] = i;
            var maps = CreatePreviewMaps(16, 16);
            if (ordered[i].Contains("decorated_pot_base", StringComparison.OrdinalIgnoreCase))
            {
                maps = CreatePreviewMaps(32, 32);
            }

            texSizes[ordered[i]] = EntityGeometryIrTextureAtlas.ResolveForBake(
                ordered[i],
                maps.Width,
                maps.Height,
                provenance);
        }

        Assert.True(MinecraftModelBaker.TryBake(mesh, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

        var sideEl = mesh.Elements.First(el =>
            !el.Faces.Values.Any(f => (f.TextureKey ?? "").Contains("base", StringComparison.OrdinalIgnoreCase)) &&
            el.Faces.ContainsKey("north"));
        var floatOffset = 0;
        const int stride = MinecraftModelBaker.FloatsPerVertex;
        for (var ei = 0; ei < mesh.Elements.Count; ei++)
        {
            if (!ReferenceEquals(mesh.Elements[ei], sideEl))
            {
                floatOffset += CountElementFloats(mesh.Elements[ei], stride);
                continue;
            }

            break;
        }

        var sideUs = new List<float>();
        var vertCount = CountElementFloats(sideEl, stride);
        for (var vi = 0; vi < vertCount / stride; vi++)
        {
            sideUs.Add(verts[floatOffset + vi * stride + 6]);
        }

        Assert.InRange(sideUs.Min(), 1f / 16f - 0.02f, 2f / 16f + 0.02f);
        Assert.InRange(sideUs.Max(), 14f / 16f - 0.02f, 15f / 16f + 0.02f);
    }
}
