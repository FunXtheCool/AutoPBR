using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Optional fields on geometry IR <c>cuboid</c> objects (see <c>docs/generated/schema/geometry-ir.schema.json</c>) produced by
/// the javap mesh lifter. Map <see cref="GetMirrorCuboidUv"/> to the <c>mirrorCuboidUv</c> argument on
/// <see cref="CleanRoomEntityModelRuntime"/>'s internal rig builder when emitting parity cuboids from IR.
/// </summary>
public static class GeometryIrCuboidMetadata
{
    /// <summary>
    /// <c>CubeListBuilder.mirror()</c> in vanilla mesh factories becomes <c>mirrorU: true</c> in lifted IR; use the same flag for
    /// <c>mirrorCuboidUv</c> when baking entity preview cuboids.
    /// </summary>
    public static bool GetMirrorCuboidUv(JsonElement cuboid) =>
        cuboid.ValueKind == JsonValueKind.Object &&
        cuboid.TryGetProperty("mirrorU", out var m) &&
        m.ValueKind == JsonValueKind.True;

    /// <summary>Optional per-cuboid inflate from <c>CubeDeformation</c> when present in IR (many shards omit it).</summary>
    public static bool TryGetInflate(JsonElement cuboid, out float inflate)
    {
        inflate = 0f;
        if (cuboid.ValueKind != JsonValueKind.Object ||
            !cuboid.TryGetProperty("inflate", out var p) ||
            p.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        inflate = (float)p.GetDouble();
        return true;
    }

    /// <summary>
    /// Expands cuboid corners for <c>CubeDeformation</c> when emitting viewport meshes.
    /// Parity emit skips inflate because <c>reference_java</c> bakes store pre-inflate corners.
    /// </summary>
    public static float ApplyCubeDeformationInflateIfNonParity(
        JsonElement cuboid,
        GeometryIrEmitFidelity fidelity,
        ref float x0,
        ref float y0,
        ref float z0,
        ref float x1,
        ref float y1,
        ref float z1)
    {
        var inflate = 0f;
        if (fidelity == GeometryIrEmitFidelity.Parity ||
            !TryGetInflate(cuboid, out inflate) ||
            inflate == 0f)
        {
            return 0f;
        }

        x0 -= inflate;
        y0 -= inflate;
        z0 -= inflate;
        x1 += inflate;
        y1 += inflate;
        z1 += inflate;
        return inflate;
    }

    /// <summary>Optional UV footprint from texCrop-style <c>addBox</c> lifts (<c>uvSpan</c>: <c>[w,h]</c> or <c>[w,h,d]</c>).</summary>
    public static bool TryGetUvSpan(JsonElement cuboid, out int uw, out int uh, out int ud)
    {
        uw = uh = ud = -1;
        if (cuboid.ValueKind != JsonValueKind.Object ||
            !cuboid.TryGetProperty("uvSpan", out var span) ||
            span.ValueKind != JsonValueKind.Array ||
            span.GetArrayLength() < 2)
        {
            return false;
        }

        uw = span[0].GetInt32();
        uh = span[1].GetInt32();
        ud = span.GetArrayLength() >= 3 ? span[2].GetInt32() : -1;
        return true;
    }

    /// <summary>Resolves v2 <c>liftKind</c> or infers from legacy v1 <c>provenance</c>.</summary>
    public static string GetLiftKind(JsonElement cuboid)
    {
        if (cuboid.ValueKind == JsonValueKind.Object &&
            cuboid.TryGetProperty("liftKind", out var lk) &&
            lk.ValueKind == JsonValueKind.String)
        {
            var s = lk.GetString();
            if (!string.IsNullOrEmpty(s))
            {
                return s;
            }
        }

        if (cuboid.TryGetProperty("provenance", out var prov) &&
            prov.ValueKind == JsonValueKind.String)
        {
            var p = prov.GetString() ?? "";
            if (p.Contains("direction_masked_faces_full_box_approx", StringComparison.Ordinal))
            {
                return GeometryIrLiftKinds.DirectionMaskFullBox;
            }
        }

        if (cuboid.TryGetProperty("uvSpan", out _))
        {
            return GeometryIrLiftKinds.TexCropStatic;
        }

        return GeometryIrLiftKinds.Exact;
    }

    /// <summary>Optional included faces for direction-mask cuboids (v2).</summary>
    public static bool TryGetFaceMask(JsonElement cuboid, out string[] faceMask)
    {
        faceMask = [];
        if (cuboid.ValueKind != JsonValueKind.Object ||
            !cuboid.TryGetProperty("faceMask", out var fm) ||
            fm.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var list = new List<string>(fm.GetArrayLength());
        foreach (var el in fm.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrEmpty(s))
                {
                    list.Add(s);
                }
            }
        }

        faceMask = list.ToArray();
        return true;
    }

    /// <summary>Optional per-cuboid atlas when a multi-factory lift merged islands with different <c>LayerDefinition.create</c> sizes.</summary>
    public static bool TryGetAtlasDimensions(JsonElement cuboid, out int atlasWidth, out int atlasHeight)
    {
        atlasWidth = 0;
        atlasHeight = 0;
        if (cuboid.ValueKind != JsonValueKind.Object ||
            !cuboid.TryGetProperty("textureWidth", out var tw) ||
            tw.ValueKind != JsonValueKind.Number ||
            !cuboid.TryGetProperty("textureHeight", out var th) ||
            th.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        var w = tw.GetInt32();
        var h = th.GetInt32();
        if (w <= 0 || h <= 0)
        {
            return false;
        }

        atlasWidth = w;
        atlasHeight = h;
        return true;
    }

    public static bool TryGetTextureKey(JsonElement cuboid, out string textureKey)
    {
        textureKey = "#skin";
        if (cuboid.ValueKind != JsonValueKind.Object ||
            !cuboid.TryGetProperty("textureKey", out var tk) ||
            tk.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var s = tk.GetString();
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        textureKey = s.StartsWith('#') ? s : "#" + s;
        return true;
    }
}
