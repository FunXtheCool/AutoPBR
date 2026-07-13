using System;
using System.Collections.Generic;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Preview.Entities;

internal sealed partial class EntityModelRuntime
{
    // Block-linked entity preview orientation (bed, boat, signs, chest, pot) — applied after geometry IR emit.

    /// <summary>
    /// Block-linked Java models use +Y downward; Explore preview is Y-up. Unlike mobs they skip full LER
    /// <c>scale(-1,-1,1)</c> — fold only vertical mirroring once at emit.
    /// </summary>
    internal static Matrix4x4 ObjectEntityPreviewVerticalFlip { get; } = Matrix4x4.CreateScale(1f, -1f, 1f);

    internal static bool UsesObjectEntityPreviewVerticalFlip(string builderMethod) =>
        string.Equals(builderMethod, "StandingSignEntity", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "HangingSignEntity", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Boat", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "ChestBoat", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Bed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "Bell", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "BannerFlagStanding", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(builderMethod, "BannerFlagWall", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decorated pot bind pose already includes neck <c>rotationX(PI)</c>; a global matrix
    /// <c>scale(1,-1,1)</c> inverts the closed assembly and breaks zero-height cap winding (see standing sign).
    /// </summary>
    internal static bool UsesDecoratedPotPreviewVerticalOrientation(string builderMethod) =>
        string.Equals(builderMethod, "DecoratedPotEntity", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Standing signs use flat root cuboids with identity bind poses. Reflecting local
    /// <see cref="ModelElement.From"/>/<see cref="ModelElement.To"/> and swapping up/down face slots
    /// preserves triangle winding; a matrix <c>scale(1,-1,1)</c> on <see cref="ModelElement.LocalToParent"/>
    /// inverts winding and detaches cap faces in Explore.
    /// Hanging signs include tilted chain sheets under <c>PartPose.offsetAndRotation</c>; conjugating those
    /// poses under local geometry reflection keeps chains attached to the board.
    /// </summary>
    internal static bool UsesObjectEntityLocalGeometryVerticalFlip(string builderMethod) =>
        string.Equals(builderMethod, "StandingSignEntity", StringComparison.OrdinalIgnoreCase);

    internal static MergedJavaBlockModel ApplyObjectEntityPreviewVerticalFlip(
        MergedJavaBlockModel model,
        string builderMethod) =>
        UsesObjectEntityLocalGeometryVerticalFlip(builderMethod)
            ? ApplyObjectEntityPreviewVerticalFlipLocalGeometry(model)
            : string.Equals(builderMethod, "HangingSignEntity", StringComparison.OrdinalIgnoreCase)
                ? ApplyHangingSignPreviewOrientation(model)
                : ApplyGlobalTransform(model, ObjectEntityPreviewVerticalFlip);

    /// <summary>
    /// Board/plank/vChains and tilted chain sheets all use local geometry reflection plus Y-flip pose
    /// conjugation so parts at negative Java Y stay attached to the board. North/south zero-depth sheets
    /// flip V bounds after reflection. WALL/CEILING <c>normalChains</c> and CEILING_MIDDLE <c>vChains</c>
    /// drop javap Z tilt (where applicable) and swap face slots so upright chains shade correctly in preview.
    /// </summary>
    private static MergedJavaBlockModel ApplyHangingSignPreviewOrientation(MergedJavaBlockModel model)
    {
        var transformed = new List<ModelElement>(model.Elements.Count);
        foreach (var element in model.Elements)
        {
            transformed.Add(ReflectHangingSignElementForPreview(element));
        }

        return new MergedJavaBlockModel
        {
            Elements = transformed,
            Textures = model.Textures,
            UsesLivingEntityRendererColumnYFlip = model.UsesLivingEntityRendererColumnYFlip,
        };
    }

    private static ModelElement ReflectHangingSignElementForPreview(ModelElement element)
    {
        var reflected = ReflectElementVerticalLocalGeometry(element);
        var pose = ConjugateVerticalFlipPartPose(element.LocalToParent);
        if (IsHangingSignNormalChainAttachment() && IsHangingSignTiltedChainSheet(reflected, pose))
        {
            pose = FlattenHangingSignChainPose(pose);
        }

        var faces = reflected.Faces;
        if (IsHangingSignNorthSouthSheetElement(element))
        {
            faces = FlipNorthSouthFaceVBounds(reflected.Faces);
            if (ShouldSwapHangingSignChainFaceSlots(reflected, pose))
            {
                faces = SwapNorthSouthFaceSlots(faces);
            }
        }

        return new ModelElement
        {
            From = reflected.From,
            To = reflected.To,
            Faces = faces,
            LocalToParent = pose,
            DepthLayerKind = reflected.DepthLayerKind,
            LayerOrdinal = reflected.LayerOrdinal,
            CastsShadow = reflected.CastsShadow,
            ShellInflateTexels = reflected.ShellInflateTexels,
            EnableParallax = reflected.EnableParallax,
            MirrorCuboidUv = reflected.MirrorCuboidUv,
        };
    }

    private static bool IsHangingSignNormalChainAttachment()
    {
        return EntityPreviewContextTypeCatalog.ResolveEffectiveAttachment(EntityPreviewBuildContext.CurrentContextTypeId)
            is EntityPreviewContextTypeCatalog.HangingSignAttachment.Wall
            or EntityPreviewContextTypeCatalog.HangingSignAttachment.Ceiling;
    }

    private static bool IsHangingSignTiltedChainSheet(ModelElement reflected, Matrix4x4 conjugatedPose) =>
        IsHangingSignNormalChainCuboid(reflected) &&
        (MathF.Abs(conjugatedPose.M12) > 0.01f || MathF.Abs(conjugatedPose.M21) > 0.01f);

    /// <summary>Drop conjugated Z tilt so upright chains hang straight in preview Y-up space.</summary>
    private static Matrix4x4 FlattenHangingSignChainPose(Matrix4x4 conjugatedPose) =>
        Matrix4x4.CreateTranslation(conjugatedPose.M41, conjugatedPose.M42, conjugatedPose.M43);

    private static bool ShouldSwapHangingSignChainFaceSlots(ModelElement reflected, Matrix4x4 pose) =>
        IsHangingSignUprightChainSheet(reflected) && !IsHangingSignTiltedChainPose(pose);

    private static bool IsHangingSignUprightChainSheet(ModelElement reflected) =>
        MathF.Abs(reflected.To[1] - reflected.From[1] - 6f) < 0.15f &&
        (MathF.Abs(reflected.To[0] - reflected.From[0] - 12f) < 0.15f ||
         IsHangingSignNormalChainCuboid(reflected));

    private static bool IsHangingSignNormalChainCuboid(ModelElement reflected) =>
        MathF.Abs(reflected.To[0] - reflected.From[0] - 3f) < 0.15f;

    private static bool IsHangingSignTiltedChainPose(Matrix4x4 pose) =>
        MathF.Abs(pose.M12) > 0.01f || MathF.Abs(pose.M21) > 0.01f;

    private static Dictionary<string, ModelFace> SwapNorthSouthFaceSlots(Dictionary<string, ModelFace> faces)
    {
        if (!faces.TryGetValue("north", out var north) || !faces.TryGetValue("south", out var south))
        {
            return new Dictionary<string, ModelFace>(faces, StringComparer.OrdinalIgnoreCase);
        }

        var swapped = new Dictionary<string, ModelFace>(faces, StringComparer.OrdinalIgnoreCase);
        swapped["north"] = south;
        swapped["south"] = north;
        return swapped;
    }

    private static Dictionary<string, ModelFace> FlipNorthSouthFaceVBounds(IReadOnlyDictionary<string, ModelFace> faces)
    {
        var flipped = new Dictionary<string, ModelFace>(faces, StringComparer.OrdinalIgnoreCase);
        foreach (var name in new[] { "north", "south" })
        {
            if (!flipped.TryGetValue(name, out var face) || face.Uv is not { Length: >= 4 } uv)
            {
                continue;
            }

            flipped[name] = new ModelFace
            {
                TextureKey = face.TextureKey,
                Uv = [uv[0], uv[3], uv[2], uv[1]],
                RotationDegrees = face.RotationDegrees,
            };
        }

        return flipped;
    }

    /// <summary><c>flip * pose * flip</c> — mirror <c>PartPose.offsetAndRotation</c> under preview Y-up correction.</summary>
    private static Matrix4x4 ConjugateVerticalFlipPartPose(Matrix4x4 pose)
    {
        var flip = ObjectEntityPreviewVerticalFlip;
        return Matrix4x4.Multiply(Matrix4x4.Multiply(flip, pose), flip);
    }

    private static bool IsHangingSignNorthSouthSheetElement(ModelElement element) =>
        element.Faces.ContainsKey("north") &&
        element.Faces.ContainsKey("south") &&
        !element.Faces.ContainsKey("east");

    /// <summary>
    /// Reflect horizontal cap sheets in element-local space and swap lone down→up (standing-sign pattern).
    /// </summary>
    internal static MergedJavaBlockModel ApplyDecoratedPotPreviewVerticalOrientation(MergedJavaBlockModel model)
    {
        var transformed = new List<ModelElement>(model.Elements.Count);
        foreach (var element in model.Elements)
        {
            transformed.Add(IsDecoratedPotHorizontalCapElement(element)
                ? ReflectElementVerticalLocalGeometry(element)
                : element);
        }

        return new MergedJavaBlockModel
        {
            Elements = transformed,
            Textures = model.Textures,
            UsesLivingEntityRendererColumnYFlip = model.UsesLivingEntityRendererColumnYFlip,
        };
    }

    private static bool IsDecoratedPotHorizontalCapElement(ModelElement element)
    {
        if (element.Faces.Count != 1)
        {
            return false;
        }

        var (faceName, face) = element.Faces.First();
        if (face.Uv is not { Length: 4 })
        {
            return false;
        }

        if (!faceName.Equals("down", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var texKey = face.TextureKey ?? "";
        if (!texKey.Contains("base", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsDecoratedPotCapRingBaseUv(face.Uv);
    }

    private static bool IsDecoratedPotCapRingBaseUv(float[] uv)
    {
        if (uv is not { Length: 4 })
        {
            return false;
        }

        // javap texOffs(-14,13): UP exterior [14,27,28,13], DOWN exterior [0,13,14,27] on 32×32 #base.
        var down = uv[0] >= -0.5f && uv[2] <= 14.5f && uv[1] >= 12.5f && uv[3] <= 27.5f;
        var up = uv[0] >= 13.5f && uv[2] <= 28.5f &&
                 ((uv[1] >= 26.5f && uv[3] <= 13.5f) || (uv[1] >= 12.5f && uv[3] <= 27.5f));
        return down || up;
    }

    private static bool IsDecoratedPotCapRingElement(ModelElement element)
    {
        if (element.Faces.TryGetValue("up", out var upFace) &&
            upFace.Uv is { Length: 4 } &&
            (upFace.TextureKey ?? "").Contains("base", StringComparison.OrdinalIgnoreCase) &&
            IsDecoratedPotCapRingBaseUv(upFace.Uv))
        {
            return true;
        }

        if (element.Faces.TryGetValue("down", out var downFace) &&
            downFace.Uv is { Length: 4 } &&
            (downFace.TextureKey ?? "").Contains("base", StringComparison.OrdinalIgnoreCase) &&
            IsDecoratedPotCapRingBaseUv(downFace.Uv))
        {
            return true;
        }

        return false;
    }

    /// <summary>After cap down→up orientation, extend Y toward body for side junction seal (preview-only; no X/Z widen).</summary>
    internal static MergedJavaBlockModel ApplyDecoratedPotPreviewCapRimSeal(MergedJavaBlockModel model)
    {
        var overlap = DecoratedPotPreviewVerticalSealOverlap;
        var transformed = new List<ModelElement>(model.Elements.Count);
        foreach (var element in model.Elements)
        {
            if (!IsDecoratedPotCapRingElement(element))
            {
                transformed.Add(element);
                continue;
            }

            TransformElementCorners(element, out var cMin, out var cMax);
            var isTopCap = cMax.Y > cMin.Y + 1e-3f && cMax.Y > 8f;
            var from = (float[])element.From.Clone();
            var to = (float[])element.To.Clone();
            if (isTopCap)
            {
                to[1] += overlap;
            }
            else
            {
                from[1] -= overlap;
            }

            transformed.Add(new ModelElement
            {
                From = from,
                To = to,
                Faces = element.Faces,
                LocalToParent = element.LocalToParent,
                DepthLayerKind = element.DepthLayerKind,
                LayerOrdinal = element.LayerOrdinal,
                CastsShadow = element.CastsShadow,
                ShellInflateTexels = element.ShellInflateTexels,
                EnableParallax = element.EnableParallax,
                MirrorCuboidUv = element.MirrorCuboidUv,
            });
        }

        return new MergedJavaBlockModel
        {
            Elements = transformed,
            Textures = model.Textures,
            UsesLivingEntityRendererColumnYFlip = model.UsesLivingEntityRendererColumnYFlip,
        };
    }

    private static MergedJavaBlockModel ApplyObjectEntityPreviewVerticalFlipLocalGeometry(MergedJavaBlockModel model)
    {
        var transformed = new List<ModelElement>(model.Elements.Count);
        foreach (var element in model.Elements)
        {
            transformed.Add(ReflectElementVerticalLocalGeometry(element));
        }

        return new MergedJavaBlockModel
        {
            Elements = transformed,
            Textures = model.Textures,
            UsesLivingEntityRendererColumnYFlip = model.UsesLivingEntityRendererColumnYFlip,
        };
    }

    private static ModelElement ReflectElementVerticalLocalGeometry(ModelElement element)
    {
        var faces = new Dictionary<string, ModelFace>(element.Faces, StringComparer.OrdinalIgnoreCase);
        if (faces.TryGetValue("up", out var upFace) && faces.TryGetValue("down", out var downFace))
        {
            faces["up"] = downFace;
            faces["down"] = upFace;
        }
        else if (faces.TryGetValue("up", out var loneUp))
        {
            faces.Remove("up");
            faces["down"] = loneUp;
        }
        else if (faces.TryGetValue("down", out var loneDown))
        {
            faces.Remove("down");
            var reflectedUpFace = loneDown;
            if (IsDecoratedPotCapRingBaseUv(loneDown.Uv!) &&
                GeometryIrUvAtlasQuality.TryNormalizeDecoratedPotCapTexU(
                    EntityModelRuntime.DecoratedPotCapTexCropRawU,
                    out _))
            {
                var upUv = EntityCuboidJavaUvConvention.GetUvRect(
                    EntityCuboidJavaUvConvention.JavaDirection.Up,
                    EntityModelRuntime.DecoratedPotCapTexCropRawU,
                    EntityModelRuntime.DecoratedPotCapTexCropV,
                    14,
                    0,
                    14);
                reflectedUpFace = new ModelFace
                {
                    TextureKey = loneDown.TextureKey,
                    Uv = upUv,
                    RotationDegrees = loneDown.RotationDegrees,
                };
            }

            faces["up"] = reflectedUpFace;
        }

        return new ModelElement
        {
            From = [element.From[0], -element.To[1], element.From[2]],
            To = [element.To[0], -element.From[1], element.To[2]],
            Faces = faces,
            LocalToParent = element.LocalToParent,
            DepthLayerKind = element.DepthLayerKind,
            LayerOrdinal = element.LayerOrdinal,
            CastsShadow = element.CastsShadow,
            ShellInflateTexels = element.ShellInflateTexels,
            EnableParallax = element.EnableParallax,
            MirrorCuboidUv = element.MirrorCuboidUv,
            BakeAtlasWidth = element.BakeAtlasWidth,
            BakeAtlasHeight = element.BakeAtlasHeight,
        };
    }

    /// <summary>
    /// Entity-atlas preview facing from <c>BedRenderer.createModelTransform</c> (26.1.2): lift 9 texels, lay flat (Rx −90°
    /// after object-entity Y-up correction), spin 180° around model center. Default <paramref name="facingYRotDegrees"/>
    /// matches south (0°).
    /// </summary>
    private static Matrix4x4 CreateBedPreviewFacingTransform(float facingYRotDegrees = 0f)
    {
        const float pivot = 8f;
        var lift = EntityParityTemplate.T(0f, 9f, 0f);
        var layFlat = EntityParityTemplate.Rx(-MathF.PI / 2f);
        var spin = EntityParityTemplate.Rz(MathF.PI + facingYRotDegrees * (MathF.PI / 180f));
        var spinAroundCenter = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(EntityParityTemplate.T(pivot, pivot, pivot), spin),
            EntityParityTemplate.T(-pivot, -pivot, -pivot));
        return EntityParityTemplate.Mul(EntityParityTemplate.Mul(lift, layFlat), spinAroundCenter);
    }

    private static bool IsDecoratedPotBasePartId(string partId) =>
        string.Equals(partId, "neck", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partId, "top", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(partId, "bottom", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// After object-entity Y flip the javap pot sits mostly below model origin; lift so the base ring rests near Y=0.
    /// </summary>
    private static MergedJavaBlockModel ApplyDecoratedPotPreviewGroundLift(MergedJavaBlockModel model)
    {
        var minY = float.MaxValue;
        foreach (var el in model.Elements)
        {
            TransformElementCorners(el, out var cMin, out _);
            minY = MathF.Min(minY, cMin.Y);
        }

        if (!float.IsFinite(minY) || minY >= -0.05f)
        {
            return model;
        }

        return ApplyGlobalTransform(model, Matrix4x4.CreateTranslation(0f, -minY, 0f));
    }

    private static void TransformElementCorners(ModelElement el, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue);
        max = new Vector3(float.MinValue);
        var corners = new[]
        {
            new Vector3(el.From[0], el.From[1], el.From[2]),
            new Vector3(el.To[0], el.From[1], el.From[2]),
            new Vector3(el.From[0], el.To[1], el.From[2]),
            new Vector3(el.To[0], el.To[1], el.From[2]),
            new Vector3(el.From[0], el.From[1], el.To[2]),
            new Vector3(el.To[0], el.From[1], el.To[2]),
            new Vector3(el.From[0], el.To[1], el.To[2]),
            new Vector3(el.To[0], el.To[1], el.To[2]),
        };
        foreach (var local in corners)
        {
            var world = Vector3.Transform(local, el.LocalToParent);
            min = Vector3.Min(min, world);
            max = Vector3.Max(max, world);
        }
    }
}
