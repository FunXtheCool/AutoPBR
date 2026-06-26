using System.Text.Json.Nodes;
using static AutoPBR.Tools.GeometryCompiler.GeometryLiftCoordinateRounding;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    private const string LiftExact = "exact";
    private const string LiftDirectionMaskFullBox = "direction_mask_full_box";

    private static JsonObject CreateCuboidJson(
        JsonArray from,
        JsonArray to,
        JsonArray uvOrigin,
        string textureKey,
        string provenance,
        string liftKind,
        IReadOnlyList<string>? liftWarnings,
        IReadOnlyList<string>? faceMask,
        bool mirrorU,
        int? uvSpanW,
        int? uvSpanH,
        int? uvSpanD,
        double? inflate)
    {
        var c = new JsonObject
        {
            ["from"] = from,
            ["to"] = to,
            ["uvOrigin"] = uvOrigin,
            ["textureKey"] = textureKey,
            ["liftKind"] = liftKind,
            ["provenance"] = provenance
        };

        if (liftWarnings is { Count: > 0 })
        {
            var w = new JsonArray();
            foreach (var code in liftWarnings)
            {
                w.Add(code);
            }

            c["liftWarnings"] = w;
        }

        if (faceMask is { Count: > 0 })
        {
            var fm = new JsonArray();
            foreach (var f in faceMask)
            {
                fm.Add(f);
            }

            c["faceMask"] = fm;
        }
        else if (faceMask is not null)
        {
            c["faceMask"] = new JsonArray();
        }

        if (mirrorU)
        {
            c["mirrorU"] = true;
        }

        if (uvSpanW is { } uw && uvSpanH is { } uh)
        {
            c["uvSpan"] = uvSpanD is { } ud
                ? new JsonArray { uw, uh, ud }
                : new JsonArray { uw, uh };
        }

        if (inflate is { } inf && Math.Abs(inf) > 1e-12)
        {
            c["inflate"] = Round(inf);
        }

        return c;
    }

    private static string ResolveLiftKindForAddBox(
        AddBoxInvokeShape shape,
        string addBoxInvokeDescriptor,
        DirectionMaskParseResult maskResult)
    {
        var hasSet = shape == AddBoxInvokeShape.Float6DirectionFaceSet ||
                     shape == AddBoxInvokeShape.Float6StringQuadKeyDirectionFaceSet ||
                     (shape == AddBoxInvokeShape.Unknown &&
                      addBoxInvokeDescriptor.Contains("Ljava/util/Set", StringComparison.Ordinal));

        if (!hasSet)
        {
            return LiftExact;
        }

        return maskResult switch
        {
            DirectionMaskParseResult.ParsedFaces or DirectionMaskParseResult.EmptySet => LiftExact,
            _ => LiftDirectionMaskFullBox
        };
    }

    private static List<string>? BuildCuboidLiftWarnings(
        string liftKind,
        DirectionMaskParseResult maskResult,
        bool cubeDeformationObfInferred)
    {
        var list = new List<string>();
        if (liftKind == LiftDirectionMaskFullBox && maskResult == DirectionMaskParseResult.UnparsedSet)
        {
            list.Add("direction_mask_unparsed_set");
        }

        if (cubeDeformationObfInferred)
        {
            list.Add("cube_deformation_obf_inferred");
        }

        return list.Count > 0 ? list : null;
    }
}
