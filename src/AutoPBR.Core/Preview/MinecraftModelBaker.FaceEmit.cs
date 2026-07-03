using System.Numerics;

namespace AutoPBR.Core.Preview;

internal static partial class MinecraftModelBaker
{
    internal static bool TryResolveTextureZipPath(
        string faceTextureKey,
        Dictionary<string, string> textures,
        string defaultNamespace,
        out string textureZipPath)
    {
        textureZipPath = string.Empty;
        if (!TryResolveTextureNotation(faceTextureKey, textures, defaultNamespace, out var notation, out var ns))
        {
            return false;
        }

        var path = notation.Replace('\\', '/');
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

    /// <summary>Follows Java model <c>#alias</c> texture chains (e.g. <c>#down</c> → <c>#end</c> → <c>minecraft:block/oak_log_top</c>).</summary>
    internal static bool TryResolveTextureNotation(
        string faceTextureKey,
        Dictionary<string, string> textures,
        string defaultNamespace,
        out string notation,
        out string resolvedNamespace,
        int depth = 0)
    {
        notation = string.Empty;
        resolvedNamespace = defaultNamespace;
        if (depth > 12)
        {
            return false;
        }

        var key = faceTextureKey.Trim();
        if (key.StartsWith('#'))
        {
            key = key[1..];
        }

        if (!textures.TryGetValue(key, out var texVal) &&
            !textures.TryGetValue("#" + key, out texVal))
        {
            return false;
        }

        texVal = texVal.Trim();
        if (string.IsNullOrEmpty(texVal))
        {
            return false;
        }

        if (texVal.StartsWith('#'))
        {
            return TryResolveTextureNotation(texVal, textures, defaultNamespace, out notation, out resolvedNamespace, depth + 1);
        }

        notation = texVal;
        if (notation.Contains(':', StringComparison.Ordinal))
        {
            resolvedNamespace = notation[..notation.IndexOf(':')];
        }

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
        bool skipPreviewCuboidScale,
        in PreviewUvBakePolicy uvPolicy,
        bool mirrorCuboidUv,
        bool rescaleRotation)
    {
        mirrorCuboidUv &= uvPolicy.MapJavaCuboidFaceCorners;
        if (mirrorCuboidUv)
        {
            (fx, tx) = (tx, fx);
        }

        if (!TryGetFacePlaneRaw(faceName, fx, fy, fz, tx, ty, tz, out var n, out var tang, out var wSign, out var r0,
                out var r1, out var r2, out var r3))
        {
            return false;
        }

        if (uvPolicy.MapJavaCuboidFaceCorners)
        {
            ApplyJavaCuboidPolygonCornerOrder(ref r0, ref r1, ref r2, ref r3);
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

        if (rescaleRotation)
        {
            var cu = (u0 + u1) * 0.5f;
            var cv = (v0 + v1) * 0.5f;
            var hu = (u1 - u0) * 0.5f * MathF.Sqrt(2f);
            var hv = (v1 - v0) * 0.5f * MathF.Sqrt(2f);
            u0 = cu - hu;
            u1 = cu + hu;
            v0 = cv - hv;
            v1 = cv + hv;
        }

        ApplyPolicyUvSettings(ref u0, ref v0, ref u1, ref v1, ref face, in uvPolicy);

        var useBottomLeftOrigin = uvPolicy.UseBottomLeftUvOrigin;
        var preserveDirectionalBounds = uvPolicy.PreserveDirectionalBounds;
        var mapJavaCorners = uvPolicy.MapJavaCuboidFaceCorners;
        float Nu(float px) => NormalizeAtlasTexel(px, texW) / texW;
        float Nv(float py) => useBottomLeftOrigin
            ? 1f - NormalizeAtlasTexel(py, texH) / texH
            : NormalizeAtlasTexel(py, texH) / texH;

        Vector2 uvA;
        Vector2 uvB;
        Vector2 uvC;
        Vector2 uvD;
        if (mapJavaCorners)
        {
            // ModelPart.Cube polygon order maps c1,c0,c3,c2 to (u0,v0),(u1,v0),(u1,v1),(u0,v1).
            uvB = new Vector2(Nu(u0), Nv(v0));
            uvA = new Vector2(Nu(u1), Nv(v0));
            uvD = new Vector2(Nu(u1), Nv(v1));
            uvC = new Vector2(Nu(u0), Nv(v1));
        }
        else if (preserveDirectionalBounds)
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

        ApplyUvCornerOrderMode(uvPolicy.UvCornerOrderMode, ref uvA, ref uvB, ref uvC, ref uvD);
        ApplyFaceUvRotation(face.RotationDegrees, ref uvA, ref uvB, ref uvC, ref uvD);

        if (mirrorCuboidUv)
        {
            ApplyJavaMirroredPolygonVertexOrder(ref c0, ref c1, ref c2, ref c3, ref uvA, ref uvB, ref uvC, ref uvD);
            if (IsXAxisFace(faceName))
            {
                n = -n;
            }
        }

        DeriveTangentBasis(c0, c1, c2, c3, uvA, uvB, uvC, uvD, n, tang, wSign, out tang, out wSign);

        var stride = appendBoneIndex ? FloatsPerSkinnedVertex : FloatsPerVertex;
        var baseIndex = (uint)(v.Count / stride);
        AddVert(v, c0, n, uvA, tang, wSign, appendBoneIndex, boneElementIndex);
        AddVert(v, c1, n, uvB, tang, wSign, appendBoneIndex, boneElementIndex);
        AddVert(v, c2, n, uvC, tang, wSign, appendBoneIndex, boneElementIndex);
        AddVert(v, c3, n, uvD, tang, wSign, appendBoneIndex, boneElementIndex);

        if (uvPolicy.ReverseFaceWinding)
        {
            idx.Add(baseIndex);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex + 1);
            idx.Add(baseIndex);
            idx.Add(baseIndex + 3);
            idx.Add(baseIndex + 2);
        }
        else
        {
            idx.Add(baseIndex);
            idx.Add(baseIndex + 1);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex);
            idx.Add(baseIndex + 2);
            idx.Add(baseIndex + 3);
        }

        return true;
    }

    private static void DeriveTangentBasis(
        Vector3 c0,
        Vector3 c1,
        Vector3 c2,
        Vector3 c3,
        Vector2 uv0,
        Vector2 uv1,
        Vector2 uv2,
        Vector2 uv3,
        Vector3 normal,
        Vector3 fallbackTangent,
        float fallbackWSign,
        out Vector3 tangent,
        out float wSign)
    {
        if (TryDeriveTangentBasis(c0, c1, c2, uv0, uv1, uv2, normal, out tangent, out wSign) ||
            TryDeriveTangentBasis(c0, c2, c3, uv0, uv2, uv3, normal, out tangent, out wSign))
        {
            return;
        }

        tangent = OrthogonalizeOrFallback(fallbackTangent, normal);
        wSign = fallbackWSign;
    }

    private static bool TryDeriveTangentBasis(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector2 uv0,
        Vector2 uv1,
        Vector2 uv2,
        Vector3 normal,
        out Vector3 tangent,
        out float wSign)
    {
        tangent = default;
        wSign = 1f;

        var edge1 = p1 - p0;
        var edge2 = p2 - p0;
        var duv1 = uv1 - uv0;
        var duv2 = uv2 - uv0;
        var det = duv1.X * duv2.Y - duv1.Y * duv2.X;
        if (MathF.Abs(det) < 1e-8f)
        {
            return false;
        }

        var invDet = 1f / det;
        var rawTangent = (edge1 * duv2.Y - edge2 * duv1.Y) * invDet;
        var rawBitangent = (edge2 * duv1.X - edge1 * duv2.X) * invDet;
        tangent = rawTangent - normal * Vector3.Dot(normal, rawTangent);
        if (!TryNormalize(tangent, out tangent) || !TryNormalize(rawBitangent, out var bitangent))
        {
            return false;
        }

        wSign = Vector3.Dot(Vector3.Cross(normal, tangent), bitangent) < 0f ? -1f : 1f;
        return true;
    }

    private static Vector3 OrthogonalizeOrFallback(Vector3 tangent, Vector3 normal)
    {
        var ortho = tangent - normal * Vector3.Dot(normal, tangent);
        if (TryNormalize(ortho, out ortho))
        {
            return ortho;
        }

        var axis = MathF.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        ortho = Vector3.Cross(axis, normal);
        return TryNormalize(ortho, out ortho) ? ortho : Vector3.UnitX;
    }

    private static bool TryNormalize(Vector3 v, out Vector3 normalized)
    {
        var lenSq = v.LengthSquared();
        if (lenSq < 1e-12f || float.IsNaN(lenSq) || float.IsInfinity(lenSq))
        {
            normalized = default;
            return false;
        }

        normalized = v / MathF.Sqrt(lenSq);
        return true;
    }

    /// <summary>
    /// Java texel corners may be negative or slightly past the atlas edge (template gap); map into
    /// <c>[0, atlasSize]</c> before UV normalization. Inclusive <c>atlasSize</c> is UV 1.0.
    /// Naive modulo collapses <c>64→0</c> on a 64px sheet and scrambles cuboids; modulo on overflow
    /// (e.g. camel tail south <c>u=130</c> on 128px) smears thin membranes.
    /// </summary>
    internal static float NormalizeAtlasTexel(float px, int atlasSize)
    {
        if (atlasSize <= 0)
        {
            return px;
        }

        if (px >= 0f && px <= atlasSize)
        {
            return px;
        }

        if (px < 0f)
        {
            var wrapped = px % atlasSize;
            if (wrapped < 0f)
            {
                wrapped += atlasSize;
            }

            return wrapped;
        }

        return atlasSize;
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
            // Stored as IEEE bits; shader decodes with floatBitsToInt (GLES/ANGLE-safe vs int vertex attribs).
            v.Add(BitConverter.Int32BitsToSingle(boneElementIndex));
        }
    }

    private static void ApplyPolicyUvSettings(
        ref float u0,
        ref float v0,
        ref float u1,
        ref float v1,
        ref ModelFace face,
        in PreviewUvBakePolicy policy)
    {
        if (policy.FlipU)
        {
            (u0, u1) = (u1, u0);
        }

        if (policy.FlipV)
        {
            (v0, v1) = (v1, v0);
        }

        if (policy.OffsetUPixels != 0f || policy.OffsetVPixels != 0f)
        {
            u0 += policy.OffsetUPixels;
            u1 += policy.OffsetUPixels;
            v0 += policy.OffsetVPixels;
            v1 += policy.OffsetVPixels;
        }

        var globalRotation = NormalizeRotation90(policy.GlobalFaceRotationDegrees);
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

    internal static string ApplyFaceSemanticRouting(string faceName, in PreviewUvBakePolicy policy)
    {
        var mapped = faceName;
        if (policy.SwapFaceNorthSouth)
        {
            mapped = mapped.Equals("north", StringComparison.OrdinalIgnoreCase)
                ? "south"
                : mapped.Equals("south", StringComparison.OrdinalIgnoreCase)
                    ? "north"
                    : mapped;
        }

        if (policy.SwapFaceEastWest)
        {
            mapped = mapped.Equals("east", StringComparison.OrdinalIgnoreCase)
                ? "west"
                : mapped.Equals("west", StringComparison.OrdinalIgnoreCase)
                    ? "east"
                    : mapped;
        }

        if (policy.SwapFaceUpDown)
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

    private static void ApplyJavaCuboidPolygonCornerOrder(
        ref Vector3 c0,
        ref Vector3 c1,
        ref Vector3 c2,
        ref Vector3 c3)
    {
        // ModelPart.Cube constructs every face polygon as raw corners [c1,c0,c3,c2].
        (c0, c1, c2, c3) = (c1, c0, c3, c2);
    }

    private static void ApplyJavaMirroredPolygonVertexOrder(
        ref Vector3 c0,
        ref Vector3 c1,
        ref Vector3 c2,
        ref Vector3 c3,
        ref Vector2 uv0,
        ref Vector2 uv1,
        ref Vector2 uv2,
        ref Vector2 uv3)
    {
        // ModelPart.Polygon reverses the vertex array after UV remap when CubeListBuilder.mirror() is active.
        (c0, c1, c2, c3) = (c3, c2, c1, c0);
        (uv0, uv1, uv2, uv3) = (uv3, uv2, uv1, uv0);
    }

    private static bool IsXAxisFace(string faceName) =>
        faceName.Equals("west", StringComparison.OrdinalIgnoreCase) ||
        faceName.Equals("east", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>Exposes cuboid face corners for Java <c>ModelPart.Cube</c> parity audits (tests only).</summary>
    internal static bool TryGetFaceCornerBoundsForAudit(
        string faceName,
        float fx,
        float fy,
        float fz,
        float tx,
        float ty,
        float tz,
        out float minY,
        out float maxY,
        out Vector3 normal)
    {
        minY = maxY = 0;
        normal = default;
        if (!TryGetFacePlaneRaw(faceName, fx, fy, fz, tx, ty, tz, out normal, out _, out _, out var c0, out var c1, out var c2,
                out var c3))
        {
            return false;
        }

        minY = MathF.Min(MathF.Min(c0.Y, c1.Y), MathF.Min(c2.Y, c3.Y));
        maxY = MathF.Max(MathF.Max(c0.Y, c1.Y), MathF.Max(c2.Y, c3.Y));
        return true;
    }

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
