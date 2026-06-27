using System.Numerics;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>
    /// Emits body-layer meshes by walking IR part poses and consuming codegen cuboids in the same DFS order
    /// used by <c>codegen-entity-cuboids</c> (see <c>GeometryIrEntityCuboidTables.g.cs</c>).
    /// </summary>
    private static bool TryEmitGeometryIrBodyLayerFromCodegen(
        RigBuilder builder,
        JsonElement geometryRoot,
        ReadOnlySpan<EntityCuboid> codegenCuboids,
        in GeometryIrMeshEmitOptions options,
        out string? failureReason)
    {
        failureReason = null;
        if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            failureReason = "missing roots array";
            return false;
        }

        var cuboidIndex = 0;
        foreach (var rootPart in roots.EnumerateArray())
        {
            if (!VisitCodegenPart(builder, rootPart, options.RootTransform, codegenCuboids, ref cuboidIndex, options,
                    ref failureReason))
            {
                return false;
            }
        }

        if (cuboidIndex != codegenCuboids.Length)
        {
            failureReason =
                $"codegen cuboid count mismatch: table={codegenCuboids.Length} tree={cuboidIndex}";
            return false;
        }

        return true;
    }

    private static bool VisitCodegenPart(
        RigBuilder builder,
        JsonElement part,
        Matrix4x4 parentWorld,
        ReadOnlySpan<EntityCuboid> codegenCuboids,
        ref int cuboidIndex,
        in GeometryIrMeshEmitOptions options,
        ref string? failureReason)
    {
        var world = parentWorld;
        if (part.TryGetProperty("pose", out var poseEl))
        {
            if (!TryComposePartPose(
                    poseEl,
                    parentWorld,
                    out world,
                    out failureReason,
                    options.ResolveUseColumnTranslationTimesRotationPartPose()))
            {
                return false;
            }
        }

        var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        if (options.TryGetPartPoseOverride is { } poseOverride)
        {
            world = poseOverride(partId, world);
        }

        var partScale = options.ResolvePartScale?.Invoke(partId) ?? options.DefaultPartScale;

        if (part.TryGetProperty("cuboids", out var cuboids) && cuboids.ValueKind == JsonValueKind.Array)
        {
            foreach (var cuboidEl in cuboids.EnumerateArray())
            {
                if (GeometryIrCuboidMetadata.TryGetFaceMask(cuboidEl, out var emptyMask) && emptyMask.Length == 0)
                {
                    continue;
                }

                if (cuboidIndex >= codegenCuboids.Length)
                {
                    failureReason = "codegen cuboid index overflow";
                    return false;
                }

                var entityCuboid = ApplyCodegenEmitOptions(codegenCuboids[cuboidIndex], cuboidEl, partId, options);
                entityCuboid.Emit(builder, world, partScale);
                cuboidIndex++;
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in children.EnumerateArray())
            {
                if (!VisitCodegenPart(builder, child, world, codegenCuboids, ref cuboidIndex, options, ref failureReason))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static EntityCuboid ApplyCodegenEmitOptions(
        EntityCuboid tableCuboid,
        JsonElement cuboidEl,
        string partId,
        in GeometryIrMeshEmitOptions options)
    {
        var x0 = tableCuboid.X0;
        var y0 = tableCuboid.Y0;
        var z0 = tableCuboid.Z0;
        var x1 = tableCuboid.X1;
        var y1 = tableCuboid.Y1;
        var z1 = tableCuboid.Z1;

        var irLogicalW = (int)MathF.Round(MathF.Abs(tableCuboid.X1 - tableCuboid.X0));
        var irLogicalH = (int)MathF.Round(MathF.Abs(tableCuboid.Y1 - tableCuboid.Y0));
        var irLogicalD = (int)MathF.Round(MathF.Abs(tableCuboid.Z1 - tableCuboid.Z0));

        var inflate = GeometryIrCuboidMetadata.ApplyCubeDeformationInflateForEmit(
            cuboidEl, options, ref x0, ref y0, ref z0, ref x1, ref y1, ref z1);

        if (options.PreviewDegenerateAxisThickness > 0f)
        {
            _ = GeometryIrEmitPolicy.TryExpandAxolotlGillCuboidZExtents(
                options.OfficialJvmName, partId, ref z0, ref z1);
        }

        GeometryIrCuboidMetadata.TryGetFaceMask(cuboidEl, out var previewFaceMask);
        if (options.PreviewDegenerateAxisThickness > 0f)
        {
            if (IsDecoratedPotPreviewCompositeJvm(options.OfficialJvmName))
            {
                ApplyDecoratedPotPreviewSheetThickness(
                    ref x0, ref y0, ref z0, ref x1, ref y1, ref z1,
                    options.PreviewDegenerateAxisThickness,
                    previewFaceMask);
            }
            else
            {
                ApplyPreviewDegenerateAxisThicknessForFaceMask(
                    ref x0, ref y0, ref z0, ref x1, ref y1, ref z1,
                    options.PreviewDegenerateAxisThickness,
                    previewFaceMask);
            }
        }

        var texU = tableCuboid.TexU;
        var texV = tableCuboid.TexV;

        var mirror = GeometryIrCuboidMetadata.GetMirrorCuboidUv(cuboidEl) || tableCuboid.MirrorUv;
        var uw = tableCuboid.UvSizeW;
        var uh = tableCuboid.UvSizeH;
        var ud = tableCuboid.UvSizeD;
        var hasUvSpan = GeometryIrCuboidMetadata.TryGetUvSpan(cuboidEl, out var spanW, out var spanH, out var spanD);
        if (hasUvSpan)
        {
            (uw, uh, var resolvedD) = GeometryIrUvAtlasQuality.ResolveTexCropUvFootprint(
                cuboidEl,
                texU,
                texV,
                spanW,
                spanH,
                spanD >= 0 ? spanD : irLogicalD,
                irLogicalW,
                irLogicalH,
                irLogicalD);
            ud = resolvedD >= 0 ? resolvedD : -1;
        }
        else if (uw < 0 && uh < 0 && ud < 0 && inflate != 0f)
        {
            var footprint = GeometryIrEmitPolicy.GetInflateUvFootprint(options.OfficialJvmName);
            var logicalW = Math.Max(1, irLogicalW);
            var logicalH = Math.Max(1, irLogicalH);
            var logicalD = Math.Max(1, irLogicalD);
            if (footprint == GeometryIrEmitPolicy.InflateUvFootprint.PostInflateMeshExtents)
            {
                uw = Math.Max(1, (int)MathF.Round(MathF.Abs(x1 - x0)));
                uh = Math.Max(1, (int)MathF.Round(MathF.Abs(y1 - y0)));
                ud = Math.Max(1, (int)MathF.Round(MathF.Abs(z1 - z0)));
            }
            else
            {
                uw = logicalW;
                uh = logicalH;
                ud = logicalD;
            }
        }

        string[]? faceMask = null;
        if (GeometryIrCuboidMetadata.TryGetFaceMask(cuboidEl, out var faceMaskFromIr))
        {
            faceMask = faceMaskFromIr;
        }

        if (faceMask is { Length: > 0 })
        {
            if (IsNorthSouthFaceMaskOnly(faceMask))
            {
                if (uw <= 0 && irLogicalW > 0)
                {
                    uw = irLogicalW;
                }

                if (uh <= 0 && irLogicalH > 0)
                {
                    uh = irLogicalH;
                }

                ud = 0;
            }
            else if (IsUpDownFaceMaskOnly(faceMask))
            {
                if (uw <= 0 && irLogicalW > 0)
                {
                    uw = irLogicalW;
                }

                uh = 0;
                if (ud <= 0 && irLogicalD > 0)
                {
                    ud = irLogicalD;
                }
            }
        }

        var texCropNorthSouthFaceUv = false;

        string? textureKey = null;
        if (GeometryIrCuboidMetadata.TryGetTextureKey(cuboidEl, out var tk))
        {
            textureKey = tk;
        }

        return new EntityCuboid(x0, y0, z0, x1, y1, z1, texU, texV, uw, uh, ud, MirrorUv: mirror,
            FaceMask: faceMask, TextureKey: textureKey)
        {
            TexCropNorthSouthFaceUv = texCropNorthSouthFaceUv,
        };
    }

    internal static EntityCuboid ApplyCodegenEmitOptionsForTests(
        EntityCuboid tableCuboid,
        JsonElement cuboidEl,
        string partId,
        in GeometryIrMeshEmitOptions options) =>
        ApplyCodegenEmitOptions(tableCuboid, cuboidEl, partId, options);

    internal static bool TryBuildMeshFromGeometryIrForTests(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        string officialJvmName,
        in GeometryIrMeshEmitOptions options,
        out string? failureReason) =>
        TryBuildMeshFromGeometryIrOrCodegen(builder, profile, officialJvmName, options, out failureReason);

    private static bool TryBuildMeshFromGeometryIrOrCodegen(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        string officialJvmName,
        in GeometryIrMeshEmitOptions options,
        out string? failureReason)
    {
        failureReason = null;
        if (!GeometryIrDocumentLoader.TryLoad(profile, officialJvmName, out var geometryRoot))
        {
            failureReason = $"{officialJvmName} geometry shard not found";
            return false;
        }

        geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(officialJvmName, geometryRoot);
        var opts = options with { OfficialJvmName = officialJvmName };
        var emittedBefore = builder.EmittedElementCount;
        if (opts.PreferCodegenCuboids &&
            GeometryIrCodegenTables.TryGetBodyLayerSpan(officialJvmName, out var codegen) &&
            TryEmitGeometryIrBodyLayerFromCodegen(builder, geometryRoot, codegen, opts, out failureReason))
        {
            return true;
        }

        if (builder.EmittedElementCount > emittedBefore)
        {
            builder.ClearEmittedElements();
        }

        return TryEmitGeometryIrBodyLayer(builder, geometryRoot, opts, out failureReason);
    }
}
