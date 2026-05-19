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
                if (!TryToEntityCuboid(ctx.Cuboid, emitOptions, out var entityCuboid, out var cuboidFailure))
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
        TryComposePartPose(pose, out matrix, out _);

    internal static bool TryToEntityCuboidForTests(
        JsonElement cuboid,
        in GeometryIrMeshEmitOptions options,
        out EntityCuboid entityCuboid,
        out string? failureReason) =>
        TryToEntityCuboid(cuboid, options, out entityCuboid, out failureReason);

    private static bool TryComposePartPose(JsonElement pose, out Matrix4x4 matrix, out string? failureReason)
    {
        matrix = Matrix4x4.Identity;
        failureReason = null;

        var tx = 0f;
        var ty = 0f;
        var tz = 0f;
        if (pose.TryGetProperty("translation", out var t) && t.GetArrayLength() >= 3)
        {
            tx = (float)t[0].GetDouble();
            ty = (float)t[1].GetDouble();
            tz = (float)t[2].GetDouble();
        }

        var rx = 0f;
        var ry = 0f;
        var rz = 0f;
        if (pose.TryGetProperty("rotationEulerRad", out var r) && r.GetArrayLength() >= 3)
        {
            rx = (float)r[0].GetDouble();
            ry = (float)r[1].GetDouble();
            rz = (float)r[2].GetDouble();
        }

        var order = pose.TryGetProperty("eulerOrder", out var orderEl) ? orderEl.GetString() : "XYZ";
        var supportedOrders = new HashSet<string>(StringComparer.Ordinal)
        {
            "XYZ", "XZY", "YXZ", "YZX", "ZXY", "ZYX"
        };
        if (order is not null && !supportedOrders.Contains(order))
        {
            failureReason = $"unsupported eulerOrder '{order}'";
            return false;
        }

        matrix = EntityParityTemplate.Mul(
            EntityParityTemplate.T(tx, ty, tz),
            EntityParityTemplate.ComposeEuler(order, rx, ry, rz));
        return true;
    }

    private static bool TryToEntityCuboid(
        JsonElement cuboid,
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

        if (options.Fidelity != GeometryIrEmitFidelity.Parity &&
            options.PreviewDegenerateAxisThickness > 0f)
        {
            ApplyPreviewDegenerateAxisThickness(
                ref x0, ref y0, ref z0, ref x1, ref y1, ref z1,
                options.PreviewDegenerateAxisThickness);
        }

        var texU = uv[0].GetInt32();
        var texV = uv[1].GetInt32();
        NormalizeAtlasUv(options.AtlasWidth, options.AtlasHeight, ref texU, ref texV);

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

        entityCuboid = new EntityCuboid(x0, y0, z0, x1, y1, z1, texU, texV, uw, uh, ud, mirror,
            FaceMask: faceMaskArray, TextureKey: textureKey);
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
        if (geometryRootOverride is { } overrideRoot)
        {
            if (!TryEmitGeometryIrBodyLayer(b, overrideRoot, options, out failureReason))
            {
                return null;
            }

            return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
        }

        if (!TryBuildMeshFromGeometryIr(b, profile, officialJvmName, options, out failureReason))
        {
            return null;
        }

        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }
}
