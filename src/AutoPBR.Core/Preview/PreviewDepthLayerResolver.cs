using System.Numerics;
using System.Text.Json;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Central preview depth-layer classification from geometry IR cuboids, model elements, and texture signals.
/// Prefer explicit IR / element metadata; fall back to structural and naming heuristics for mod-ready paths.
/// </summary>
internal static class PreviewDepthLayerResolver
{
    private const float CoplanarEpsilon = 0.05f;
    private const float MatrixQuantizeStep = 1e-4f;

    public static (PreviewDepthLayerKind Kind, int LayerOrdinal, bool CastsShadow) ClassifyIrCuboid(
        string partId,
        string? officialJvmName,
        JsonElement cuboid,
        int cuboidIndexOnPart,
        int cuboidCountOnPart)
    {
        if (GeometryIrCuboidMetadata.TryGetPreviewDepthLayer(cuboid, out var explicitKind))
        {
            return (explicitKind, cuboidIndexOnPart, CastsShadowFor(explicitKind, cuboid));
        }

        if (TryClassifyByPartId(partId, officialJvmName, out var partKind, out var partOrdinal))
        {
            return (partKind, partOrdinal, CastsShadowFor(partKind, cuboid));
        }

        if (GeometryIrCuboidMetadata.TryGetTextureKey(cuboid, out var textureKey) &&
            TryInferFromTextureKey(textureKey, partId, out var keyKind, out var keyOrdinal))
        {
            return (keyKind, keyOrdinal, CastsShadowFor(keyKind, cuboid));
        }

        // Horizontal wing/gill sheets stay Base: javap emits bone cuboids before skin on the same
        // ModelPart and relies on polygon order, not a separate overlay depth pass.
        if (GeometryIrCuboidMetadata.TryGetInflate(cuboid, out var inflate) && inflate > 0f)
        {
            return (PreviewDepthLayerKind.Base, cuboidIndexOnPart, true);
        }

        return (PreviewDepthLayerKind.Base, 0, false);
    }

    public static bool TryResolveElement(
        ModelElement element,
        IReadOnlyDictionary<string, string> textures,
        out PreviewDepthLayerKind kind,
        out int layerOrdinal,
        out bool castsShadow)
    {
        kind = element.DepthLayerKind;
        layerOrdinal = element.LayerOrdinal;
        castsShadow = element.CastsShadow;

        if (kind != PreviewDepthLayerKind.Base)
        {
            return false;
        }

        if (TryInferFromFaceTextureKeys(element.Faces, out kind, out layerOrdinal))
        {
            castsShadow = kind == PreviewDepthLayerKind.Base && element.CastsShadow;
            return true;
        }

        foreach (var face in element.Faces.Values)
        {
            if (!textures.TryGetValue(face.TextureKey, out var zipPath))
            {
                continue;
            }

            if (PreviewDepthLayerHeuristics.TryInferKind(zipPath.Replace('\\', '/'), out kind))
            {
                layerOrdinal = 0;
                castsShadow = kind == PreviewDepthLayerKind.Base && element.CastsShadow;
                return true;
            }
        }

        kind = PreviewDepthLayerKind.Base;
        layerOrdinal = 0;
        castsShadow = element.CastsShadow;
        return false;
    }

    public static bool TryInferFromTextureKey(
        string textureKey,
        string partId,
        out PreviewDepthLayerKind kind,
        out int layerOrdinal)
    {
        kind = PreviewDepthLayerKind.Base;
        layerOrdinal = 0;
        if (string.IsNullOrWhiteSpace(textureKey))
        {
            return false;
        }

        var key = textureKey.StartsWith('#') ? textureKey : "#" + textureKey;
        if (IsPrimaryTextureKey(key))
        {
            return false;
        }

        if (key.Contains("eye", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.CosmeticOverlay;
            return true;
        }

        if (key.Contains("emissive", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("glow", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.EmissiveOverlay;
            return true;
        }

        if (key.Contains("wind", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.CutoutOverlay;
            layerOrdinal = WindPartOrdinal(partId);
            return true;
        }

        if (key.Contains("overlay", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("profession", StringComparison.OrdinalIgnoreCase))
        {
            kind = key.Contains("profession", StringComparison.OrdinalIgnoreCase)
                ? PreviewDepthLayerKind.CosmeticOverlay
                : PreviewDepthLayerKind.CutoutOverlay;
            return true;
        }

        return false;
    }

    public static void EnrichMergedModel(MergedJavaBlockModel merged, string? officialModelJvmName = null)
    {
        for (var i = 0; i < merged.Elements.Count; i++)
        {
            var element = merged.Elements[i];
            if (!TryResolveElement(element, merged.Textures, out var kind, out var ordinal, out var castsShadow))
            {
                continue;
            }

            merged.Elements[i] = CopyElement(element, kind, ordinal, castsShadow);
        }

        if (!string.IsNullOrWhiteSpace(officialModelJvmName))
        {
            ApplyRendererStateModelLayers(merged, officialModelJvmName);
        }

        ApplyCoplanarSiblingOverlay(merged);
    }

    public static void ApplyRendererStateModelLayers(MergedJavaBlockModel merged, string officialModelJvmName)
    {
        for (var i = 0; i < merged.Elements.Count; i++)
        {
            var element = merged.Elements[i];
            if (element.DepthLayerKind != PreviewDepthLayerKind.Base)
            {
                continue;
            }

            foreach (var face in element.Faces.Values)
            {
                if (!RendererStateDocumentLoader.TryGetModelLayerByTextureKey(
                        officialModelJvmName,
                        face.TextureKey,
                        out var kind))
                {
                    continue;
                }

                merged.Elements[i] = CopyElement(element, kind, element.LayerOrdinal, castsShadow: false);
                break;
            }
        }
    }

    private static void ApplyCoplanarSiblingOverlay(MergedJavaBlockModel merged)
    {
        var groups = new Dictionary<MatrixKey, List<int>>();
        for (var i = 0; i < merged.Elements.Count; i++)
        {
            var key = MatrixKey.From(merged.Elements[i].LocalToParent);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(i);
        }

        foreach (var indices in groups.Values)
        {
            if (indices.Count < 2)
            {
                continue;
            }

            if (indices.Any(i => merged.Elements[i].DepthLayerKind == PreviewDepthLayerKind.TranslucentOverlay))
            {
                continue;
            }

            indices.Sort();
            for (var overlayOrdinal = 0; overlayOrdinal < indices.Count; overlayOrdinal++)
            {
                var idx = indices[overlayOrdinal];
                var element = merged.Elements[idx];
                if (element.DepthLayerKind != PreviewDepthLayerKind.Base)
                {
                    continue;
                }

                // Horizontal wing/gill membranes and texCrop north/south side sheets are not outer shells.
                if (TryClassifyHorizontalUpFaceMembraneElement(element) ||
                    TryClassifyNorthSouthFaceMembraneElement(element))
                {
                    continue;
                }

                var overlapsPrior = false;
                for (var prior = 0; prior < overlayOrdinal; prior++)
                {
                    var priorElement = merged.Elements[indices[prior]];
                    if (TryGetLocalAabb(element, out var minA, out var maxA) &&
                        TryGetLocalAabb(priorElement, out var minB, out var maxB) &&
                        AabbsIntersect(minA, maxA, minB, maxB))
                    {
                        overlapsPrior = true;
                        break;
                    }
                }

                if (!overlapsPrior || !IsLikelyOuterShell(element, merged.Elements[indices[0]]))
                {
                    continue;
                }

                merged.Elements[idx] = CopyElement(
                    element,
                    PreviewDepthLayerKind.CutoutOverlay,
                    overlayOrdinal,
                    castsShadow: false);
            }
        }
    }

    private static ModelElement CopyElement(
        ModelElement element,
        PreviewDepthLayerKind kind,
        int layerOrdinal,
        bool castsShadow) =>
        new()
        {
            From = element.From,
            To = element.To,
            Faces = element.Faces,
            LocalToParent = element.LocalToParent,
            DepthLayerKind = kind,
            LayerOrdinal = layerOrdinal,
            CastsShadow = castsShadow,
            ShellInflateTexels = element.ShellInflateTexels,
            EnableParallax = element.EnableParallax && kind == PreviewDepthLayerKind.Base,
            MirrorCuboidUv = element.MirrorCuboidUv,
        };

    private static bool TryInferFromFaceTextureKeys(
        IReadOnlyDictionary<string, ModelFace> faces,
        out PreviewDepthLayerKind kind,
        out int layerOrdinal)
    {
        foreach (var face in faces.Values)
        {
            if (TryInferFromTextureKey(face.TextureKey, string.Empty, out kind, out layerOrdinal))
            {
                return true;
            }
        }

        kind = PreviewDepthLayerKind.Base;
        layerOrdinal = 0;
        return false;
    }

    private static bool IsPrimaryTextureKey(string key) =>
        key is "#skin" or "#main";

    private static bool IsSlimeModelJvm(string? officialJvmName) =>
        !string.IsNullOrWhiteSpace(officialJvmName) &&
        officialJvmName.Contains(".SlimeModel", StringComparison.Ordinal);

    private static bool TryGetLocalAabb(ModelElement element, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(
            MathF.Min(element.From[0], element.To[0]),
            MathF.Min(element.From[1], element.To[1]),
            MathF.Min(element.From[2], element.To[2]));
        max = new Vector3(
            MathF.Max(element.From[0], element.To[0]),
            MathF.Max(element.From[1], element.To[1]),
            MathF.Max(element.From[2], element.To[2]));
        return max.X > min.X && max.Y > min.Y && max.Z > min.Z;
    }

    private static bool AabbsIntersect(Vector3 minA, Vector3 maxA, Vector3 minB, Vector3 maxB) =>
        minA.X - CoplanarEpsilon <= maxB.X &&
        maxA.X + CoplanarEpsilon >= minB.X &&
        minA.Y - CoplanarEpsilon <= maxB.Y &&
        maxA.Y + CoplanarEpsilon >= minB.Y &&
        minA.Z - CoplanarEpsilon <= maxB.Z &&
        maxA.Z + CoplanarEpsilon >= minB.Z;

    /// <summary>
    /// Detects horizontal <c>faceMask:["up"]</c> sheets so coplanar-sibling enrich does not treat them as outer shells.
    /// </summary>
    private static bool TryClassifyHorizontalUpFaceMembraneElement(ModelElement element)
    {
        if (!element.Faces.TryGetValue("up", out var upFace) || upFace.Uv is not { Length: >= 4 })
        {
            return false;
        }

        foreach (var faceName in element.Faces.Keys)
        {
            if (!faceName.Equals("up", StringComparison.OrdinalIgnoreCase) &&
                !faceName.Equals("down", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!TryGetLocalAabb(element, out var min, out var max))
        {
            return false;
        }

        var spanX = max.X - min.X;
        var spanY = max.Y - min.Y;
        var spanZ = max.Z - min.Z;
        return spanX >= 8f && spanZ >= 8f && spanY <= MathF.Max(spanX, spanZ) * 0.25f;
    }

    /// <summary>
    /// Detects north/south-only texCrop side sheets (e.g. Creaking head panels) so coplanar enrich does not
    /// retag them as cutout overlays on the shared head part pose.
    /// </summary>
    private static bool TryClassifyNorthSouthFaceMembraneElement(ModelElement element)
    {
        if (element.Faces.Count == 0)
        {
            return false;
        }

        foreach (var faceName in element.Faces.Keys)
        {
            if (!faceName.Equals("north", StringComparison.OrdinalIgnoreCase) &&
                !faceName.Equals("south", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!TryGetLocalAabb(element, out var min, out var max))
        {
            return false;
        }

        var spanX = max.X - min.X;
        var spanY = max.Y - min.Y;
        var spanZ = max.Z - min.Z;
        return spanX >= 4f && spanY >= 4f && spanZ <= MathF.Max(spanX, spanY) * 0.25f;
    }

    /// <summary>Coplanar enrich only tags a larger overlapping sibling (outer robe/shell), not disjoint cuboids on the same pose.</summary>
    private static bool IsLikelyOuterShell(ModelElement candidate, ModelElement reference)
    {
        if (candidate.ShellInflateTexels > 0f)
        {
            return true;
        }

        if (!TryGetLocalAabb(candidate, out var cMin, out var cMax) ||
            !TryGetLocalAabb(reference, out var rMin, out var rMax))
        {
            return false;
        }

        var candidateVolume = AabbVolume(cMin, cMax);
        var referenceVolume = AabbVolume(rMin, rMax);
        return candidateVolume > referenceVolume * 1.02f;
    }

    private static float AabbVolume(Vector3 min, Vector3 max) =>
        MathF.Max(0f, max.X - min.X) *
        MathF.Max(0f, max.Y - min.Y) *
        MathF.Max(0f, max.Z - min.Z);

    private readonly struct MatrixKey : IEquatable<MatrixKey>
    {
        private readonly long _hash;

        private MatrixKey(long hash) => _hash = hash;

        public static MatrixKey From(Matrix4x4 matrix)
        {
            var hash = new HashCode();
            AddQuantized(hash, matrix.M11);
            AddQuantized(hash, matrix.M12);
            AddQuantized(hash, matrix.M13);
            AddQuantized(hash, matrix.M14);
            AddQuantized(hash, matrix.M21);
            AddQuantized(hash, matrix.M22);
            AddQuantized(hash, matrix.M23);
            AddQuantized(hash, matrix.M24);
            AddQuantized(hash, matrix.M31);
            AddQuantized(hash, matrix.M32);
            AddQuantized(hash, matrix.M33);
            AddQuantized(hash, matrix.M34);
            AddQuantized(hash, matrix.M41);
            AddQuantized(hash, matrix.M42);
            AddQuantized(hash, matrix.M43);
            AddQuantized(hash, matrix.M44);
            return new MatrixKey(hash.ToHashCode());
        }

        private static void AddQuantized(HashCode hash, float value) =>
            hash.Add(MathF.Round(value / MatrixQuantizeStep));

        public bool Equals(MatrixKey other) => _hash == other._hash;

        public override bool Equals(object? obj) => obj is MatrixKey other && Equals(other);

        public override int GetHashCode() => _hash.GetHashCode();
    }

    private static bool TryClassifyByPartId(
        string partId,
        string? officialJvmName,
        out PreviewDepthLayerKind kind,
        out int layerOrdinal)
    {
        kind = PreviewDepthLayerKind.Base;
        layerOrdinal = 0;
        if (string.IsNullOrEmpty(partId))
        {
            return false;
        }

        if (string.Equals(partId, "outer_cube", StringComparison.OrdinalIgnoreCase) &&
            IsSlimeModelJvm(officialJvmName))
        {
            kind = PreviewDepthLayerKind.TranslucentOverlay;
            return true;
        }

        if (IsChestModelJvm(officialJvmName))
        {
            if (string.Equals(partId, "lid", StringComparison.OrdinalIgnoreCase))
            {
                kind = PreviewDepthLayerKind.CutoutOverlay;
                layerOrdinal = 1;
                return true;
            }

            if (string.Equals(partId, "lock", StringComparison.OrdinalIgnoreCase))
            {
                kind = PreviewDepthLayerKind.CutoutOverlay;
                layerOrdinal = 2;
                return true;
            }
        }

        if (partId.Contains("eye", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.CosmeticOverlay;
            return true;
        }

        if (string.Equals(partId, "mouth", StringComparison.OrdinalIgnoreCase) &&
            IsSlimeModelJvm(officialJvmName))
        {
            kind = PreviewDepthLayerKind.CosmeticOverlay;
            return true;
        }

        if (partId.StartsWith("wind", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.CutoutOverlay;
            layerOrdinal = WindPartOrdinal(partId);
            return true;
        }

        if (string.Equals(partId, "hat", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.CutoutOverlay;
            return true;
        }

        if (partId.Contains("jacket", StringComparison.OrdinalIgnoreCase) ||
            partId.Contains("sleeve", StringComparison.OrdinalIgnoreCase) ||
            partId.Contains("pants", StringComparison.OrdinalIgnoreCase) ||
            partId.Contains("overlay", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.CutoutOverlay;
            layerOrdinal = partId.Contains("left", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return true;
        }

        if (partId.Contains("wool", StringComparison.OrdinalIgnoreCase) ||
            partId.Contains("fleece", StringComparison.OrdinalIgnoreCase) ||
            partId.Contains("fur", StringComparison.OrdinalIgnoreCase))
        {
            kind = PreviewDepthLayerKind.CutoutOverlay;
            return true;
        }

        if (string.Equals(partId, "nose", StringComparison.OrdinalIgnoreCase) &&
            IsVillagerFamilyJvm(officialJvmName))
        {
            kind = PreviewDepthLayerKind.CosmeticOverlay;
            return true;
        }

        return false;
    }

    private static bool CastsShadowFor(PreviewDepthLayerKind kind, JsonElement cuboid)
    {
        if (kind == PreviewDepthLayerKind.Base &&
            GeometryIrCuboidMetadata.TryGetInflate(cuboid, out var inflate) &&
            inflate > 0f)
        {
            return true;
        }

        return kind == PreviewDepthLayerKind.Base;
    }

    private static int WindPartOrdinal(string partId)
    {
        if (partId.EndsWith("_top", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (partId.EndsWith("_mid", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 0;
    }

    private static bool IsVillagerFamilyJvm(string? officialJvmName) =>
        officialJvmName is { Length: > 0 } jvm &&
        (jvm.Contains("Villager", StringComparison.Ordinal) ||
         jvm.Contains("Illager", StringComparison.Ordinal) ||
         jvm.Contains("ZombieVillager", StringComparison.Ordinal));

    private static bool IsChestModelJvm(string? officialJvmName) =>
        officialJvmName is { Length: > 0 } jvm &&
        jvm.Contains(".object.chest.ChestModel", StringComparison.Ordinal);
}
