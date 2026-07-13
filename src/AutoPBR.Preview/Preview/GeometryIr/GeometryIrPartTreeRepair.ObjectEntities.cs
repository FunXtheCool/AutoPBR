using System.Text.Json.Nodes;

namespace AutoPBR.Preview.GeometryIr;

internal static partial class GeometryIrPartTreeRepair
{
    /// <summary>
    /// Direction-mask chest lifts anchor bottom/lid at x=0; reference_java bakes x=1 inset on single-body layers.
    /// Single-body layers use plain <c>addBox</c> (all faces); strip mistaken direction masks from bytecode bleed.
    /// </summary>
    private static void RepairObjectChestModelCuboidOrigins(JsonObject shardDoc)
    {
        var officialJvm = shardDoc["officialJvmName"]?.GetValue<string>();
        if (officialJvm is null ||
            !officialJvm.StartsWith("net.minecraft.client.model.object.chest.ChestModel", StringComparison.Ordinal))
        {
            return;
        }

        if (shardDoc["roots"] is not JsonArray roots)
        {
            return;
        }

        var factoryMethod = shardDoc["factoryMethod"]?.GetValue<string>();
        if (string.Equals(factoryMethod, "createSingleBodyLayer", StringComparison.Ordinal))
        {
            RepairChestSingleBodyFaceMasks(roots);
        }

        if (!string.Equals(officialJvm, "net.minecraft.client.model.object.chest.ChestModel", StringComparison.Ordinal))
        {
            return;
        }

        foreach (var root in roots)
        {
            if (root is not JsonObject rootObj || rootObj["children"] is not JsonArray rootKids)
            {
                continue;
            }

            if (string.Equals(factoryMethod, "createDoubleBodyLeftLayer", StringComparison.Ordinal))
            {
                continue;
            }

            RepairChestPartCuboidOrigin(rootKids, "bottom", dx: 1f);
            RepairChestPartCuboidOrigin(rootKids, "lid", dx: 1f);
            RepairChestLockCuboidOrigin(rootKids, factoryMethod);
        }
    }

    /// <summary>
    /// Cap rings: raw <c>texOffs(-14,13)</c>, top exterior <c>faceMask:["up"]</c>, bottom exterior <c>["down"]</c>.
    /// Legacy lifts may store wrapped <c>uvOrigin [18,13]</c> or dual <c>["down"]</c> on both caps.
    /// </summary>
    private static void RepairDecoratedPotCapCuboids(JsonObject shardDoc)
    {
        if (shardDoc["officialJvmName"]?.GetValue<string>() is not { } officialJvm ||
            !officialJvm.StartsWith("net.minecraft.client.model.DecoratedPotModel", StringComparison.Ordinal) ||
            shardDoc["roots"] is not JsonArray roots)
        {
            return;
        }

        foreach (var root in roots)
        {
            if (root is JsonObject rootObj)
            {
                RepairDecoratedPotCapCuboidsRecursive(rootObj, null);
            }
        }
    }

    private static void RepairDecoratedPotCapCuboidsRecursive(JsonObject part, string? partId)
    {
        partId ??= part["id"]?.GetValue<string>();

        if (part["cuboids"] is JsonArray cuboids)
        {
            foreach (var cuboidNode in cuboids)
            {
                if (cuboidNode is not JsonObject cuboid ||
                    cuboid["uvOrigin"] is not JsonArray uv ||
                    uv.Count < 2 ||
                    cuboid["faceMask"] is not JsonArray faceMask ||
                    faceMask.Count != 1 ||
                    cuboid["from"] is not JsonArray from ||
                    cuboid["to"] is not JsonArray to ||
                    from.Count < 3 ||
                    to.Count < 3)
                {
                    continue;
                }

                var y0 = from[1]?.GetValue<double>() ?? 0;
                var y1 = to[1]?.GetValue<double>() ?? 0;
                if (Math.Abs(y1 - y0) > 0.01)
                {
                    continue;
                }

                var texU = uv[0]?.GetValue<int>() ?? 0;
                var texV = uv[1]?.GetValue<int>() ?? 0;
                if (!GeometryIrUvAtlasQuality.TryIsDecoratedPotCapDownCuboid(texU, texV, 14, 14))
                {
                    continue;
                }

                if (texU != EntityModelRuntime.DecoratedPotCapTexCropRawU ||
                    texV != EntityModelRuntime.DecoratedPotCapTexCropV)
                {
                    uv[0] = EntityModelRuntime.DecoratedPotCapTexCropRawU;
                    uv[1] = EntityModelRuntime.DecoratedPotCapTexCropV;
                }

                var expectedFace = string.Equals(partId, "top", StringComparison.OrdinalIgnoreCase)
                    ? "up"
                    : string.Equals(partId, "bottom", StringComparison.OrdinalIgnoreCase)
                        ? "down"
                        : null;
                if (expectedFace is not null &&
                    !string.Equals(faceMask[0]?.GetValue<string>(), expectedFace, StringComparison.OrdinalIgnoreCase))
                {
                    faceMask[0] = expectedFace;
                }
            }
        }

        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        foreach (var child in kids)
        {
            if (child is JsonObject childObj)
            {
                RepairDecoratedPotCapCuboidsRecursive(childObj, childObj["id"]?.GetValue<string>());
            }
        }
    }

    private static void RepairChestPartCuboidOrigin(JsonArray rootKids, string partId, float dx)
    {
        if (!TryFindPartById(rootKids, partId, out var partNode) || partNode is not JsonObject part ||
            part["cuboids"] is not JsonArray cuboids ||
            cuboids.Count == 0 ||
            cuboids[0] is not JsonObject cuboid ||
            cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count < 3 ||
            to.Count < 3)
        {
            return;
        }

        var fromX = from[0]?.GetValue<double>() ?? 0;
        if (fromX >= 0.5)
        {
            return;
        }

        from[0] = fromX + dx;
        to[0] = (to[0]?.GetValue<double>() ?? 0) + dx;
    }

    private static void RepairChestLockCuboidOrigin(JsonArray rootKids, string? factoryMethod)
    {
        if (!string.Equals(factoryMethod, "createSingleBodyLayer", StringComparison.Ordinal))
        {
            return;
        }

        if (!TryFindPartById(rootKids, "lock", out var partNode) || partNode is not JsonObject part ||
            part["cuboids"] is not JsonArray cuboids ||
            cuboids.Count == 0 ||
            cuboids[0] is not JsonObject cuboid ||
            cuboid["from"] is not JsonArray from ||
            cuboid["to"] is not JsonArray to ||
            from.Count < 3 ||
            to.Count < 3)
        {
            return;
        }

        var fromX = from[0]?.GetValue<double>() ?? 0;
        if (fromX is >= 6 and <= 9)
        {
            return;
        }

        from[0] = 7;
        from[1] = -2;
        from[2] = 14;
        to[0] = 9;
        to[1] = 2;
        to[2] = 15;
    }

    private static void RepairChestSingleBodyFaceMasks(JsonArray roots)
    {
        foreach (var root in roots)
        {
            if (root is JsonObject rootObj)
            {
                RepairChestSingleBodyFaceMasksRecursive(rootObj);
            }
        }
    }

    private static void RepairChestSingleBodyFaceMasksRecursive(JsonObject part)
    {
        if (part["cuboids"] is JsonArray cuboids)
        {
            foreach (var cuboidNode in cuboids)
            {
                if (cuboidNode is JsonObject cuboid)
                {
                    cuboid.Remove("faceMask");
                }
            }
        }

        if (part["children"] is not JsonArray kids)
        {
            return;
        }

        foreach (var child in kids)
        {
            if (child is JsonObject childObj)
            {
                RepairChestSingleBodyFaceMasksRecursive(childObj);
            }
        }
    }

    /// <summary>
    /// Older merged lifts kept every cuboid on <c>#skin</c> with the primary 32² shard atlas. Re-stamp wind tiers to
    /// <c>#wind</c> / 128² and eyes to <c>#eyes</c> / 32² so UV quality gates and emit match
    /// <see cref="AutoPBR.Tools.GeometryCompiler.LayerDefinitionRetainAtlasStamp"/>.
    /// </summary>
    private static void RepairBreezeMultiLayerAtlasTags(JsonObject shardDoc)
    {
        if (!string.Equals(
                shardDoc["officialJvmName"]?.GetValue<string>(),
                "net.minecraft.client.model.monster.breeze.BreezeModel",
                StringComparison.Ordinal) ||
            shardDoc["roots"] is not JsonArray roots)
        {
            return;
        }

        foreach (var root in roots)
        {
            if (root is JsonObject rootObj)
            {
                RepairBreezeMultiLayerAtlasTagsRecursive(rootObj, inheritedWind: false, inheritedEyes: false);
            }
        }
    }

    private static void RepairBreezeMultiLayerAtlasTagsRecursive(
        JsonObject part,
        bool inheritedWind,
        bool inheritedEyes)
    {
        var partId = part["id"]?.GetValue<string>() ?? "";
        var isWind = inheritedWind || partId.StartsWith("wind", StringComparison.OrdinalIgnoreCase);
        var isEyes = inheritedEyes || string.Equals(partId, "eyes", StringComparison.OrdinalIgnoreCase);

        if (part["cuboids"] is JsonArray cuboids)
        {
            foreach (var cuboidNode in cuboids)
            {
                if (cuboidNode is not JsonObject cuboid)
                {
                    continue;
                }

                if (isWind)
                {
                    cuboid["textureWidth"] = 128;
                    cuboid["textureHeight"] = 128;
                    cuboid["textureKey"] = "#wind";
                }
                else if (isEyes)
                {
                    cuboid["textureWidth"] = 32;
                    cuboid["textureHeight"] = 32;
                    cuboid["textureKey"] = "#eyes";
                }
                else if (cuboid["textureWidth"] is not JsonValue tw ||
                         cuboid["textureHeight"] is not JsonValue th ||
                         !tw.TryGetValue<int>(out var atlasW) ||
                         !th.TryGetValue<int>(out var atlasH) ||
                         atlasW <= 0 ||
                         atlasH <= 0)
                {
                    cuboid["textureWidth"] = 32;
                    cuboid["textureHeight"] = 32;
                }
            }
        }

        if (part["children"] is not JsonArray children)
        {
            return;
        }

        foreach (var child in children)
        {
            if (child is JsonObject childObj)
            {
                RepairBreezeMultiLayerAtlasTagsRecursive(childObj, isWind, isEyes);
            }
        }
    }

    /// <summary>
    /// Body-layer lifts keep <c>rod_1</c>/<c>rod_2</c>/<c>rod_3</c> as <c>body</c> siblings beside the empty
    /// <c>rods</c> anchor (y=8). Java binds rods under <c>rods</c>; without renesting they miss the anchor offset.
    /// </summary>
    private static void RepairBreezeRodPartHierarchy(JsonObject shardDoc)
    {
        if (!string.Equals(
                shardDoc["officialJvmName"]?.GetValue<string>(),
                "net.minecraft.client.model.monster.breeze.BreezeModel",
                StringComparison.Ordinal) ||
            shardDoc["roots"] is not JsonArray roots)
        {
            return;
        }

        foreach (var root in roots)
        {
            if (root is not JsonObject rootObj || rootObj["children"] is not JsonArray rootKids)
            {
                continue;
            }

            foreach (var rodId in new[] { "rod_1", "rod_2", "rod_3" })
            {
                ReparentFlatPart(rootKids, rodId, "rods");
            }
        }
    }
}
