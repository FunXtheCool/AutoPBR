using System.Numerics;
using System.Text.Json;

using AutoPBR.Core.Models;

namespace AutoPBR.Preview.Entities;

internal sealed partial class EntityModelRuntime
{
    private const float PoseEpsilon = 1e-5f;

    /// <summary>
    /// Walks geometry IR <c>roots</c> and emits cuboids through <see cref="EntityCuboid.Emit"/>.
    /// Body-layer / rest pose only; animation overlays belong in <see cref="GeometryIrMeshEmitOptions.TryGetPartPoseOverride"/>.
    /// Production use: call <see cref="TryBuildMeshFromGeometryIr"/> with a JVM name from <see cref="GeometryIrModelJvmNames"/>
    /// and emit options from <see cref="GeometryIrMeshEmitPresets"/> (Cod, Salmon, …).
    /// </summary>
    private static bool TryEmitGeometryIrBodyLayer(
        RigBuilder builder,
        JsonElement geometryRoot,
        in GeometryIrMeshEmitOptions options,
        out string? failureReason)
    {
        failureReason = null;
        var emitOptions = options;
        string? walkFailure = null;
        var ok = GeometryIrMeshWalk.WalkRoots(
            geometryRoot,
            emitOptions.RootTransform,
            emitOptions,
            ctx =>
            {
                if (!TryToEntityCuboid(ctx.Cuboid, ctx.PartId, emitOptions, out var entityCuboid, out var cuboidFailure))
                {
                    walkFailure = cuboidFailure;
                    return false;
                }

                var (layerKind, layerOrdinal, castsShadow) = PreviewDepthLayerClassifier.ClassifyIrCuboid(
                    ctx.PartId,
                    emitOptions.OfficialJvmName,
                    ctx.Cuboid,
                    ctx.CuboidIndexOnPart,
                    ctx.CuboidCountOnPart);
                entityCuboid = entityCuboid with
                {
                    DepthLayerKind = layerKind,
                    LayerOrdinal = layerOrdinal,
                    CastsShadow = castsShadow,
                };

                entityCuboid.Emit(builder, ctx.PartWorld, ctx.PartScale);
                return true;
            },
            onPartWorld: null,
            out walkFailure);
        failureReason = walkFailure;
        return ok;
    }

    internal static bool TryComposePartPosePublic(JsonElement pose, out Matrix4x4 matrix) =>
        TryComposePartPose(pose, Matrix4x4.Identity, out matrix, out _);

    internal static bool TryComposePartPosePublic(JsonElement pose, Matrix4x4 parentWorld, out Matrix4x4 matrix) =>
        TryComposePartPose(pose, parentWorld, out matrix, out _, partId: null);

    internal static bool TryComposePartPosePublic(
        JsonElement pose,
        Matrix4x4 parentWorld,
        out Matrix4x4 matrix,
        string? partId) =>
        TryComposePartPose(pose, parentWorld, out matrix, out _, partId: partId);

    internal static bool TryComposePartPoseOffsetAndRotationLocalTexel(
        JsonElement pose,
        out Matrix4x4 localTexel,
        out string? failureReason)
    {
        localTexel = Matrix4x4.Identity;
        failureReason = null;
        if (!TryReadPose(pose, out var tx, out var ty, out var tz, out var rx, out var ry, out var rz, out var order, out failureReason))
        {
            return false;
        }

        localTexel = ApplyPoseScale(
            EntityParityTemplate.PartPose(tx, ty, tz, rx, ry, rz, order),
            ReadUniformPoseScale(pose));
        return true;
    }

    internal static bool TryComposeLegacyPartPoseTexelPublic(JsonElement pose, out Matrix4x4 matrix, out string? failureReason) =>
        TryComposeLegacyPartPoseTexel(pose, out matrix, out failureReason);

    internal static bool TryComposeTranslationTimesRotationPartPosePublic(
        JsonElement pose,
        Matrix4x4 parentWorldBlock,
        out Matrix4x4 worldBlock,
        out string? failureReason)
    {
        if (!TryComposeLegacyPartPoseTexel(pose, out var localPartPose, out failureReason))
        {
            worldBlock = default;
            return false;
        }

        worldBlock = Matrix4x4.Multiply(localPartPose, parentWorldBlock);
        return true;
    }

    /// <summary>Vanilla bind pose <c>ModelPart.translateAndRotate</c> local delta in block space (PoseStack /16).</summary>
    internal static bool TryComposePartRenderLocalBlock(
        JsonElement pose,
        out Matrix4x4 localBlock,
        out string? failureReason)
    {
        localBlock = Matrix4x4.Identity;
        failureReason = null;

        if (!TryReadPose(pose, out var tx, out var ty, out var tz, out var rx, out var ry, out var rz, out var order, out failureReason))
        {
            return false;
        }

        _ = order;
        localBlock = ApplyPoseScale(
            EntityParityTemplate.ModelPartRenderLocalBlock(tx, ty, tz, rx, ry, rz),
            ReadUniformPoseScale(pose));
        return true;
    }

    internal static Matrix4x4 ModelPartRenderChildTexel(
        Matrix4x4 parentWorldTexel,
        float txTexel,
        float tyTexel,
        float tzTexel,
        float xRad = 0f,
        float yRad = 0f,
        float zRad = 0f)
    {
        var parentBlock = TexelRowAffineToBlock(parentWorldTexel);
        var localBlock = EntityParityTemplate.ModelPartRenderLocalBlock(txTexel, tyTexel, tzTexel, xRad, yRad, zRad);
        return BlockRowAffineToTexel(Matrix4x4.Multiply(localBlock, parentBlock));
    }

    private static bool TryComposePartPose(
        JsonElement pose,
        Matrix4x4 parentWorld,
        out Matrix4x4 matrix,
        out string? failureReason,
        bool useColumnTranslationTimesRotation = false,
        string? partId = null)
    {
        if (useColumnTranslationTimesRotation)
        {
            return TryComposeColumnPartPose(pose, parentWorld, out matrix, out failureReason);
        }

        if (partId is not null &&
            partId.Contains("horn", StringComparison.OrdinalIgnoreCase) &&
            PoseHasNonZeroRotation(pose))
        {
            return TryComposeColumnPartPose(pose, parentWorld, out matrix, out failureReason);
        }

        if (EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose)
        {
            return TryComposeLegacyPartPoseTexel(pose, out matrix, out failureReason);
        }

        if (!TryComposePartRenderLocalBlock(pose, out var localBlock, out failureReason))
        {
            matrix = Matrix4x4.Identity;
            return false;
        }

        var parentBlock = TexelRowAffineToBlock(parentWorld);
        matrix = BlockRowAffineToTexel(Matrix4x4.Multiply(localBlock, parentBlock));
        return true;
    }

    /// <summary>
    /// Java <c>PartPose.offsetAndRotation</c> chain in texel space. PoseStack stores the equivalent column chain as
    /// <c>parent * T * R</c>; with <see cref="Matrix4x4"/> row-vector transforms this must be <c>(R * T) * parent</c>.
    /// </summary>
    internal static bool PoseHasNonZeroRotation(JsonElement pose)
    {
        if (!pose.ValueKind.Equals(JsonValueKind.Object) ||
            !pose.TryGetProperty("rotationEulerRad", out var r) ||
            r.ValueKind != JsonValueKind.Array ||
            r.GetArrayLength() < 3)
        {
            return false;
        }

        const float eps = 1e-5f;
        return MathF.Abs((float)r[0].GetDouble()) > eps ||
               MathF.Abs((float)r[1].GetDouble()) > eps ||
               MathF.Abs((float)r[2].GetDouble()) > eps;
    }

    internal static bool ShouldUseColumnPartPoseCompose(JsonElement pose, in GeometryIrMeshEmitOptions options)
    {
        if (GeometryIrEmitPolicy.UsesObjectEntityModelPartPoseCompose(options.OfficialJvmName) ||
            GeometryIrEmitPolicy.UsesModelPartTranslateAndRotateBindPoseJvm(options.OfficialJvmName) ||
            (UsesFlatPartPoseOffsetQuadrupedJvm(options.OfficialJvmName) &&
             !IsPolarBearGeometryIrJvm(options.OfficialJvmName)))
        {
            return false;
        }

        if (options.ResolveUseColumnTranslationTimesRotationPartPose())
        {
            return true;
        }

        if (!PoseHasNonZeroRotation(pose))
        {
            return false;
        }

        // Flat root-sibling quadrupeds use ModelPart.translateAndRotate at bind unless the part carries
        // a non-zero PartPose rotation (cow/polar/chicken torso Rx π/2) where column Er×T matches Java.
        if (UsesFlatPartPoseOffsetQuadrupedJvm(options.OfficialJvmName) && !PoseHasNonZeroRotation(pose))
        {
            return false;
        }

        var jvm = options.OfficialJvmName ?? "";
        return jvm.Contains(".animal.", StringComparison.Ordinal) ||
               jvm.Contains(".monster.", StringComparison.Ordinal);
    }

    internal static bool TryComposeColumnPartPose(
        JsonElement pose,
        Matrix4x4 parentWorldTexel,
        out Matrix4x4 worldTexel,
        out string? failureReason)
    {
        worldTexel = parentWorldTexel;
        failureReason = null;
        if (!pose.ValueKind.Equals(JsonValueKind.Object))
        {
            return true;
        }

        if (!TryReadPose(pose, out var tx, out var ty, out var tz, out var rx, out var ry, out var rz, out var order, out failureReason))
        {
            return false;
        }

        var local = ApplyPoseScale(
            EntityParityTemplate.PartPose(tx, ty, tz, rx, ry, rz, order),
            ReadUniformPoseScale(pose));
        worldTexel = EntityParityTemplate.Child(parentWorldTexel, local);
        return true;
    }

    private static Matrix4x4 ApplyPoseScale(Matrix4x4 localPose, float uniformScale) =>
        MathF.Abs(uniformScale - 1f) <= PoseEpsilon
            ? localPose
            : Matrix4x4.Multiply(Matrix4x4.CreateScale(uniformScale), localPose);

    private static float ReadUniformPoseScale(JsonElement pose) =>
        pose.ValueKind == JsonValueKind.Object &&
        pose.TryGetProperty("uniformScale", out var scaleEl) &&
        scaleEl.ValueKind == JsonValueKind.Number
            ? (float)scaleEl.GetDouble()
            : 1f;

    private static bool TryComposeLegacyPartPoseTexel(JsonElement pose, out Matrix4x4 matrix, out string? failureReason)
    {
        matrix = Matrix4x4.Identity;
        failureReason = null;

        if (!TryReadPose(pose, out var tx, out var ty, out var tz, out var rx, out var ry, out var rz, out var order, out failureReason))
        {
            return false;
        }

        var translation = EntityParityTemplate.T(tx, ty, tz);
        var rotation = EntityParityTemplate.ComposeEuler(order, rx, ry, rz);
        matrix = ApplyPoseScale(
            EntityParityTemplate.Mul(translation, rotation),
            ReadUniformPoseScale(pose));
        return true;
    }
    internal static Matrix4x4 BlockRowAffineToTexel(Matrix4x4 blockRow) =>
        blockRow with
        {
            M41 = blockRow.M41 * 16f,
            M42 = blockRow.M42 * 16f,
            M43 = blockRow.M43 * 16f,
        };

    internal static Matrix4x4 TexelRowAffineToBlock(Matrix4x4 texelRow) =>
        texelRow with
        {
            M41 = texelRow.M41 / 16f,
            M42 = texelRow.M42 / 16f,
            M43 = texelRow.M43 / 16f,
        };

    private static bool TryReadPose(
        JsonElement pose,
        out float tx,
        out float ty,
        out float tz,
        out float rx,
        out float ry,
        out float rz,
        out string? order,
        out string? failureReason)
    {
        tx = ty = tz = rx = ry = rz = 0f;
        order = "XYZ";
        failureReason = null;

        if (pose.TryGetProperty("translation", out var t) && t.GetArrayLength() >= 3)
        {
            tx = (float)t[0].GetDouble();
            ty = (float)t[1].GetDouble();
            tz = (float)t[2].GetDouble();
        }

        if (pose.TryGetProperty("rotationEulerRad", out var r) && r.GetArrayLength() >= 3)
        {
            rx = (float)r[0].GetDouble();
            ry = (float)r[1].GetDouble();
            rz = (float)r[2].GetDouble();
        }

        order = pose.TryGetProperty("eulerOrder", out var orderEl) ? orderEl.GetString() : "XYZ";
        var supportedOrders = new HashSet<string>(StringComparer.Ordinal)
        {
            "XYZ", "XZY", "YXZ", "YZX", "ZXY", "ZYX"
        };
        if (order is not null && !supportedOrders.Contains(order))
        {
            failureReason = $"unsupported eulerOrder '{order}'";
            return false;
        }

        return true;
    }

    internal static bool TryToEntityCuboidForTests(
        JsonElement cuboid,
        in GeometryIrMeshEmitOptions options,
        out EntityCuboid entityCuboid,
        out string? failureReason) =>
        TryToEntityCuboid(cuboid, "", options, out entityCuboid, out failureReason);

    private static bool TryToEntityCuboid(
        JsonElement cuboid,
        string partId,
        in GeometryIrMeshEmitOptions options,
        out EntityCuboid entityCuboid,
        out string? failureReason)
    {
        failureReason = null;
        entityCuboid = default;

        if (!cuboid.TryGetProperty("from", out var from) || from.GetArrayLength() < 3 ||
            !cuboid.TryGetProperty("to", out var to) || to.GetArrayLength() < 3 ||
            !cuboid.TryGetProperty("uvOrigin", out var uv) || uv.GetArrayLength() < 2)
        {
            failureReason = "cuboid missing from/to/uvOrigin";
            return false;
        }

        var x0 = (float)from[0].GetDouble();
        var y0 = (float)from[1].GetDouble();
        var z0 = (float)from[2].GetDouble();
        var x1 = (float)to[0].GetDouble();
        var y1 = (float)to[1].GetDouble();
        var z1 = (float)to[2].GetDouble();

        // UV footprint follows lifted IR texOffs/uvSpan — not preview-only thicken or axolotl gill Z-expand.
        var irLogicalW = (int)MathF.Round(MathF.Abs(x1 - x0));
        var irLogicalH = (int)MathF.Round(MathF.Abs(y1 - y0));
        var irLogicalD = (int)MathF.Round(MathF.Abs(z1 - z0));

        var inflate = GeometryIrCuboidMetadata.ApplyCubeDeformationInflateForEmit(
            cuboid, options, ref x0, ref y0, ref z0, ref x1, ref y1, ref z1);

        if (options.PreviewDegenerateAxisThickness > 0f)
        {
            _ = GeometryIrEmitPolicy.TryExpandAxolotlGillCuboidZExtents(
                options.OfficialJvmName, partId, ref z0, ref z1);
        }

        GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var previewFaceMask);
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

        var texU = uv[0].GetInt32();
        var texV = uv[1].GetInt32();

        var mirror = GeometryIrCuboidMetadata.GetMirrorCuboidUv(cuboid);
        var uw = -1;
        var uh = -1;
        var ud = -1;
        var hasUvSpan = GeometryIrCuboidMetadata.TryGetUvSpan(cuboid, out var spanW, out var spanH, out var spanD);
        if (hasUvSpan)
        {
            (uw, uh, var resolvedD) = GeometryIrUvAtlasQuality.ResolveTexCropUvFootprint(
                cuboid,
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
        else if (inflate != 0f)
        {
            var logicalW = Math.Max(1, (int)MathF.Round(MathF.Abs((float)to[0].GetDouble() - (float)from[0].GetDouble())));
            var logicalH = Math.Max(1, (int)MathF.Round(MathF.Abs((float)to[1].GetDouble() - (float)from[1].GetDouble())));
            var logicalD = Math.Max(1, (int)MathF.Round(MathF.Abs((float)to[2].GetDouble() - (float)from[2].GetDouble())));
            if (GeometryIrEmitPolicy.GetInflateUvFootprint(options.OfficialJvmName) ==
                GeometryIrEmitPolicy.InflateUvFootprint.PostInflateMeshExtents)
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

        string[]? faceMaskArray = null;
        if (GeometryIrCuboidMetadata.TryGetFaceMask(cuboid, out var faceMask))
        {
            faceMaskArray = faceMask;
        }

        string? textureKey = null;
        if (GeometryIrCuboidMetadata.TryGetTextureKey(cuboid, out var tk))
        {
            textureKey = tk;
        }

        if (!string.IsNullOrEmpty(partId) && options.ResolvePartTextureKey?.Invoke(partId) is { } partTexKey)
        {
            textureKey = partTexKey;
        }

        if (faceMaskArray is { Length: > 0 })
        {
            if (IsNorthSouthFaceMaskOnly(faceMaskArray))
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
            else if (IsUpDownFaceMaskOnly(faceMaskArray))
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

        // ModelPart.Cube (26.1.2 javap) always lays out north/south with the standard cube cross-unfold at d=0;
        // inferred lift uvSpan footprints must not switch to texCrop opposite-face gap layout.
        var texCropNorthSouthFaceUv = false;

        var cuboidRx = 0f;
        var cuboidRy = 0f;
        var cuboidRz = 0f;
        if (cuboid.TryGetProperty("cuboidRotationEulerRad", out var cuboidRot) &&
            cuboidRot.ValueKind == JsonValueKind.Array &&
            cuboidRot.GetArrayLength() >= 3)
        {
            cuboidRx = (float)cuboidRot[0].GetDouble();
            cuboidRy = (float)cuboidRot[1].GetDouble();
            cuboidRz = (float)cuboidRot[2].GetDouble();
        }

        Vector3? rotationPivot = null;
        if (cuboid.TryGetProperty("rotationPivot", out var pivot) &&
            pivot.ValueKind == JsonValueKind.Array &&
            pivot.GetArrayLength() >= 3)
        {
            rotationPivot = new Vector3(
                (float)pivot[0].GetDouble(),
                (float)pivot[1].GetDouble(),
                (float)pivot[2].GetDouble());
        }

        entityCuboid = new EntityCuboid(
            x0, y0, z0, x1, y1, z1,
            texU, texV,
            uw, uh, ud,
            MirrorUv: mirror,
            XRot: cuboidRx,
            YRot: cuboidRy,
            ZRot: cuboidRz,
            FaceMask: faceMaskArray,
            TextureKey: textureKey)
        {
            RotationPivot = rotationPivot,
            TexCropNorthSouthFaceUv = texCropNorthSouthFaceUv,
        };
        return true;
    }

    private static bool TryBuildMeshFromGeometryIr(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        string officialJvmName,
        in GeometryIrMeshEmitOptions options,
        out string? failureReason) =>
        TryBuildMeshFromGeometryIrOrCodegen(builder, profile, officialJvmName, options, out failureReason);

    private static bool TryBuildCodMeshFromGeometryIr(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway) =>
        TryBuildMeshFromGeometryIr(
            builder,
            profile,
            GeometryIrModelJvmNames.Cod,
            GeometryIrMeshEmitPresets.ForCod(p, tailSway),
            out _);

    private static bool TryBuildSalmonMeshFromGeometryIr(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway) =>
        TryBuildMeshFromGeometryIr(
            builder,
            profile,
            GeometryIrModelJvmNames.Salmon,
            GeometryIrMeshEmitPresets.ForSalmon(p, tailSway),
            out _);

    internal static bool TryBuildCodGeometryIrMesh(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway,
        in GeometryIrMeshEmitOptions options,
        out string? failureReason) =>
        TryBuildMeshFromGeometryIr(
            builder,
            profile,
            GeometryIrModelJvmNames.Cod,
            options,
            out failureReason);

    internal static bool TryBuildSalmonGeometryIrMesh(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway,
        in GeometryIrMeshEmitOptions options,
        out string? failureReason) =>
        TryBuildMeshFromGeometryIr(
            builder,
            profile,
            GeometryIrModelJvmNames.Salmon,
            options,
            out failureReason);

    /// <summary>Geometry IR Cod mesh (IR-fidelity emit: lifted box extents, no hand overrides).</summary>
    internal static MergedJavaBlockModel? TryBuildCodGeometryIrMeshForTests(
        string texRef,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway,
        out string? failureReason) =>
        TryBuildCodGeometryIrMeshForTests(texRef, profile, p, tailSway, preferCodegen: false, out failureReason);

    internal static MergedJavaBlockModel? TryBuildCodGeometryIrMeshForTests(
        string texRef,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway,
        bool preferCodegen,
        out string? failureReason)
    {
        var b = new RigBuilder(32, 32);
        var baseOpts = GeometryIrMeshEmitPresets.ForCodIrFidelity(p, tailSway);
        var opts = preferCodegen
            ? baseOpts with { PreferCodegenCuboids = true, Fidelity = GeometryIrEmitFidelity.Parity }
            : baseOpts;
        if (!TryBuildCodGeometryIrMesh(b, profile, p, tailSway, opts, out failureReason))
        {
            return null;
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Geometry IR Salmon mesh (IR-fidelity emit).</summary>
    internal static MergedJavaBlockModel? TryBuildSalmonGeometryIrMeshForTests(
        string texRef,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway,
        out string? failureReason)
    {
        var b = new RigBuilder(32, 32);
        if (!TryBuildSalmonGeometryIrMesh(
                b,
                profile,
                p,
                tailSway,
                GeometryIrMeshEmitPresets.ForSalmonIrFidelity(p, tailSway),
                out failureReason))
        {
            return null;
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Viewport fidelity emit for mob-family tests (zero-axis thicken when applicable).</summary>
    internal static MergedJavaBlockModel? TryBuildGeometryIrViewportMeshForTests(
        string texRef,
        MinecraftNativeProfile profile,
        string officialJvmName,
        int atlasWidth,
        int atlasHeight,
        out string? failureReason)
    {
        var b = new RigBuilder(atlasWidth, atlasHeight);
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasWidth,
            AtlasHeight = atlasHeight,
            Fidelity = GeometryIrEmitFidelity.Viewport,
            PreviewDegenerateAxisThickness = 0.08f,
            OfficialJvmName = officialJvmName,
            PreferCodegenCuboids = string.Equals(officialJvmName, GeometryIrModelJvmNames.Cod, StringComparison.Ordinal) ||
                                   string.Equals(officialJvmName, GeometryIrModelJvmNames.Salmon, StringComparison.Ordinal),
        };
        if (!TryBuildMeshFromGeometryIr(b, profile, officialJvmName, options, out failureReason))
        {
            return null;
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>Generic parity-fidelity emit for mob-family golden tests (pilot JVM names from geometry index).</summary>
    internal static MergedJavaBlockModel? TryBuildGeometryIrParityMeshForTests(
        string texRef,
        MinecraftNativeProfile profile,
        string officialJvmName,
        int atlasWidth,
        int atlasHeight,
        out string? failureReason,
        JsonElement? geometryRootOverride = null)
    {
        var lerPlan = ResolveGeometryIrParityEmitPlan(
            officialJvmName,
            stemLower: null,
            normalizedAssetPath: null,
            deferLivingEntityRendererUntilAfterMotionPasses: false);
        var b = new RigBuilder(atlasWidth, atlasHeight);
        var options = ApplyLivingEntityRendererEmitPlan(
            new GeometryIrMeshEmitOptions
            {
                RootTransform = Matrix4x4.Identity,
                DefaultPartScale = 1f,
                AtlasWidth = atlasWidth,
                AtlasHeight = atlasHeight,
                Fidelity = GeometryIrEmitFidelity.Parity,
                PreviewDegenerateAxisThickness = 0f,
            }
                .WithOfficialJvmPoseComposeDefaults(officialJvmName)
                with
            { OfficialJvmName = officialJvmName },
            lerPlan);
        if (geometryRootOverride is { } overrideRoot)
        {
            if (!TryEmitGeometryIrBodyLayer(b, overrideRoot, options, out failureReason))
            {
                return null;
            }

            return FinishGeometryIrMeshLivingEntityRendererBasis(b.Build(texRef), lerPlan);
        }

        if (!TryBuildMeshFromGeometryIr(b, profile, officialJvmName, options, out failureReason))
        {
            return null;
        }

        return FinishGeometryIrMeshLivingEntityRendererBasis(b.Build(texRef), lerPlan);
    }

    /// <summary>
    /// Test-only parity emit with explicit LER multiply order (see ApplyLivingEntityRendererPreviewBasis with bool overload).
    /// </summary>
    internal static MergedJavaBlockModel? TryBuildGeometryIrParityMeshForTestsWithLerCompose(
        string texRef,
        string officialJvmName,
        int atlasWidth,
        int atlasHeight,
        JsonElement geometryRootOverride,
        bool lerMirrorRightComposeLocalChain,
        out string? failureReason)
    {
        var b = new RigBuilder(atlasWidth, atlasHeight);
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasWidth,
            AtlasHeight = atlasHeight,
            Fidelity = GeometryIrEmitFidelity.Parity,
            PreviewDegenerateAxisThickness = 0f,
            OfficialJvmName = officialJvmName,
        };
        if (!TryEmitGeometryIrBodyLayer(b, geometryRootOverride, options, out failureReason))
        {
            return null;
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef), lerMirrorRightComposeLocalChain);
    }
}
