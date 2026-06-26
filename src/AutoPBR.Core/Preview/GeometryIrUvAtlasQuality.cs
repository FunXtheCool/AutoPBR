using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Quality gates for cuboid <c>uvOrigin</c> footprints vs shard or per-cuboid atlas dimensions
/// (catches multi-<c>LayerDefinition</c> lifts merged under one <c>textureWidth</c>/<c>textureHeight</c>).
/// </summary>
public static class GeometryIrUvAtlasQuality
{
    /// <summary>Gap between opposite texCrop direction-mask faces on U (north/south and up/down templates).</summary>
    internal const int TexCropOppositeFaceDepthGap = 2;

    public sealed record Result(bool? UvWithinAtlasMatch, string? Message, bool LayerAtlasConsistent, string? LayerAtlasMessage);

    public static Result Evaluate(JsonElement shardRoot)
    {
        if (!TryGetShardAtlas(shardRoot, out var shardW, out var shardH))
        {
            return new Result(null, "shard missing textureWidth/textureHeight", true, null);
        }

        var offenders = new List<string>();
        var layerSizes = new Dictionary<string, (int W, int H)>(StringComparer.Ordinal);
        var layerAtlasConsistent = true;
        string? layerMessage = null;

        if (shardRoot.TryGetProperty("roots", out var roots) && roots.ValueKind == JsonValueKind.Array)
        {
            foreach (var root in roots.EnumerateArray())
            {
                WalkPart(root, shardW, shardH, offenders, layerSizes, ref layerAtlasConsistent, ref layerMessage);
            }
        }

        if (offenders.Count == 0)
        {
            return new Result(true, null, layerAtlasConsistent, layerMessage);
        }

        var msg = offenders.Count == 1
            ? offenders[0]
            : $"{offenders.Count} cuboids exceed atlas: {offenders[0]}";
        return new Result(false, msg, layerAtlasConsistent, layerMessage);
    }

    private static void WalkPart(
        JsonElement part,
        int shardW,
        int shardH,
        List<string> offenders,
        Dictionary<string, (int W, int H)> layerSizes,
        ref bool layerAtlasConsistent,
        ref string? layerMessage)
    {
        if (part.TryGetProperty("cuboids", out var cuboids) && cuboids.ValueKind == JsonValueKind.Array)
        {
            var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "<part>" : "<part>";
            foreach (var cuboid in cuboids.EnumerateArray())
            {
                EvaluateCuboid(cuboid, partId, shardW, shardH, offenders, layerSizes, ref layerAtlasConsistent, ref layerMessage);
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkPart(ch, shardW, shardH, offenders, layerSizes, ref layerAtlasConsistent, ref layerMessage);
            }
        }
    }

    private static void EvaluateCuboid(
        JsonElement cuboid,
        string partId,
        int shardW,
        int shardH,
        List<string> offenders,
        Dictionary<string, (int W, int H)> layerSizes,
        ref bool layerAtlasConsistent,
        ref string? layerMessage)
    {
        if (!cuboid.TryGetProperty("uvOrigin", out var uv) || uv.GetArrayLength() < 2 ||
            !cuboid.TryGetProperty("from", out var from) || from.GetArrayLength() < 3 ||
            !cuboid.TryGetProperty("to", out var to) || to.GetArrayLength() < 3)
        {
            return;
        }

        var atlasW = shardW;
        var atlasH = shardH;
        var hasCuboidAtlas = GeometryIrCuboidMetadata.TryGetAtlasDimensions(cuboid, out var cuboidAtlasW, out var cuboidAtlasH);
        if (hasCuboidAtlas)
        {
            atlasW = cuboidAtlasW;
            atlasH = cuboidAtlasH;
        }

        GeometryIrCuboidMetadata.TryGetTextureKey(cuboid, out var textureKey);
        if (hasCuboidAtlas)
        {
            if (layerSizes.TryGetValue(textureKey, out var prior) &&
                (prior.W != atlasW || prior.H != atlasH))
            {
                layerAtlasConsistent = false;
                layerMessage ??=
                    $"textureKey {textureKey} has conflicting per-cuboid atlas sizes ({prior.W}x{prior.H} vs {atlasW}x{atlasH})";
            }
            else
            {
                layerSizes[textureKey] = (atlasW, atlasH);
            }
        }

        // Use raw texOffs like vanilla CubeListBuilder / BuildCubeUvLayout (negative origins are valid).
        var texU = uv[0].GetInt32();
        var texV = uv[1].GetInt32();

        var (w, h, d) = ResolveCuboidUvExtents(cuboid, from, to);
        var mirrorU = GeometryIrCuboidMetadata.GetMirrorCuboidUv(cuboid);
        var faces = GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var mask) && mask.Length > 0
            ? mask
            : ["north", "south", "east", "west", "up", "down"];
        if (!CuboidUvFootprintFitsAtlas(texU, texV, w, h, d, atlasW, atlasH, faces, mirrorU))
        {
            var (maxU, maxV) = ComputeUnfoldedUvMaxForFaceMask(texU, texV, w, h, d, faces, mirrorU);
            offenders.Add(
                $"{partId} uvOrigin=({uv[0].GetInt32()},{uv[1].GetInt32()}) footprint→{maxU}x{maxV} exceeds {atlasW}x{atlasH} atlas");
        }
    }

    /// <summary>Max texel corner of vanilla unfolded box UV (matches entity cube UV layout).</summary>
    internal static (int MaxU, int MaxV) ComputeUnfoldedUvMax(int u, int v, int w, int h, int d, bool mirrorU = false)
    {
        if (w == 0 || h == 0 || d == 0)
        {
            var faces = new List<string>(6);
            if (w > 0 && h > 0)
            {
                faces.Add("up");
                faces.Add("down");
            }

            if (w > 0 && d > 0)
            {
                faces.Add("north");
                faces.Add("south");
            }

            if (h > 0 && d > 0)
            {
                faces.Add("east");
                faces.Add("west");
            }

            if (faces.Count > 0)
            {
                return ComputeUnfoldedUvMaxForFaceMask(u, v, w, h, d, faces, mirrorU);
            }
        }

        return ComputeUnfoldedUvMaxForFaceMask(
            u,
            v,
            w,
            h,
            d,
            ["north", "south", "east", "west", "up", "down"],
            mirrorU);
    }

    /// <summary>
    /// Integer box dimensions for UV footprint checks. Uses <c>uvSpan</c> when present; otherwise vanilla
    /// integer extents without forcing degenerate axes to 1 (zero-thickness planes only unfold exposed faces).
    /// </summary>
    internal static (int W, int H, int D) ResolveCuboidUvExtents(JsonElement cuboid, JsonElement from, JsonElement to)
    {
        var logicalW = LogicalAxisExtent(from, to, 0);
        var logicalH = LogicalAxisExtent(from, to, 1);
        var logicalD = LogicalAxisExtent(from, to, 2);
        if (!GeometryIrCuboidMetadata.TryGetUvSpan(cuboid, out var spanW, out var spanH, out var spanD))
        {
            return (logicalW, logicalH, logicalD);
        }

        var texU = 0;
        var texV = 0;
        if (cuboid.TryGetProperty("uvOrigin", out var uv) && uv.GetArrayLength() >= 2)
        {
            texU = uv[0].GetInt32();
            texV = uv[1].GetInt32();
        }

        return ResolveTexCropUvFootprint(
            cuboid,
            texU,
            texV,
            spanW,
            spanH,
            spanD >= 0 ? spanD : logicalD,
            logicalW,
            logicalH,
            logicalD);
    }

    /// <summary>
    /// texCrop north/south direction-mask sheets at <c>d=0</c>: anchor on north, opposite on south with
    /// <see cref="TexCropOppositeFaceDepthGap"/> (not full cube cross-unfold).
    /// </summary>
    internal static (float[] North, float[] South) BuildTexCropNorthSouthFaceUvRects(int u, int v, int w, int h)
    {
        var north = new float[] { u, v, u + w, v + h };
        var oppositeU0 = u + w + TexCropOppositeFaceDepthGap;
        var south = new float[] { oppositeU0, v, oppositeU0 + w, v + h };
        return (north, south);
    }

    /// <summary>
    /// keeps the same UV bounds and applies mirror only when building polygons (see bake <c>MirrorCuboidUv</c>).
    /// </summary>
    internal static (float[] Up, float[] Down) BuildUpDownTexCropFaceUvRects(
        int u,
        int v,
        int w,
        int d,
        IReadOnlyList<string> faceMask,
        bool mirrorU = false)
    {
        _ = mirrorU;

        // Dragon membranes lift as faceMask:["up"] only; Java still draws the anchor crop on Direction.DOWN.
        if (faceMask.Count == 1 &&
            faceMask[0].Equals("up", StringComparison.OrdinalIgnoreCase))
        {
            var down = EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Down, u, v, w, 0, d);
            var up = EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Up, u, v, w, 0, d);
            return (up, down);
        }

        // Decorated pot caps: javap texOffs(-14,13).addBox(14,0,14) with faceMask:["down"] uses ModelPart.Cube DOWN
        // cross-unfold (u+d, v, u+d+w, v+d), not the texCrop [u,v,u+w,v+d] anchor used by dual-mask sheets.
        if (faceMask.Count == 1 &&
            faceMask[0].Equals("down", StringComparison.OrdinalIgnoreCase) &&
            d > 0 &&
            TryResolveDecoratedPotCapDownTexCropEmitU(u, v, w, d, out var emitU))
        {
            var down = EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Down, emitU, v, w, 0, d);
            var up = EntityCuboidJavaUvConvention.GetUvRect(
                EntityCuboidJavaUvConvention.JavaDirection.Up, emitU, v, w, 0, d);
            return (up, down);
        }

        // texCrop anchor: uvOrigin anchors the first entry in faceMask (dual-mask bee sheets, etc.).
        var anchor = new float[] { u, v, u + w, v + d };
        var oppositeU0 = u + w + TexCropOppositeFaceDepthGap;
        var opposite = new float[] { oppositeU0, v, oppositeU0 + w, v + d };

        var anchorUp = faceMask.Count > 0 &&
                       faceMask[0].Equals("up", StringComparison.OrdinalIgnoreCase);
        return anchorUp ? (anchor, opposite) : (opposite, anchor);
    }

    /// <summary>
    /// javap <c>texOffs(-14,13)</c> on 32×32 <c>#base</c>: DOWN slot is <see cref="EntityCuboidJavaUvConvention.GetUvRect"/>
    /// with emit U = <c>texU + d</c> when <c>texU &lt; 0</c>, or repaired from legacy wrapped <c>uvOrigin[18]</c>.
    /// </summary>
    internal static bool TryResolveDecoratedPotCapDownTexCropEmitU(
        int texU,
        int texV,
        int spanW,
        int spanD,
        out int emitU)
    {
        emitU = texU;
        if (spanW != 14 || spanD != 14 || texV != 13)
        {
            return false;
        }

        emitU = texU < 0
            ? texU + spanD
            : texU - spanD - (2 * TexCropOppositeFaceDepthGap);
        return true;
    }

    /// <summary>
    /// Resolves texCrop <c>uvSpan</c> emit footprint for direction-mask sheets (north/south or up/down).
    /// </summary>
    internal static (int W, int H, int D) ResolveTexCropUvFootprint(
        JsonElement cuboid,
        int texU,
        int texV,
        int spanW,
        int spanH,
        int spanD,
        int logicalW,
        int logicalH,
        int logicalD)
    {
        if (GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var faceMask) &&
            IsUpDownFaceMaskOnly(faceMask) &&
            logicalH == 0 &&
            spanW > 0 &&
            spanD >= 0)
        {
            return (spanW, 0, spanD);
        }

        return ResolveNorthSouthSheetUvFootprint(
            cuboid,
            texU,
            texV,
            spanW,
            spanH,
            spanD,
            logicalW,
            logicalH,
            logicalD);
    }

    /// <summary>
    /// Resolves UV atlas footprint dimensions for emit. TexCrop lifts may duplicate the crop anchor into
    /// <c>uvSpan</c>; north/south zero-depth sheets need geometry extents (see bee leg parts).
    /// </summary>
    internal static (int W, int H, int D) ResolveNorthSouthSheetUvFootprint(
        JsonElement cuboid,
        int texU,
        int texV,
        int spanW,
        int spanH,
        int spanD,
        int logicalW,
        int logicalH,
        int logicalD)
    {
        if (spanW == texU && spanH == texV &&
            logicalW > 0 &&
            logicalH > 0)
        {
            return (logicalW, logicalH, logicalD > 0 ? logicalD : spanD);
        }

        if (GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var faceMask) &&
            IsNorthSouthFaceMaskOnly(faceMask) &&
            logicalW > 0 &&
            logicalH > 0)
        {
            return (spanW, spanH, 0);
        }

        return (spanW, spanH, spanD);
    }

    private static int LogicalAxisExtent(JsonElement from, JsonElement to, int axis) =>
        (int)MathF.Round(MathF.Abs((float)to[axis].GetDouble() - (float)from[axis].GetDouble()));

    /// <summary>Max UV corner when only a subset of faces is emitted (direction-mask cuboids).</summary>
    internal static (int MaxU, int MaxV) ComputeUnfoldedUvMaxForFaceMask(
        int u,
        int v,
        int w,
        int h,
        int d,
        IReadOnlyList<string> faceMask,
        bool mirrorU = false)
    {
        var maxU = u;
        var maxV = v;
        foreach (var corner in EnumerateFaceMaskUvCorners(u, v, w, h, d, faceMask, mirrorU))
        {
            maxU = Math.Max(maxU, corner.U);
            maxV = Math.Max(maxV, corner.V);
        }

        return (maxU, maxV);
    }

    /// <summary>
    /// True when all unfolded face corners fit in atlas using modular UV (matches repeat/wrap sampling on entity sheets).
    /// </summary>
    internal static bool CuboidUvFootprintFitsAtlas(
        int texU,
        int texV,
        int w,
        int h,
        int d,
        int atlasW,
        int atlasH,
        IReadOnlyList<string> faces,
        bool mirrorU)
    {
        var us = new List<int>();
        var vs = new List<int>();
        foreach (var (cu, cv) in EnumerateFaceMaskUvCorners(texU, texV, w, h, d, faces, mirrorU))
        {
            us.Add(PositiveMod(cu, atlasW));
            vs.Add(PositiveMod(cv, atlasH));
        }

        if (us.Count == 0)
        {
            return true;
        }

        return us.Max() - us.Min() < atlasW && vs.Max() - vs.Min() < atlasH;
    }

    private static int PositiveMod(int value, int modulus)
    {
        var r = value % modulus;
        return r < 0 ? r + modulus : r;
    }

    private static bool IsNorthSouthFaceMaskOnly(IReadOnlyList<string> faceMask)
    {
        if (faceMask.Count == 0)
        {
            return false;
        }

        foreach (var name in faceMask)
        {
            if (!string.Equals(name, "north", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "south", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUpDownFaceMaskOnly(IReadOnlyList<string> faceMask)
    {
        if (faceMask.Count == 0)
        {
            return false;
        }

        foreach (var name in faceMask)
        {
            if (!string.Equals(name, "up", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "down", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static (int U0, int V0, int U1, int V1) RectToTuple(float[] rect) =>
        ((int)rect[0], (int)rect[1], (int)rect[2], (int)rect[3]);

    private static IEnumerable<(int U, int V)> EnumerateFaceMaskUvCorners(
        int u,
        int v,
        int w,
        int h,
        int d,
        IReadOnlyList<string> faceMask,
        bool mirrorU)
    {
        // Same layout as CleanRoomEntityRigBuilder.BuildCubeUvLayout (u0,v0,u1,v1 per face).
        foreach (var face in faceMask)
        {
            var (u0, v0, u1, v1) = face switch
            {
                "west" => (u, v + d, u + d, v + d + h),
                "north" when IsNorthSouthFaceMaskOnly(faceMask) && w > 0 && h > 0 && d == 0 =>
                    RectToTuple(EntityCuboidJavaUvConvention.GetUvRect(
                        EntityCuboidJavaUvConvention.JavaDirection.North, u, v, w, h, 0)),
                "south" when IsNorthSouthFaceMaskOnly(faceMask) && w > 0 && h > 0 && d == 0 =>
                    RectToTuple(EntityCuboidJavaUvConvention.GetUvRect(
                        EntityCuboidJavaUvConvention.JavaDirection.South, u, v, w, h, 0)),
                "down" when IsUpDownFaceMaskOnly(faceMask) && w > 0 && d > 0 && h == 0 =>
                    RectToTuple(BuildUpDownTexCropFaceUvRects(u, v, w, d, faceMask).Down),
                "up" when IsUpDownFaceMaskOnly(faceMask) && w > 0 && d > 0 && h == 0 =>
                    RectToTuple(BuildUpDownTexCropFaceUvRects(u, v, w, d, faceMask).Up),
                "north" => (u + d, v + d, u + d + w, v + d + h),
                "east" => (u + d + w, v + d, u + d + w + d, v + d + h),
                "south" => (u + d + w + d, v + d, u + d + w + d + w, v + d + h),
                "up" => (u + d + w, v + d, u + d + w + w, v),
                "down" => (u + d, v, u + d + w, v + d),
                _ => (0, 0, 0, 0)
            };

            if (mirrorU && u0 != u1)
            {
                (u0, u1) = (u1, u0);
            }

            yield return (u0, v0);
            yield return (u1, v1);
        }
    }

    /// <summary>Infers axis-aligned face mask for zero-thickness cuboids (preview/lift degenerate sheets).</summary>
    public static string[]? InferDegenerateFaceMask(int w, int h, int d)
    {
        if (w > 0 && h > 0 && d > 0)
        {
            return null;
        }

        if (d == 0 && w > 0 && h > 0)
        {
            return ["north", "south"];
        }

        if (h == 0 && w > 0 && d > 0)
        {
            return ["up", "down"];
        }

        if (w == 0 && h > 0 && d > 0)
        {
            return ["east", "west"];
        }

        return null;
    }

    /// <summary>Clamps a bytecode face mask to faces that still exist for non-zero logical extents.</summary>
    public static string[]? SanitizeFaceMaskForLogicalExtents(int w, int h, int d, string[] faceMask)
    {
        var inferred = InferDegenerateFaceMask(w, h, d);
        if (inferred is not null)
        {
            return inferred;
        }

        return faceMask.Length == 0 ? null : faceMask;
    }

    private static bool TryGetShardAtlas(JsonElement shardRoot, out int w, out int h)
    {
        w = h = 0;
        if (!shardRoot.TryGetProperty("textureWidth", out var tw) || tw.ValueKind != JsonValueKind.Number ||
            !shardRoot.TryGetProperty("textureHeight", out var th) || th.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        w = tw.GetInt32();
        h = th.GetInt32();
        return w > 0 && h > 0;
    }
}
