using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Cross-family utilities, LER basis, walk-cycle math, EntityParityTemplate, EntityCuboid, BabyProfile.

    /// <summary>
    /// Reusable parity template helpers for bytecode-first entity ports.
    /// <b>Rigging policy:</b> compose vanilla <c>PartPose</c>-equivalent chains only through these methods
    /// (<see cref="Mul"/>, <see cref="T"/>, <see cref="Rx"/>, <see cref="Ry"/>, <see cref="Rz"/>, <see cref="Er"/>).
    /// Do not duplicate local <c>static Mul/T/Er</c> helpers in new or refactored builders.
    /// Living-entity previews should end with <see cref="ApplyLivingEntityRendererPreviewBasis"/> (LER <c>scale(-1,-1,1)</c> folded as world-root),
    /// except equines which use <see cref="ApplyEquineLivingEntityRendererPreviewBasis"/> / <see cref="ApplyBabyEquineLivingEntityRendererPreviewBasis"/> for tuned multiply order and baby yaw,
    /// and quadruped rigs that pass <c>lerMirrorRightComposeLocalChain: true</c> so torso chains match vanilla (see <see cref="UsesQuadrupedLerMirrorRightComposeLocalChain"/>).
    /// </summary>
    private static class EntityParityTemplate
    {
        public static Matrix4x4 Mul(Matrix4x4 a, Matrix4x4 b) => Matrix4x4.Multiply(a, b);

        public static Matrix4x4 T(float x, float y, float z) => Matrix4x4.CreateTranslation(x, y, z);

        public static Matrix4x4 Rx(float radians) => Matrix4x4.CreateRotationX(radians);

        public static Matrix4x4 Ry(float radians) => Matrix4x4.CreateRotationY(radians);

        public static Matrix4x4 Rz(float radians) => Matrix4x4.CreateRotationZ(radians);

        /// <summary>
        /// Java <c>PartPose.offsetAndRotation</c> Euler (radians): X, then Y, then Z — matches
        /// <see cref="RigBuilder"/> <c>ComposeEntityPartEulerRad</c> (column-vector convention).
        /// </summary>
        public static Matrix4x4 Er(float xRad, float yRad, float zRad) =>
            Mul(Mul(Rz(zRad), Ry(yRad)), Rx(xRad));

        /// <summary>Intrinsic Euler rotation matching geometry IR <c>eulerOrder</c> (same convention as <see cref="Er"/> for <c>XYZ</c>).</summary>
        public static Matrix4x4 ComposeEuler(string? order, float xRad, float yRad, float zRad) =>
            order switch
            {
                null or "XYZ" => Er(xRad, yRad, zRad),
                "XZY" => Mul(Mul(Ry(yRad), Rz(zRad)), Rx(xRad)),
                "YXZ" => Mul(Mul(Rz(zRad), Rx(xRad)), Ry(yRad)),
                "YZX" => Mul(Mul(Rx(xRad), Rz(zRad)), Ry(yRad)),
                "ZXY" => Mul(Mul(Ry(yRad), Rx(xRad)), Rz(zRad)),
                "ZYX" => Mul(Mul(Rx(xRad), Ry(yRad)), Rz(zRad)),
                _ => Er(xRad, yRad, zRad)
            };

        public static void AssertFinitePose(in Matrix4x4 pose, string label)
        {
            static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
            if (!IsFinite(pose.M11) || !IsFinite(pose.M22) || !IsFinite(pose.M33) || !IsFinite(pose.M44) ||
                !IsFinite(pose.M41) || !IsFinite(pose.M42) || !IsFinite(pose.M43))
            {
                throw new InvalidOperationException($"Entity parity pose '{label}' contained non-finite values.");
            }
        }
    }

    /// <summary>
    /// Data-first description of a Java-style model cuboid: local-space diagonal corners, UV origin, optional explicit UV
    /// footprint, and UV mirror flag. Used as the common surface between geometry IR emission, codegen tables, and legacy code-built rigs,
    /// and any future codegen that wants to target <see cref="RigBuilder.AddBox"/>.
    /// </summary>
    internal readonly record struct EntityCuboid(
        float X0,
        float Y0,
        float Z0,
        float X1,
        float Y1,
        float Z1,
        int TexU,
        int TexV,
        int UvSizeW = -1,
        int UvSizeH = -1,
        int UvSizeD = -1,
        bool MirrorUv = false,
        float OffsetX = 0f,
        float OffsetY = 0f,
        float OffsetZ = 0f,
        float XRot = 0f,
        float YRot = 0f,
        float ZRot = 0f,
        string[]? FaceMask = null,
        string? TextureKey = null)
    {
        public Vector3? RotationPivot { get; init; }

        public void Emit(RigBuilder builder, Matrix4x4 parentPose, float partScale, string texKey = "#skin")
        {
            var key = TextureKey ?? texKey;
            builder.AddBox(
                X0, Y0, Z0,
                X1, Y1, Z1,
                key,
                partScale,
                OffsetX,
                OffsetY,
                OffsetZ,
                TexU,
                TexV,
                parentPose,
                XRot,
                YRot,
                ZRot,
                RotationPivot,
                UvSizeW,
                UvSizeH,
                UvSizeD,
                MirrorUv,
                FaceMask);
        }
    }

    /// <summary>Emit every cuboid in <paramref name="cuboids"/> under the same parent pose and part scale.</summary>
    private static void EmitCuboids(
        RigBuilder builder,
        ReadOnlySpan<EntityCuboid> cuboids,
        Matrix4x4 parentPose,
        float partScale,
        string texKey = "#skin")
    {
        foreach (var c in cuboids)
        {
            c.Emit(builder, parentPose, partScale, texKey);
        }
    }

    /// <summary>
    /// Standard <c>QuadrupedModel.createLegs</c> leg: <c>texOffs(0,16)</c>, local <c>(-2,0,-2)</c>–<c>(2,legFootY,2)</c>.
    /// Origin at the leg root; Y grows downward to <paramref name="legFootY"/>.
    /// </summary>
    private static EntityCuboid QuadrupedLegCuboidTex016(float legFootY, bool mirrorUv = false) =>
        new(-2f, 0f, -2f, 2f, legFootY, 2f, 0, 16, MirrorUv: mirrorUv);

    internal static Matrix4x4 ComposeEulerForTests(string? order, float xRad, float yRad, float zRad) =>
        EntityParityTemplate.ComposeEuler(order, xRad, yRad, zRad);

    internal static Matrix4x4 ErForTests(float xRad, float yRad, float zRad) =>
        EntityParityTemplate.Er(xRad, yRad, zRad);

    /// <summary>
    /// Baby mob mesh/transform tuning changed at Java Edition 26.1; earlier builds include all 1.x versions and 26.0.x snapshots.
    /// Uses <see cref="MinecraftNativeProfile.ParsedVersion"/> when present; otherwise parses <c>major.minor</c> from <see cref="MinecraftNativeProfile.Name"/>.
    /// </summary>
    private static bool UsesPostBabyModelUpdate(MinecraftNativeProfile profile)
    {
        if (profile.ParsedVersion is { } v)
        {
            if (v.Major > 26)
            {
                return true;
            }

            return v is { Major: 26, Minor: >= 1 };
        }

        return NameLooksLikePostBabyModelGameVersion(profile.Name);
    }


    private static bool NameLooksLikePostBabyModelGameVersion(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var maj))
        {
            return false;
        }

        if (!int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var min))
        {
            return false;
        }

        if (maj > 26)
        {
            return true;
        }

        return maj == 26 && min >= 1;
    }

    private static (float LimbSwing, float LimbSwingAmount) ComputePreviewEntityWalkCycle(
        float animationTimeSeconds,
        float idlePhase01,
        float wave) =>
        PreviewRenderStateSynthesis.ComputeWalkCycle(animationTimeSeconds, idlePhase01, wave);

    /// <summary>Vanilla <c>Mth.triangleWave(position, period)</c> (26.1.2 <c>client.jar</c> bytecode).</summary>
    private static float VanillaMthTriangleWave(float position, float period)
    {
        var p = period;
        var r = position % p;
        if (r < 0f)
        {
            r += p;
        }

        return (MathF.Abs(r - p * 0.5f) - p * 0.25f) / (p * 0.25f);
    }


    private static MergedJavaBlockModel ApplyGlobalTransform(
        MergedJavaBlockModel model,
        Matrix4x4 transform,
        Matrix4x4? preMultiplyWorld = null,
        Matrix4x4? postMultiplyWorld = null)
    {
        var transformed = new List<ModelElement>(model.Elements.Count);
        foreach (var e in model.Elements)
        {
            var ls = Matrix4x4.Multiply(e.LocalToParent, transform);
            var m = preMultiplyWorld is { } pre ? Matrix4x4.Multiply(pre, ls)
                : postMultiplyWorld is { } post ? Matrix4x4.Multiply(ls, post)
                : ls;
            transformed.Add(new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = m,
            });
        }

        return new MergedJavaBlockModel
        {
            Elements = transformed,
            Textures = model.Textures,
        };
    }

    /// <summary>
    /// Applies an additional world-root matrix on the baked element poses: <c>LocalToParent' = worldRoot * LocalToParent</c>
    /// (column-vector mesh convention — same as <see cref="MinecraftModelBaker"/>).
    /// </summary>
    private static MergedJavaBlockModel ApplyPreviewWorldRoot(MergedJavaBlockModel model, Matrix4x4 worldRoot)
    {
        var transformed = new List<ModelElement>(model.Elements.Count);
        foreach (var e in model.Elements)
        {
            var m = Matrix4x4.Multiply(worldRoot, e.LocalToParent);
            transformed.Add(new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = m,
            });
        }

        return new MergedJavaBlockModel
        {
            Elements = transformed,
            Textures = model.Textures,
        };
    }

    /// <summary>
    /// Vanilla <c>LivingEntityRenderer</c> applies <c>PoseStack.scale(-1, -1, 1)</c> before drawing most mob models.
    /// Default fold is <c>worldRoot * LocalToParent</c>; quadruped cow-class style torsos match vanilla when folded as
    /// <c>LocalToParent * worldRoot</c> (same idea as <see cref="ApplyEquineLivingEntityRendererPreviewBasis"/>).
    /// </summary>
    /// <remarks>
    /// Horse/donkey/mule use <see cref="ApplyEquineLivingEntityRendererPreviewBasis"/> / <see cref="ApplyBabyEquineLivingEntityRendererPreviewBasis"/> instead
    /// (model-scale-aware and baby yaw). Pass <paramref name="lerMirrorRightComposeLocalChain"/> for other quadrupeds (see
    /// <see cref="UsesQuadrupedLerMirrorRightComposeLocalChain"/> + fast GPU bone path in <see cref="TryFillBoneMatricesFast"/>).
    /// </remarks>
    private static MergedJavaBlockModel ApplyLivingEntityRendererPreviewBasis(
        MergedJavaBlockModel model,
        bool lerMirrorRightComposeLocalChain = false) =>
        lerMirrorRightComposeLocalChain
            ? ApplyGlobalTransform(model, Matrix4x4.CreateScale(-1f, -1f, 1f))
            : ApplyPreviewWorldRoot(model, Matrix4x4.CreateScale(-1f, -1f, 1f));


    private static string ToTextureRef(string normalizedAssetPath)
    {
        // assets/minecraft/textures/entity/cow/cow.png -> entity/cow/cow
        var p = normalizedAssetPath;
        var marker = "/textures/";
        var i = p.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (i < 0)
        {
            return "entity/unknown";
        }

        var rel = p[(i + marker.Length)..];
        return rel.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? rel[..^4] : rel;
    }

    /// <summary>Same parent folder as <paramref name="normalizedPrimaryAssetPngPath"/>, different file stem (e.g. <c>phantom_eyes</c>).</summary>
    private static string CompanionDiffuseTextureRefFromSiblingFileStem(
        string normalizedPrimaryAssetPngPath,
        string siblingFileStemWithoutExtension)
    {
        var norm = normalizedPrimaryAssetPngPath.Replace('\\', '/').TrimStart('/');
        var slash = norm.LastIndexOf('/');
        var parent = slash >= 0 ? norm[..slash] : "";
        var relPng = string.IsNullOrEmpty(parent)
            ? $"{siblingFileStemWithoutExtension}.png"
            : $"{parent}/{siblingFileStemWithoutExtension}.png";
        return ToTextureRef(relPng);
    }


    internal static bool LooksLikeBabyTexture(string stem, string normalizedAssetPath)
    {
        return stem.Contains("baby", StringComparison.OrdinalIgnoreCase) ||
            stem.Contains("snifflet", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/baby/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("_baby/", StringComparison.OrdinalIgnoreCase) ||
            normalizedAssetPath.Contains("/sniffer/snifflet", StringComparison.OrdinalIgnoreCase);
    }


    private static bool ContainsAny(string value, string[] keys)
    {
        foreach (var k in keys)
        {
            if (value.Contains(k, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }


    private static float Wave(float t, float hz) => MathF.Sin(t * MathF.PI * 2f * hz);

    internal readonly record struct BabyProfile(float BodyScale, float HeadScale, float LegScale)
    {
        public static readonly BabyProfile Adult = new(1f, 1f, 1f);

        /// <summary>
        /// Matches vanilla <c>LivingEntity.DEFAULT_BABY_SCALE</c> (<c>0.5F</c>, 26.1.2 <c>client.jar</c>): uniform <c>getAgeScale()</c> while the
        /// renderer keeps the adult <see cref="HumanoidModel"/> / shared mesh (e.g. <c>SkeletonModel</c>, <c>PlayerModel</c>, <c>IllagerModel</c> —
        /// see geometry IR under <c>docs/generated/geometry/26.1.2/</c>).
        /// </summary>
        public static readonly BabyProfile VanillaUniformBaby = new(0.5f, 0.5f, 0.5f);
    }
}
