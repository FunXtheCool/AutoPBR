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
            if (!TryComposePartPose(poseEl, out var local, out failureReason))
            {
                return false;
            }

            world = EntityParityTemplate.Mul(parentWorld, local);
        }

        var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        if (options.TryGetPartPoseOverride is { } poseOverride)
        {
            world = poseOverride(partId, world);
        }

        var partScale = options.ResolvePartScale?.Invoke(partId) ?? options.DefaultPartScale;
        if (part.TryGetProperty("pose", out var poseForScale) &&
            poseForScale.TryGetProperty("uniformScale", out var scaleEl) &&
            scaleEl.ValueKind == JsonValueKind.Number)
        {
            partScale *= (float)scaleEl.GetDouble();
        }

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

                var entityCuboid = ApplyCodegenEmitOptions(codegenCuboids[cuboidIndex], cuboidEl, options);
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
        in GeometryIrMeshEmitOptions options)
    {
        var x0 = tableCuboid.X0;
        var y0 = tableCuboid.Y0;
        var z0 = tableCuboid.Z0;
        var x1 = tableCuboid.X1;
        var y1 = tableCuboid.Y1;
        var z1 = tableCuboid.Z1;

        var inflate = GeometryIrCuboidMetadata.ApplyCubeDeformationInflateIfNonParity(
            cuboidEl, options.Fidelity, ref x0, ref y0, ref z0, ref x1, ref y1, ref z1);

        if (options.Fidelity != GeometryIrEmitFidelity.Parity &&
            options.PreviewDegenerateAxisThickness > 0f)
        {
            ApplyPreviewDegenerateAxisThickness(
                ref x0, ref y0, ref z0, ref x1, ref y1, ref z1,
                options.PreviewDegenerateAxisThickness);
        }

        var texU = tableCuboid.TexU;
        var texV = tableCuboid.TexV;
        NormalizeAtlasUv(options.AtlasWidth, options.AtlasHeight, ref texU, ref texV);

        var mirror = GeometryIrCuboidMetadata.GetMirrorCuboidUv(cuboidEl) || tableCuboid.MirrorUv;
        var uw = tableCuboid.UvSizeW;
        var uh = tableCuboid.UvSizeH;
        var ud = tableCuboid.UvSizeD;

        if (uw < 0 && uh < 0 && ud < 0 && inflate > 0f)
        {
            var footprint = GeometryIrEmitPolicy.GetInflateUvFootprint(options.OfficialJvmName);
            var logicalW = Math.Max(1, (int)MathF.Round(MathF.Abs(tableCuboid.X1 - tableCuboid.X0)));
            var logicalH = Math.Max(1, (int)MathF.Round(MathF.Abs(tableCuboid.Y1 - tableCuboid.Y0)));
            var logicalD = Math.Max(1, (int)MathF.Round(MathF.Abs(tableCuboid.Z1 - tableCuboid.Z0)));
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

        string? textureKey = null;
        if (GeometryIrCuboidMetadata.TryGetTextureKey(cuboidEl, out var tk))
        {
            textureKey = tk;
        }

        return new EntityCuboid(x0, y0, z0, x1, y1, z1, texU, texV, uw, uh, ud, mirror,
            FaceMask: faceMask, TextureKey: textureKey);
    }

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
