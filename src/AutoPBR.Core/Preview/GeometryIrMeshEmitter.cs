using System.Numerics;
using System.Text.Json;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
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
        TryComposePartPose(pose, parentWorld, out matrix, out _);

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
        localBlock = EntityParityTemplate.ModelPartRenderLocalBlock(tx, ty, tz, rx, ry, rz);
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
        bool useColumnTranslationTimesRotation = false)
    {
        if (useColumnTranslationTimesRotation)
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

        var local = EntityParityTemplate.PartPose(tx, ty, tz, rx, ry, rz, order);
        worldTexel = EntityParityTemplate.Child(parentWorldTexel, local);
        return true;
    }

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
        matrix = EntityParityTemplate.Mul(translation, rotation);
        return true;
    }
    internal static Matrix4x4 BlockRowAffineToTexel(Matrix4x4 blockRow) =>
        new(
            blockRow.M11, blockRow.M12, blockRow.M13, blockRow.M14,
            blockRow.M21, blockRow.M22, blockRow.M23, blockRow.M24,
            blockRow.M31, blockRow.M32, blockRow.M33, blockRow.M34,
            blockRow.M41 * 16f, blockRow.M42 * 16f, blockRow.M43 * 16f, blockRow.M44);

    internal static Matrix4x4 TexelRowAffineToBlock(Matrix4x4 texelRow) =>
        new(
            texelRow.M11, texelRow.M12, texelRow.M13, texelRow.M14,
            texelRow.M21, texelRow.M22, texelRow.M23, texelRow.M24,
            texelRow.M31, texelRow.M32, texelRow.M33, texelRow.M34,
            texelRow.M41 / 16f, texelRow.M42 / 16f, texelRow.M43 / 16f, texelRow.M44);

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

        var inflate = GeometryIrCuboidMetadata.ApplyCubeDeformationInflateIfNonParity(
            cuboid, options.Fidelity, ref x0, ref y0, ref z0, ref x1, ref y1, ref z1);

        _ = options.PreviewDegenerateAxisThickness > 0f &&
            GeometryIrEmitPolicy.TryExpandAxolotlGillCuboidZExtents(
                options.OfficialJvmName, partId, ref z0, ref z1);

        if (options.PreviewDegenerateAxisThickness > 0f)
        {
            ApplyPreviewDegenerateAxisThickness(
                ref x0, ref y0, ref z0, ref x1, ref y1, ref z1,
                options.PreviewDegenerateAxisThickness);
        }

        GeometryIrEmitPolicy.TryReorientGhastFamilyTentacleCuboidYForModelSpace(
            options.OfficialJvmName,
            partId,
            ref y0,
            ref y1);

        var texU = uv[0].GetInt32();
        var texV = uv[1].GetInt32();
        var atlasW = options.AtlasWidth;
        var atlasH = options.AtlasHeight;
        if (GeometryIrCuboidMetadata.TryGetAtlasDimensions(cuboid, out var cuboidAtlasW, out var cuboidAtlasH))
        {
            atlasW = cuboidAtlasW;
            atlasH = cuboidAtlasH;
        }
        else if (!string.IsNullOrEmpty(partId) &&
                 options.ResolvePartAtlasDimensions?.Invoke(partId) is { Width: > 0, Height: > 0 } partAtlas)
        {
            atlasW = partAtlas.Width;
            atlasH = partAtlas.Height;
        }

        NormalizeAtlasUv(atlasW, atlasH, ref texU, ref texV);

        var mirror = GeometryIrCuboidMetadata.GetMirrorCuboidUv(cuboid);
        var uw = -1;
        var uh = -1;
        var ud = -1;
        if (GeometryIrCuboidMetadata.TryGetUvSpan(cuboid, out var spanW, out var spanH, out var spanD))
        {
            uw = spanW;
            uh = spanH;
            ud = spanD >= 0 ? spanD : -1;
        }
        else if (inflate > 0f)
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

        _ = GeometryIrEmitPolicy.TryApplyGhastFamilyCuboidUvFootprint(
            options.OfficialJvmName, partId, y0, y1, ref uw, ref uh, ref ud);

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
            mirror,
            XRot: cuboidRx,
            YRot: cuboidRy,
            ZRot: cuboidRz,
            FaceMask: faceMaskArray,
            TextureKey: textureKey)
        {
            RotationPivot = rotationPivot
        };
        return true;
    }

    private static void ApplyPreviewDegenerateAxisThickness(
        ref float x0, ref float y0, ref float z0,
        ref float x1, ref float y1, ref float z1,
        float thickness)
    {
        ExpandAxisIfDegenerate(ref x0, ref x1, thickness);
        ExpandAxisIfDegenerate(ref y0, ref y1, thickness);
        ExpandAxisIfDegenerate(ref z0, ref z1, thickness);
    }

    private static void ExpandAxisIfDegenerate(ref float a0, ref float a1, float thickness)
    {
        if (MathF.Abs(a1 - a0) >= PoseEpsilon)
        {
            return;
        }

        var mid = (a0 + a1) * 0.5f;
        // Zero-thickness IR axis → visible preview sheet (span 2×thickness centered on lifted coordinate).
        a0 = mid - thickness;
        a1 = mid + thickness;
    }

    /// <summary>Wrap negative <c>texOffs</c> origins onto the entity atlas (e.g. Cod top fin V -6 on 32px sheet → 26).</summary>
    private static void NormalizeAtlasUv(int atlasW, int atlasH, ref int texU, ref int texV)
    {
        if (texU < 0)
        {
            texU += atlasW;
        }

        if (texV < 0)
        {
            texV += atlasH;
        }
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
        float tailSway,
        out string? failureReason) =>
        TryBuildMeshFromGeometryIr(
            builder,
            profile,
            GeometryIrModelJvmNames.Cod,
            GeometryIrMeshEmitPresets.ForCod(p, tailSway),
            out failureReason);

    private static bool TryBuildSalmonMeshFromGeometryIr(
        RigBuilder builder,
        MinecraftNativeProfile profile,
        BabyProfile p,
        float tailSway,
        out string? failureReason) =>
        TryBuildMeshFromGeometryIr(
            builder,
            profile,
            GeometryIrModelJvmNames.Salmon,
            GeometryIrMeshEmitPresets.ForSalmon(p, tailSway),
            out failureReason);

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
                with { OfficialJvmName = officialJvmName },
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
    /// Test-only parity emit with explicit LER multiply order (see <see cref="ApplyLivingEntityRendererPreviewBasis"/>).
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
