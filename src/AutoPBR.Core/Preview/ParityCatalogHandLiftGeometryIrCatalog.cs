using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// In-memory geometry IR for block-entity / renderer-only parity builders (no vanilla <c>*Model</c> in client index).
/// Cuboids match <see cref="CleanRoomEntityModelRuntime"/> hand builders at rest pose.
/// </summary>
internal static class ParityCatalogHandLiftGeometryIrCatalog
{
    private static readonly object Gate = new();
    private static FrozenDictionary<string, JsonElement>? _cache;

    public static bool TryGetOkRoot(string officialJvmName, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        EnsureBuilt();
        if (_cache!.TryGetValue(officialJvmName, out var el))
        {
            root = el;
            return true;
        }

        return false;
    }

    private static void EnsureBuilt()
    {
        if (_cache is not null)
        {
            return;
        }

        lock (Gate)
        {
            if (_cache is not null)
            {
                return;
            }

            var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var (jvm, doc) in BuildAll())
            {
                dict[jvm] = doc.RootElement.Clone();
            }

            _cache = dict.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }

    private static IEnumerable<(string Jvm, JsonDocument Doc)> BuildAll()
    {
        yield return ("net.minecraft.client.model.SignModel", Doc(64, 32, "net.minecraft.client.model.SignModel",
            Cuboid(-12, 0, -1, 12, 12, 1, 0, 0),
            Cuboid(-1, 12, -1, 1, 28, 1, 26, 0)));

        yield return ("net.minecraft.client.model.HangingSignModel", Doc(64, 32, "net.minecraft.client.model.HangingSignModel",
            Cuboid(-10, 2, -1, 10, 12, 1, 0, 0),
            Cuboid(-1, 12, -1, 1, 22, 1, 22, 22)));

        yield return ("net.minecraft.client.model.ConduitModel", Doc(64, 32, "net.minecraft.client.model.ConduitModel",
            Cuboid(-6, 2, -6, 6, 14, 6, 0, 0),
            Cuboid(-3, 5, -3, 3, 11, 3, 0, 16)));

        yield return ("net.minecraft.client.model.BeaconBeamModel", Doc(16, 256, "net.minecraft.client.model.BeaconBeamModel",
            Cuboid(-0.25f, 0, -0.25f, 0.25f, 64, 0.25f, 0, 0),
            Cuboid(-0.25f, 0, -0.25f, 0.25f, 64, 0.25f, 0, 0)));

        yield return ("net.minecraft.client.model.EndPortalModel", Doc(16, 256, "net.minecraft.client.model.EndPortalModel",
            Cuboid(-0.25f, 0, -0.25f, 0.25f, 64, 0.25f, 0, 0),
            Cuboid(-0.25f, 0, -0.25f, 0.25f, 64, 0.25f, 0, 0)));

        yield return ("net.minecraft.client.model.EndPortalSurfaceModel", Doc(16, 16, "net.minecraft.client.model.EndPortalSurfaceModel",
            Cuboid(-8, 0, -0.5f, 8, 16, 0.5f, 0, 0)));

        yield return ("net.minecraft.client.model.ExperienceOrbModel", Doc(64, 64, "net.minecraft.client.model.ExperienceOrbModel",
            Cuboid(-4, 0, -4, 4, 8, 4, 0, 0)));

        yield return ("net.minecraft.client.model.FishingHookModel", Doc(64, 64, "net.minecraft.client.model.FishingHookModel",
            Cuboid(-1, -1, -1, 1, 1, 1, 0, 0)));

        yield return ("net.minecraft.client.model.GuardianBeamModel", Doc(16, 256, "net.minecraft.client.model.GuardianBeamModel",
            Cuboid(-0.25f, 0, -0.25f, 0.25f, 64, 0.25f, 0, 0)));

        yield return ("net.minecraft.client.model.DragonFireballModel", Doc(64, 64, "net.minecraft.client.model.DragonFireballModel",
            Cuboid(-4, -4, -4, 4, 4, 4, 0, 0)));

        yield return HandLiftEquipmentHumanoidLeggings();
    }

    private static (string, JsonDocument) HandLiftEquipmentHumanoidLeggings()
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray
            {
                PartWithCuboid("body", Cuboid(4, 12, 6, 12, 24, 10, 16, 16)),
                PartWithCuboid("left_leg", Cuboid(4, 0, 6, 8, 12, 10, 0, 16)),
                PartWithCuboid("right_leg", Cuboid(8, 0, 6, 12, 12, 10, 0, 16)),
            }
        };
        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = "net.minecraft.client.model.EquipmentHumanoidLeggingsModel",
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["textureWidth"] = 64,
            ["textureHeight"] = 64,
            ["factoryMethod"] = "createBodyLayer",
            ["roots"] = new JsonArray { root }
        };
        return ("net.minecraft.client.model.EquipmentHumanoidLeggingsModel", JsonDocument.Parse(doc.ToJsonString()));
    }

    private static JsonObject PartWithCuboid(string id, JsonObject cuboid) =>
        new()
        {
            ["id"] = id,
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray { cuboid },
            ["children"] = new JsonArray()
        };

    private static JsonObject BoatPart(
        string id,
        float tx, float ty, float tz,
        float rx, float ry, float rz,
        float x0, float y0, float z0, float x1, float y1, float z1,
        int u, int v,
        float cuboidRx = 0f, float cuboidRy = 0f, float cuboidRz = 0f,
        float? pivotX = null, float? pivotY = null, float? pivotZ = null)
    {
        var cuboid = Cuboid(x0, y0, z0, x1, y1, z1, u, v);
        if (cuboidRx != 0f || cuboidRy != 0f || cuboidRz != 0f)
        {
            cuboid["cuboidRotationEulerRad"] = new JsonArray { cuboidRx, cuboidRy, cuboidRz };
        }

        if (pivotX is not null && pivotY is not null && pivotZ is not null)
        {
            cuboid["rotationPivot"] = new JsonArray { pivotX.Value, pivotY.Value, pivotZ.Value };
        }

        return new JsonObject
        {
            ["id"] = id,
            ["pose"] = Pose(tx, ty, tz, rx, ry, rz),
            ["cuboids"] = new JsonArray { cuboid },
            ["children"] = new JsonArray()
        };
    }

    private static (string, JsonDocument) HandLiftBoatModel(bool includeChest)
    {
        const float pi = MathF.PI;
        var children = new JsonArray
        {
            BoatPart("bottom", 0, 3, 1, 0, 0, 0, -14, -9, -3, 14, 7, 0, 0, 0,
                cuboidRx: pi / 2f, pivotX: 0, pivotY: -1, pivotZ: -1.5f),
            BoatPart("back", -15, 4, 4, 0, 0, 0, -13, -7, -1, 5, -1, 1, 0, 19,
                cuboidRy: 3f * pi / 2f, pivotX: -4, pivotY: -4, pivotZ: 0),
            BoatPart("front", 15, 4, 0, 0, 0, 0, -8, -7, -1, 8, -1, 1, 0, 27,
                cuboidRy: pi / 2f, pivotX: 0, pivotY: -4, pivotZ: 0),
            BoatPart("right", 0, 4, -9, 0, 0, 0, -14, -7, -1, 14, -1, 1, 0, 35,
                cuboidRy: pi, pivotX: 0, pivotY: -4, pivotZ: 0),
            BoatPart("left", 0, 4, 9, 0, 0, 0, -14, -7, -1, 14, -1, 1, 0, 43),
            BoatPart("left_paddle", 3, -5, 9, 0, 0, 0, -1, 0, -5, 1, 2, 13, 62, 0,
                cuboidRz: 0.19634955f, pivotX: 0, pivotY: 1, pivotZ: 4),
            BoatPart("right_paddle", 3, -5, -9, 0, 0, 0, -1, 0, -5, 1, 2, 13, 62, 20,
                cuboidRy: pi, cuboidRz: 0.19634955f, pivotX: 0, pivotY: 1, pivotZ: 4),
        };

        if (includeChest)
        {
            children.Add(BoatPart("chest_bottom", -2, -5, -6, 0, -pi / 2f, 0, 0, 0, 0, 12, 8, 12, 0, 76));
            children.Add(BoatPart("chest_lid", -2, -9, -6, 0, -pi / 2f, 0, 0, 0, 0, 12, 4, 12, 0, 59));
            children.Add(BoatPart("chest_lock", -1, -6, -1, 0, -pi / 2f, 0, 0, 0, 0, 2, 4, 1, 0, 59));
        }

        var jvm = includeChest
            ? "net.minecraft.client.model.object.boat.ChestBoatModel"
            : "net.minecraft.client.model.object.boat.BoatModel";
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = children
        };
        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = jvm,
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["textureWidth"] = 128,
            ["textureHeight"] = 64,
            ["factoryMethod"] = includeChest ? "createChestBoatModel" : "createBoatModel",
            ["roots"] = new JsonArray { root }
        };
        return (jvm, JsonDocument.Parse(doc.ToJsonString()));
    }

    private static (string, JsonDocument) HandLiftChestModel()
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray
            {
                Cuboid(1, 0, 1, 15, 10, 15, 0, 0),
                Cuboid(1, 10, 1, 15, 14, 15, 0, 19),
                Cuboid(7, 8, 15, 9, 12, 16, 0, 0),
            },
            ["children"] = new JsonArray()
        };
        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = "net.minecraft.client.model.object.chest.ChestModel",
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["textureWidth"] = 64,
            ["textureHeight"] = 64,
            ["factoryMethod"] = "createBodyLayer",
            ["roots"] = new JsonArray { root }
        };
        return ("net.minecraft.client.model.object.chest.ChestModel", JsonDocument.Parse(doc.ToJsonString()));
    }

    private static JsonDocument Doc(int tw, int th, string jvm, params JsonObject[] cuboids)
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(cuboids),
            ["children"] = new JsonArray()
        };
        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = jvm,
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["textureWidth"] = tw,
            ["textureHeight"] = th,
            ["factoryMethod"] = "createBodyLayer",
            ["roots"] = new JsonArray { root }
        };
        return JsonDocument.Parse(doc.ToJsonString());
    }

    private static JsonObject Cuboid(
        float x0, float y0, float z0, float x1, float y1, float z1, int u, int v,
        int? uvW = null, int? uvH = null, int? uvD = null) =>
        CuboidNode(x0, y0, z0, x1, y1, z1, u, v, uvW, uvH, uvD);

    private static JsonObject CuboidNode(
        float x0, float y0, float z0, float x1, float y1, float z1, int u, int v,
        int? uvW = null, int? uvH = null, int? uvD = null)
    {
        var c = new JsonObject
        {
            ["from"] = new JsonArray { x0, y0, z0 },
            ["to"] = new JsonArray { x1, y1, z1 },
            ["uvOrigin"] = new JsonArray { u, v },
            ["liftKind"] = "exact",
            ["faceMask"] = new JsonArray { "down", "up", "north", "south", "east", "west" }
        };
        if (uvW is not null && uvH is not null && uvD is not null)
        {
            c["uvSize"] = new JsonArray { uvW.Value, uvH.Value, uvD.Value };
        }

        return c;
    }

    private static JsonObject Pose(
        float tx = 0, float ty = 0, float tz = 0,
        float rx = 0, float ry = 0, float rz = 0) =>
        new()
        {
            ["translation"] = new JsonArray { tx, ty, tz },
            ["rotationEulerRad"] = new JsonArray { rx, ry, rz },
            ["eulerOrder"] = "XYZ"
        };
}
