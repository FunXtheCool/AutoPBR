using System.Numerics;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    /// <summary>
    /// Preview camera faces the chest front (+Z); place the east/right block at origin and offset the west/left block +X
    /// so labeled <c>_left</c>/<c>_right</c> atlas sheets appear on the viewer's left/right.
    /// </summary>
    private const float DoubleChestLeftHalfPreviewOffsetTexels = 16f;

    private const string PairedHalfTextureDictKey = "chest_pair";

    private const string PairedHalfFaceTextureKey = "#chest_pair";

    internal static bool IsDoubleChestHalfAssetPath(string normalizedAssetPath)
    {
        var norm = normalizedAssetPath.Replace('\\', '/');
        return norm.Contains("/textures/entity/chest/", StringComparison.OrdinalIgnoreCase) &&
               (norm.Contains("_left", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("_right", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool TryGetDoubleChestPartnerAssetPath(string normalizedAssetPath, out string partnerPath)
    {
        partnerPath = "";
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        if (!IsDoubleChestHalfAssetPath(norm))
        {
            return false;
        }

        var slash = norm.LastIndexOf('/');
        var dir = slash >= 0 ? norm[..(slash + 1)] : "";
        var file = slash >= 0 ? norm[(slash + 1)..] : norm;
        string? partnerFile = null;
        if (file.Contains("_left", StringComparison.OrdinalIgnoreCase))
        {
            partnerFile = file.Replace("_left", "_right", StringComparison.OrdinalIgnoreCase);
        }
        else if (file.Contains("_right", StringComparison.OrdinalIgnoreCase))
        {
            partnerFile = file.Replace("_right", "_left", StringComparison.OrdinalIgnoreCase);
        }

        if (string.IsNullOrEmpty(partnerFile) ||
            string.Equals(partnerFile, file, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        partnerPath = dir + partnerFile;
        return true;
    }

    private static bool TryMergeDoubleChestPartnerHalf(
        string norm,
        MinecraftNativeProfile profile,
        float idlePhase01,
        float animationTimeSeconds,
        bool applyGeometryIrSetupAnimMotion,
        ref MergedJavaBlockModel primaryMesh)
    {
        if (!TryGetDoubleChestPartnerAssetPath(norm, out var partnerNorm))
        {
            return false;
        }

        var partnerStem = Path.GetFileNameWithoutExtension(partnerNorm).ToLowerInvariant();
        var partnerTexRef = ToTextureRef(partnerNorm);
        var partnerIsBaby = LooksLikeBabyTexture(partnerStem, partnerNorm);

        if (!TryDispatchEntityStaticMeshBuild(
                partnerNorm,
                partnerStem,
                partnerTexRef,
                profile,
                partnerIsBaby,
                idlePhase01,
                animationTimeSeconds,
                routeCache: null,
                applyGeometryIrSetupAnimMotion,
                out _,
                out var partnerMesh,
                out _) ||
            partnerMesh.Elements.Count == 0)
        {
            return false;
        }

        var primaryIsLeft = norm.Contains("_left", StringComparison.OrdinalIgnoreCase);
        var leftMesh = primaryIsLeft ? primaryMesh : partnerMesh;
        var rightMesh = primaryIsLeft ? partnerMesh : primaryMesh;
        var leftTexRef = ToTextureRef(primaryIsLeft ? norm : partnerNorm);
        var rightTexRef = ToTextureRef(primaryIsLeft ? partnerNorm : norm);

        primaryMesh = MergeDoubleChestPreviewHalves(leftMesh, rightMesh, leftTexRef, rightTexRef);
        return true;
    }

    private static MergedJavaBlockModel MergeDoubleChestPreviewHalves(
        MergedJavaBlockModel leftHalf,
        MergedJavaBlockModel rightHalf,
        string leftSkinTextureRef,
        string rightSkinTextureRef)
    {
        var leftOffset = Matrix4x4.CreateTranslation(DoubleChestLeftHalfPreviewOffsetTexels, 0f, 0f);
        var elements = new List<ModelElement>(leftHalf.Elements.Count + rightHalf.Elements.Count);

        foreach (var el in leftHalf.Elements)
        {
            var pose = Matrix4x4.Multiply(leftOffset, el.LocalToParent);
            elements.Add(CloneModelElement(el, pose, remapFaceTextureKey: null));
        }

        foreach (var el in rightHalf.Elements)
        {
            elements.Add(CloneModelElement(el, el.LocalToParent, PairedHalfFaceTextureKey));
        }

        return new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["skin"] = leftSkinTextureRef,
                [PairedHalfTextureDictKey] = rightSkinTextureRef,
            },
            UsesLivingEntityRendererColumnYFlip = leftHalf.UsesLivingEntityRendererColumnYFlip ||
                                                   rightHalf.UsesLivingEntityRendererColumnYFlip,
        };
    }

    private static ModelElement CloneModelElement(
        ModelElement source,
        Matrix4x4 localToParent,
        string? remapFaceTextureKey)
    {
        var faces = new Dictionary<string, ModelFace>(StringComparer.Ordinal);
        foreach (var (faceName, face) in source.Faces)
        {
            faces[faceName] = new ModelFace
            {
                TextureKey = remapFaceTextureKey ?? face.TextureKey,
                Uv = face.Uv,
                RotationDegrees = face.RotationDegrees,
            };
        }

        return new ModelElement
        {
            From = source.From,
            To = source.To,
            Faces = faces,
            LocalToParent = localToParent,
            DepthLayerKind = source.DepthLayerKind,
            LayerOrdinal = source.LayerOrdinal,
            CastsShadow = source.CastsShadow,
            ShellInflateTexels = source.ShellInflateTexels,
            EnableParallax = source.EnableParallax,
            MirrorCuboidUv = source.MirrorCuboidUv,
        };
    }
}
