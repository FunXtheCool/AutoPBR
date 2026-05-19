using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal static class MinecraftModelBaker
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
        out List<PreviewDrawBatch> batches)
    {
        vertices = [];
        indices = [];
        var batchList = new List<PreviewDrawBatch>();

        var v = new List<float>(256 * FloatsPerVertex);
        var idx = new List<uint>(384);
        var faceOrder = new[] { "north", "south", "west", "east", "up", "down" };

        var currentBatchStart = 0;
        int? currentMat = null;

        void CloseBatchIfNeeded(int newMat)
        {
            if (currentMat is null)
            {
                currentMat = newMat;
                currentBatchStart = idx.Count;
                return;
            }

            if (newMat == currentMat.Value)
            {
                return;
            }

            var count = idx.Count - currentBatchStart;
            if (count > 0)
            {
                batchList.Add(new PreviewDrawBatch(currentBatchStart, count, currentMat.Value));
            }

            currentMat = newMat;
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
                batchList.Add(new PreviewDrawBatch(currentBatchStart, count, currentMat.Value));
            }
        }

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

                var effectiveFaceName = ApplyFaceSemanticDebugRouting(faceName);

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

                CloseBatchIfNeeded(matIdx);

                _ = TryEmitFace(effectiveFaceName, fx, fy, fz, tx, ty, tz, face, wh.w, wh.h, el.LocalToParent, v, idx,
                    appendBoneIndex: false, boneElementIndex: 0, skipPreviewCuboidScale: false);
            }
        }

        FlushFinalBatch();

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
        out List<PreviewDrawBatch> batches)
    {
        vertices = [];
        indices = [];
        var batchList = new List<PreviewDrawBatch>();

        var v = new List<float>(256 * FloatsPerSkinnedVertex);
        var idx = new List<uint>(384);
        var faceOrder = new[] { "north", "south", "west", "east", "up", "down" };

        var currentBatchStart = 0;
        int? currentMat = null;

        void CloseBatchIfNeeded(int newMat)
        {
            if (currentMat is null)
            {
                currentMat = newMat;
                currentBatchStart = idx.Count;
                return;
            }

            if (newMat == currentMat.Value)
            {
                return;
            }

            var count = idx.Count - currentBatchStart;
            if (count > 0)
            {
                batchList.Add(new PreviewDrawBatch(currentBatchStart, count, currentMat.Value));
            }

            currentMat = newMat;
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
                batchList.Add(new PreviewDrawBatch(currentBatchStart, count, currentMat.Value));
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

                var effectiveFaceName = ApplyFaceSemanticDebugRouting(faceName);

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

                CloseBatchIfNeeded(matIdx);

                _ = TryEmitFace(effectiveFaceName, fx, fy, fz, tx, ty, tz, face, wh.w, wh.h, el.LocalToParent, v, idx,
                    appendBoneIndex: true, boneElementIndex: elementIndex, skipPreviewCuboidScale: true);
            }

            elementIndex++;
        }

        FlushFinalBatch();

        batches = batchList;
        if (v.Count == 0 || idx.Count == 0 || batchList.Count == 0)
        {
            return false;
        }

        vertices = v.ToArray();
        indices = idx.ToArray();
        return true;
    }

    internal static bool TryResolveTextureZipPath(
        string faceTextureKey,
        Dictionary<string, string> textures,
        string defaultNamespace,
        out string textureZipPath)
    {
        textureZipPath = string.Empty;
        var key = faceTextureKey.Trim();
        if (key.StartsWith('#'))
        {
            key = key[1..];
        }

        if (!textures.TryGetValue(key, out var texVal))
        {
            if (!textures.TryGetValue("#" + key, out texVal))
            {
                return false;
            }
        }

        texVal = texVal.Trim();
        if (string.IsNullOrEmpty(texVal))
        {
            return false;
        }

        var ns = defaultNamespace;
        var path = texVal.Replace('\\', '/');
        if (path.Contains(':', StringComparison.Ordinal))
        {
            var c = path.IndexOf(':');
            ns = path[..c];
            path = path[(c + 1)..].TrimStart('/');
        }

        if (!path.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            path = "textures/" + path;
        }

        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            path += ".png";
        }

        textureZipPath = $"assets/{ns}/{path}";
        return true;
    }

    private static bool TryEmitFace(
        string faceName,
        float fx,
        float fy,
        float fz,
        float tx,
        float ty,
        float tz,
        ModelFace face,
        int texW,
        int texH,
        Matrix4x4 localToMesh,
        List<float> v,
        List<uint> idx,
        bool appendBoneIndex,
        int boneElementIndex,
        bool skipPreviewCuboidScale = false)
    {
        if (!TryGetFacePlaneRaw(faceName, fx, fy, fz, tx, ty, tz, out var n, out var tang, out var wSign, out var r0,
                out var r1, out var r2, out var r3))
        {
            return false;
        }

        if (!localToMesh.IsIdentity)
        {
            r0 = Vector3.Transform(r0, localToMesh);
            r1 = Vector3.Transform(r1, localToMesh);
            r2 = Vector3.Transform(r2, localToMesh);
            r3 = Vector3.Transform(r3, localToMesh);
            n = Vector3.Normalize(Vector3.TransformNormal(n, localToMesh));
            tang = Vector3.Normalize(Vector3.TransformNormal(tang, localToMesh));
        }

        var c0 = skipPreviewCuboidScale ? r0 : W(r0.X, r0.Y, r0.Z);
        var c1 = skipPreviewCuboidScale ? r1 : W(r1.X, r1.Y, r1.Z);
        var c2 = skipPreviewCuboidScale ? r2 : W(r2.X, r2.Y, r2.Z);
        var c3 = skipPreviewCuboidScale ? r3 : W(r3.X, r3.Y, r3.Z);

        float u0, v0, u1, v1;
        if (face.Uv is { Length: >= 4 } uv)
        {
            u0 = uv[0];
            v0 = uv[1];
            u1 = uv[2];
            v1 = uv[3];
        }
        else
        {
            u0 = 0;
            v0 = 0;
            u1 = 16;
            v1 = 16;
        }

        ApplyGlobalUvDebugSettings(ref u0, ref v0, ref u1, ref v1, ref face);

        // Minecraft texture space: origin top-left, v increases downward. Map to GL bottom-left origin.
        float Nu(float px) => px / texW;
        float Nv(float py) => UvDebugSettings.UseBottomLeftUvOrigin ? 1f - py / texH : py / texH;

        Vector2 uvA;
        Vector2 uvB;
        Vector2 uvC;
        Vector2 uvD;
        if (UvDebugSettings.PreserveDirectionalBounds)
        {
            // Preserve directional UV bounds (u0/u1 and v0/v1) instead of min/max canonicalization.
            uvA = new Vector2(Nu(u0), Nv(v1));
            uvB = new Vector2(Nu(u1), Nv(v1));
            uvC = new Vector2(Nu(u1), Nv(v0));
            uvD = new Vector2(Nu(u0), Nv(v0));
        }
        else
        {
            uvA = new Vector2(Nu(Math.Min(u0, u1)), Nv(Math.Max(v0, v1)));
            uvB = new Vector2(Nu(Math.Max(u0, u1)), Nv(Math.Max(v0, v1)));
            uvC = new Vector2(Nu(Math.Max(u0, u1)), Nv(Math.Min(v0, v1)));
            uvD = new Vector2(Nu(Math.Min(u0, u1)), Nv(Math.Min(v0, v1)));
        }

        ApplyUvCornerOrderMode(UvDebugSettings.UvCornerOrderMode, ref uvA, ref uvB, ref uvC, ref uvD);
        ApplyFaceUvRotation(face.RotationDegrees, ref uvA, ref uvB, ref uvC, ref uvD);

        var stride = appendBoneIndex ? FloatsPerSkinnedVertex : FloatsPerVertex;
        var baseIndex = (uint)(v.Count / stride);
        AddVert(v, c0, n, uvA, tang, wSign, appendBoneIndex, boneElementIndex);
        AddVert(v, c1, n, uvB, tang, wSign, appendBoneIndex, boneElementIndex);
        AddVert(v, c2, n, uvC, tang, wSign, appendBoneIndex, boneElementIndex);
        AddVert(v, c3, n, uvD, tang, wSign, appendBoneIndex, boneElementIndex);

        idx.Add(baseIndex);
        idx.Add(baseIndex + 1);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex);
        idx.Add(baseIndex + 2);
        idx.Add(baseIndex + 3);
        return true;
    }

    private static void AddVert(List<float> v, Vector3 p, Vector3 n, Vector2 uv, Vector3 t, float wSign,
        bool appendBoneIndex, int boneElementIndex)
    {
        v.Add(p.X);
        v.Add(p.Y);
        v.Add(p.Z);
        v.Add(n.X);
        v.Add(n.Y);
        v.Add(n.Z);
        v.Add(uv.X);
        v.Add(uv.Y);
        v.Add(t.X);
        v.Add(t.Y);
        v.Add(t.Z);
        v.Add(wSign);
        if (appendBoneIndex)
        {
            // Stored as IEEE bits so the GPU path can use glVertexAttribIPointer (GLES/ANGLE mediump float is unsafe for indices).
            v.Add(BitConverter.Int32BitsToSingle(boneElementIndex));
        }
    }

    private static void ApplyGlobalUvDebugSettings(ref float u0, ref float v0, ref float u1, ref float v1, ref ModelFace face)
    {
        if (UvDebugSettings.FlipU)
        {
            (u0, u1) = (u1, u0);
        }

        if (UvDebugSettings.FlipV)
        {
            (v0, v1) = (v1, v0);
        }

        var offsetU = (float)UvDebugSettings.OffsetUPixels;
        var offsetV = (float)UvDebugSettings.OffsetVPixels;
        if (offsetU != 0f || offsetV != 0f)
        {
            u0 += offsetU;
            u1 += offsetU;
            v0 += offsetV;
            v1 += offsetV;
        }

        var globalRotation = NormalizeRotation90(UvDebugSettings.GlobalFaceRotationDegrees);
        if (globalRotation != 0)
        {
            face = new ModelFace
            {
                TextureKey = face.TextureKey,
                Uv = face.Uv,
                RotationDegrees = NormalizeRotation90(face.RotationDegrees + globalRotation),
            };
        }
    }

    private static string ApplyFaceSemanticDebugRouting(string faceName)
    {
        var mapped = faceName;
        if (UvDebugSettings.SwapFaceNorthSouth)
        {
            mapped = mapped.Equals("north", StringComparison.OrdinalIgnoreCase)
                ? "south"
                : mapped.Equals("south", StringComparison.OrdinalIgnoreCase)
                    ? "north"
                    : mapped;
        }

        if (UvDebugSettings.SwapFaceEastWest)
        {
            mapped = mapped.Equals("east", StringComparison.OrdinalIgnoreCase)
                ? "west"
                : mapped.Equals("west", StringComparison.OrdinalIgnoreCase)
                    ? "east"
                    : mapped;
        }

        if (UvDebugSettings.SwapFaceUpDown)
        {
            mapped = mapped.Equals("up", StringComparison.OrdinalIgnoreCase)
                ? "down"
                : mapped.Equals("down", StringComparison.OrdinalIgnoreCase)
                    ? "up"
                    : mapped;
        }

        return mapped;
    }

    private static void ApplyUvCornerOrderMode(int mode, ref Vector2 uvA, ref Vector2 uvB, ref Vector2 uvC, ref Vector2 uvD)
    {
        switch (mode)
        {
            case 1: // Rotate90
                (uvA, uvB, uvC, uvD) = (uvB, uvC, uvD, uvA);
                break;
            case 2: // Rotate180
                (uvA, uvB, uvC, uvD) = (uvC, uvD, uvA, uvB);
                break;
            case 3: // Rotate270
                (uvA, uvB, uvC, uvD) = (uvD, uvA, uvB, uvC);
                break;
            case 4: // ReverseWinding
                (uvB, uvD) = (uvD, uvB);
                break;
        }
    }

    private static void ApplyFaceUvRotation(int rotationDegrees, ref Vector2 uvA, ref Vector2 uvB, ref Vector2 uvC,
        ref Vector2 uvD)
    {
        rotationDegrees = NormalizeRotation90(rotationDegrees);
        switch (rotationDegrees)
        {
            case 90:
                (uvA, uvB, uvC, uvD) = (uvB, uvC, uvD, uvA);
                break;
            case 180:
                (uvA, uvB, uvC, uvD) = (uvC, uvD, uvA, uvB);
                break;
            case 270:
                (uvA, uvB, uvC, uvD) = (uvD, uvA, uvB, uvC);
                break;
        }
    }

    private static int NormalizeRotation90(int rotationDegrees)
    {
        var normalized = ((rotationDegrees % 360) + 360) % 360;
        return normalized switch
        {
            < 45 => 0,
            < 135 => 90,
            < 225 => 180,
            < 315 => 270,
            _ => 0,
        };
    }

    private static Vector3 W(float x, float y, float z) => new(x / 16f - 0.5f, y / 16f - 0.5f, z / 16f - 0.5f);

    /// <summary>Face corners in Minecraft model texels (before <see cref="W"/> preview scaling).</summary>
    private static bool TryGetFacePlaneRaw(
        string faceName,
        float fx,
        float fy,
        float fz,
        float tx,
        float ty,
        float tz,
        out Vector3 n,
        out Vector3 tang,
        out float wSign,
        out Vector3 c0,
        out Vector3 c1,
        out Vector3 c2,
        out Vector3 c3)
    {
        n = default;
        tang = default;
        wSign = 1f;
        c0 = c1 = c2 = c3 = default;

        switch (faceName.ToLowerInvariant())
        {
            case "north":
                n = new Vector3(0, 0, -1);
                tang = new Vector3(1, 0, 0);
                wSign = 1f;
                c0 = new Vector3(fx, fy, fz);
                c1 = new Vector3(tx, fy, fz);
                c2 = new Vector3(tx, ty, fz);
                c3 = new Vector3(fx, ty, fz);
                return true;
            case "south":
                n = new Vector3(0, 0, 1);
                tang = new Vector3(-1, 0, 0);
                wSign = 1f;
                c0 = new Vector3(tx, fy, tz);
                c1 = new Vector3(fx, fy, tz);
                c2 = new Vector3(fx, ty, tz);
                c3 = new Vector3(tx, ty, tz);
                return true;
            case "west":
                n = new Vector3(-1, 0, 0);
                tang = new Vector3(0, 0, -1);
                wSign = 1f;
                c0 = new Vector3(fx, fy, tz);
                c1 = new Vector3(fx, fy, fz);
                c2 = new Vector3(fx, ty, fz);
                c3 = new Vector3(fx, ty, tz);
                return true;
            case "east":
                n = new Vector3(1, 0, 0);
                tang = new Vector3(0, 0, 1);
                wSign = 1f;
                c0 = new Vector3(tx, fy, fz);
                c1 = new Vector3(tx, fy, tz);
                c2 = new Vector3(tx, ty, tz);
                c3 = new Vector3(tx, ty, fz);
                return true;
            case "up":
                n = new Vector3(0, 1, 0);
                tang = new Vector3(1, 0, 0);
                wSign = 1f;
                c0 = new Vector3(fx, ty, fz);
                c1 = new Vector3(tx, ty, fz);
                c2 = new Vector3(tx, ty, tz);
                c3 = new Vector3(fx, ty, tz);
                return true;
            case "down":
                n = new Vector3(0, -1, 0);
                tang = new Vector3(1, 0, 0);
                wSign = -1f;
                c0 = new Vector3(fx, fy, tz);
                c1 = new Vector3(tx, fy, tz);
                c2 = new Vector3(tx, fy, fz);
                c3 = new Vector3(fx, fy, fz);
                return true;
            default:
                return false;
        }
    }
}
