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
        yield return ("net.minecraft.client.model.BedModel", Doc(64, 64, "net.minecraft.client.model.BedModel",
            Cuboid(0, 0, 0, 16, 16, 6, 0, 0),
            Cuboid(0, 6, 0, 3, 9, 3, 50, 6),
            Cuboid(-16, 6, 0, -13, 9, 3, 50, 18),
            Cuboid(0, 0, 8, 16, 16, 14, 0, 22),
            Cuboid(0, 6, -8, 3, 9, -5, 50, 0),
            Cuboid(-16, 6, -8, -13, 9, -5, 50, 12)));

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

        yield return ("net.minecraft.client.model.SkullModel", Doc(64, 64, "net.minecraft.client.model.SkullModel",
            Cuboid(-4, -8, -4, 4, 0, 4, 0, 0),
            Cuboid(-4.25f, -8.25f, -4.25f, 4.25f, 0.25f, 4.25f, 32, 0)));

        yield return HandLiftEquipmentHumanoidLeggings();

        yield return HandLiftDecoratedPot();
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

    private static (string, JsonDocument) HandLiftDecoratedPot()
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray()
        };
        var cuboids = (JsonArray)root["cuboids"]!;
        cuboids.Add(CuboidNode(4, 17, 4, 12, 20, 12, 0, 0));
        cuboids.Add(CuboidNode(5, 20, 5, 11, 21, 11, 0, 5));
        cuboids.Add(CuboidNode(0, 0, 0, 14, 0, 14, 18, 13, 14, 1, 14));
        cuboids.Add(CuboidNode(0, 0, 0, 14, 0, 14, 18, 13, 14, 1, 14));
        var children = (JsonArray)root["children"]!;
        foreach (var (id, pose, u, v) in new[]
                 {
                     ("back", new float[] { 15, 16, 1, 0, 0, (float)Math.PI }, 1, 32),
                     ("left", new float[] { 1, 16, 1, 0, -(float)Math.PI / 2, (float)Math.PI }, 1, 32),
                     ("right", new float[] { 15, 16, 15, 0, (float)Math.PI / 2, (float)Math.PI }, 1, 32),
                     ("front", new float[] { 1, 16, 15, (float)Math.PI, 0, 0 }, 1, 32),
                 })
        {
            var part = new JsonObject
            {
                ["id"] = id,
                ["pose"] = Pose(pose[0], pose[1], pose[2], pose[3], pose[4], pose[5]),
                ["cuboids"] = new JsonArray
                {
                    CuboidNode(0, 0, 0, 14, 16, 0, u, v, 14, 16, 1)
                },
                ["children"] = new JsonArray()
            };
            children.Add(part);
        }

        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = "net.minecraft.client.model.DecoratedPotModel",
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["textureWidth"] = 64,
            ["textureHeight"] = 64,
            ["factoryMethod"] = "createBodyLayer",
            ["roots"] = new JsonArray { root }
        };
        return ("net.minecraft.client.model.DecoratedPotModel", JsonDocument.Parse(doc.ToJsonString()));
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
