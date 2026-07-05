using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal static partial class MinecraftModelBaker
{
    public const int FloatsPerVertex = 12;

    /// <summary>Bind-pose layout for GPU bone skinning: same as <see cref="FloatsPerVertex"/> plus element bone index.</summary>
    public const int FloatsPerSkinnedVertex = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex;

    /// <summary>
    /// Bakes merged model into one interleaved mesh + draw batches. <paramref name="textureZipPathToMaterialIndex"/> maps
    /// normalized zip paths (forward slashes) to slot index.
    /// </summary>
    public static bool TryBake(
        MergedJavaBlockModel model,
        string textureNamespace,
        IReadOnlyDictionary<string, int> textureZipPathToMaterialIndex,
        IReadOnlyDictionary<string, (int w, int h)> textureSizeByZipPath,
        out float[] vertices,
        out uint[] indices,
        out List<PreviewDrawBatch> batches) =>
        TryBakeWithUvPolicy(
            model,
            textureNamespace,
            textureZipPathToMaterialIndex,
            textureSizeByZipPath,
            PreviewUvBakePolicy.Resolve(model),
            appendBoneIndex: false,
            skipPreviewCuboidScale: false,
            out vertices,
            out indices,
            out batches);

    /// <summary>Test / Phase C hook: bake with an explicit UV policy instead of <see cref="PreviewUvBakePolicy.Resolve"/>.</summary>
    internal static bool TryBakeWithUvPolicy(
        MergedJavaBlockModel model,
        string textureNamespace,
        IReadOnlyDictionary<string, int> textureZipPathToMaterialIndex,
        IReadOnlyDictionary<string, (int w, int h)> textureSizeByZipPath,
        in PreviewUvBakePolicy uvPolicy,
        out float[] vertices,
        out uint[] indices,
        out List<PreviewDrawBatch> batches) =>
        TryBakeWithUvPolicy(
            model,
            textureNamespace,
            textureZipPathToMaterialIndex,
            textureSizeByZipPath,
            in uvPolicy,
            appendBoneIndex: false,
            skipPreviewCuboidScale: false,
            out vertices,
            out indices,
            out batches);

    private static bool TryBakeWithUvPolicy(
        MergedJavaBlockModel model,
        string textureNamespace,
        IReadOnlyDictionary<string, int> textureZipPathToMaterialIndex,
        IReadOnlyDictionary<string, (int w, int h)> textureSizeByZipPath,
        in PreviewUvBakePolicy uvPolicy,
        bool appendBoneIndex,
        bool skipPreviewCuboidScale,
        out float[] vertices,
        out uint[] indices,
        out List<PreviewDrawBatch> batches)
    {
        vertices = [];
        indices = [];
        var batchList = new List<PreviewDrawBatch>();

        var stride = appendBoneIndex ? FloatsPerSkinnedVertex : FloatsPerVertex;
        var v = new List<float>(256 * stride);
        var idx = new List<uint>(384);
        var faceOrder = new[] { "north", "south", "west", "east", "up", "down" };

        var currentBatchStart = 0;
        int? currentMat = null;
        PreviewDrawLayerPolicy currentPolicy = PreviewDrawLayerPolicy.DefaultBase;
        var currentParallax = true;

        void CloseBatchIfNeeded(int newMat, PreviewDrawLayerPolicy newPolicy, bool newParallax)
        {
            if (currentMat is null)
            {
                currentMat = newMat;
                currentPolicy = newPolicy;
                currentParallax = newParallax;
                currentBatchStart = idx.Count;
                return;
            }

            if (newMat == currentMat.Value && newPolicy.Equals(currentPolicy) && newParallax == currentParallax)
            {
                return;
            }

            var count = idx.Count - currentBatchStart;
            if (count > 0)
            {
                batchList.Add(new PreviewDrawBatch(currentBatchStart, count, currentMat.Value)
                {
                    LayerPolicy = currentPolicy,
                    EnableParallax = currentParallax,
                });
            }

            currentMat = newMat;
            currentPolicy = newPolicy;
            currentParallax = newParallax;
            currentBatchStart = idx.Count;
        }

        void FlushFinalBatch()
        {
            if (currentMat is null)
            {
                return;
            }

            var count = idx.Count - currentBatchStart;
            if (count > 0)
            {
                batchList.Add(new PreviewDrawBatch(currentBatchStart, count, currentMat.Value)
                {
                    LayerPolicy = currentPolicy,
                    EnableParallax = currentParallax,
                });
            }
        }

        var elementIndex = 0;
        foreach (var el in model.Elements)
        {
            var fx = el.From[0];
            var fy = el.From[1];
            var fz = el.From[2];
            var tx = el.To[0];
            var ty = el.To[1];
            var tz = el.To[2];

            foreach (var faceName in faceOrder)
            {
                if (!el.Faces.TryGetValue(faceName, out var face))
                {
                    continue;
                }

                var effectiveFaceName = ApplyFaceSemanticRouting(faceName, in uvPolicy);

                if (!TryResolveTextureZipPath(face.TextureKey, model.Textures, textureNamespace, out var texZip))
                {
                    continue;
                }

                if (!textureZipPathToMaterialIndex.TryGetValue(texZip, out var matIdx))
                {
                    continue;
                }

                if (!textureSizeByZipPath.TryGetValue(texZip, out var wh))
                {
                    continue;
                }

                CloseBatchIfNeeded(matIdx, ResolveElementLayerPolicy(el, texZip, model.Textures), el.EnableParallax);

                _ = TryEmitFace(effectiveFaceName, fx, fy, fz, tx, ty, tz, face, wh.w, wh.h, el.LocalToParent, v, idx,
                    appendBoneIndex, appendBoneIndex ? elementIndex : 0, skipPreviewCuboidScale, in uvPolicy, el.MirrorCuboidUv,
                    el.RescaleRotation);
            }

            elementIndex++;
        }

        FlushFinalBatch();
        PreviewDrawBatchOrdering.Sort(batchList);

        batches = batchList;
        if (v.Count == 0 || idx.Count == 0 || batchList.Count == 0)
        {
            return false;
        }

        vertices = v.ToArray();
        indices = idx.ToArray();
        return true;
    }

    /// <summary>
    /// Emits vertices in per-element space <b>after</b> <see cref="ModelElement.LocalToParent"/> (same as CPU baker before the
    /// <c>x/16−½</c> cuboid preview scale), with a bone index per vertex. GPU uniforms store row <c>M_bind⁻¹ · M_anim</c> so
    /// <c>r · M_bind · bone = r · M_anim</c> and <c>W</c> matches the CPU path (cuboid scale is applied in the GL vertex shader after skinning).
    /// Element index matches <see cref="MergedJavaBlockModel.Elements"/> order.
    /// </summary>
    public static bool TryBakeBindPoseForGpuSkinning(
        MergedJavaBlockModel model,
        string textureNamespace,
        IReadOnlyDictionary<string, int> textureZipPathToMaterialIndex,
        IReadOnlyDictionary<string, (int w, int h)> textureSizeByZipPath,
        out float[] vertices,
        out uint[] indices,
        out List<PreviewDrawBatch> batches) =>
        TryBakeWithUvPolicy(
            model,
            textureNamespace,
            textureZipPathToMaterialIndex,
            textureSizeByZipPath,
            PreviewUvBakePolicy.Resolve(model),
            appendBoneIndex: true,
            skipPreviewCuboidScale: true,
            out vertices,
            out indices,
            out batches);
}
