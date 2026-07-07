using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>
/// In-memory geometry IR for block-entity / renderer-only parity builders (no vanilla <c>*Model</c> in client index).
/// Cuboids match <see cref="EntityModelRuntime"/> hand builders at rest pose.
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
        yield return HandLiftStandingSignModel();
        yield return HandLiftHangingSignModelCeiling();
        yield return HandLiftHangingSignModelWall();
        yield return HandLiftHangingSignModelCeilingMiddle();
        yield return HandLiftDecoratedPotPreviewComposite();

        yield return HandLiftConduitLayer(
            "net.minecraft.client.model.ConduitRenderer.createShellLayer",
            32,
            16,
            "createShellLayer",
            Cuboid(-3, -3, -3, 3, 3, 3, 0, 0));

        yield return HandLiftConduitLayer(
            "net.minecraft.client.model.ConduitRenderer.createCageLayer",
            32,
            16,
            "createCageLayer",
            Cuboid(-4, -4, -4, 4, 4, 4, 0, 0));

        yield return HandLiftConduitLayer(
            "net.minecraft.client.model.ConduitRenderer.createEyeLayer",
            16,
            16,
            "createEyeLayer",
            Cuboid(-4, -4, 0, 4, 4, 0, 0, 0));

        yield return HandLiftConduitLayer(
            "net.minecraft.client.model.ConduitRenderer.createWindLayer",
            64,
            32,
            "createWindLayer",
            Cuboid(-8, -8, -8, 8, 8, 8, 0, 0));

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

    /// <summary>
    /// <c>StandingSignRenderer.createSignLayer</c> (26.1.2 javap): sibling <c>sign</c> + <c>stick</c> under root,
    /// both <c>PartPose.ZERO</c> — joint at model Y = −2 (not parent-chain translated).
    /// </summary>
    private static (string, JsonDocument) HandLiftStandingSignModel()
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray
            {
                PartWithCuboid("sign", Cuboid(-12, -14, -1, 12, -2, 1, 0, 0)),
                PartWithCuboid("stick", Cuboid(-1, -2, -1, 1, 12, 1, 0, 14)),
            }
        };
        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = "net.minecraft.client.model.SignModel",
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["extractionNotes"] = new JsonArray
            {
                "StandingSignRenderer.createSignLayer (26.1.2 client.jar): sign + stick siblings at PartPose.ZERO."
            },
            ["textureWidth"] = 64,
            ["textureHeight"] = 32,
            ["factoryMethod"] = "createSignLayer",
            ["roots"] = new JsonArray { root }
        };
        return ("net.minecraft.client.model.SignModel", JsonDocument.Parse(doc.ToJsonString()));
    }

    private static JsonObject HangingSignBoardPart() =>
        PartWithCuboid("board", Cuboid(-7, 0, -1, 7, 10, 1, 0, 12));

    private static JsonArray HangingSignNormalChainChildren()
    {
        const float chainTilt = 0.7853982f;
        return new JsonArray
        {
            PartWithCuboidPose("chainL1", ChainSheetCuboid(0, 6), -5, -6, 0, 0, 0, -chainTilt),
            PartWithCuboidPose("chainL2", ChainSheetCuboid(6, 6), -5, -6, 0, 0, 0, chainTilt),
            PartWithCuboidPose("chainR1", ChainSheetCuboid(0, 6), 5, -6, 0, 0, 0, -chainTilt),
            PartWithCuboidPose("chainR2", ChainSheetCuboid(6, 6), 5, -6, 0, 0, 0, chainTilt),
        };
    }

    /// <summary>
    /// <c>HangingSignRenderer.createHangingSignLayer</c> CEILING attachment (26.1.2 javap): <c>board</c> +
    /// <c>normalChains</c> with four tilted zero-depth chain sheets; preview uprights them like WALL.
    /// </summary>
    private static (string, JsonDocument) HandLiftHangingSignModelCeiling()
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray
            {
                HangingSignBoardPart(),
                PartWithChildren("normalChains", HangingSignNormalChainChildren()),
            }
        };
        return HandLiftHangingSignDoc(
            EntityPreviewContextTypeCatalog.HangingSignHandLiftJvm,
            "HangingSignRenderer.createHangingSignLayer CEILING (26.1.2 client.jar): board + normalChains.",
            root);
    }

    /// <summary>
    /// <c>HangingSignRenderer.createHangingSignLayer</c> WALL attachment: <c>board</c> + <c>plank</c> +
    /// <c>normalChains</c> (same tilted chain poses as CEILING in 26.1.2 javap; preview uprights them).
    /// </summary>
    private static (string, JsonDocument) HandLiftHangingSignModelWall()
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray
            {
                HangingSignBoardPart(),
                PartWithCuboid("plank", Cuboid(-8, -6, -2, 8, -4, 2, 0, 0)),
                PartWithChildren("normalChains", HangingSignNormalChainChildren()),
            }
        };
        return HandLiftHangingSignDoc(
            EntityPreviewContextTypeCatalog.ResolveHandLiftJvm(EntityPreviewContextTypeCatalog.HangingSignAttachment.Wall),
            "HangingSignRenderer.createHangingSignLayer WALL (26.1.2 client.jar): board + plank + normalChains.",
            root);
    }

    /// <summary>
    /// <c>HangingSignRenderer.createHangingSignLayer</c> CEILING_MIDDLE attachment: <c>board</c> + <c>vChains</c>.
    /// </summary>
    private static (string, JsonDocument) HandLiftHangingSignModelCeilingMiddle()
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = new JsonArray
            {
                HangingSignBoardPart(),
                PartWithCuboid("vChains", VerticalChainSheetCuboid()),
            }
        };
        return HandLiftHangingSignDoc(
            EntityPreviewContextTypeCatalog.ResolveHandLiftJvm(EntityPreviewContextTypeCatalog.HangingSignAttachment.CeilingMiddle),
            "HangingSignRenderer.createHangingSignLayer CEILING_MIDDLE (26.1.2 client.jar): board + vChains.",
            root);
    }

    private static (string, JsonDocument) HandLiftHangingSignDoc(string jvmName, string note, JsonObject root)
    {
        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = jvmName,
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["extractionNotes"] = new JsonArray { note },
            ["textureWidth"] = 64,
            ["textureHeight"] = 32,
            ["factoryMethod"] = "createHangingSignLayer",
            ["roots"] = new JsonArray { root }
        };
        return (jvmName, JsonDocument.Parse(doc.ToJsonString()));
    }

    /// <summary>
    /// <c>DecoratedPotRenderer.createBaseLayer</c> + <c>createSidesLayer</c> (26.1.2 javap): base neck/top/bottom on
    /// <c>#base</c> (32×32) and north-only side sheets on <c>#skin</c> (16×16 pattern).
    /// </summary>
    private static (string, JsonDocument) HandLiftDecoratedPotPreviewComposite()
    {
        const float pi = 3.141592654f;
        var children = new JsonArray
        {
            PartWithCuboidsPose(
                "neck",
                new JsonArray
                {
                    PotBaseCuboid(4, 17, 4, 12, 20, 12, 0, 0),
                    PotBaseCuboid(5, 20, 5, 11, 21, 11, 0, 5),
                },
                0, 37, 16, pi, 0, 0),
            PartWithCuboidPose(
                "top",
                PotCapCuboid(EntityModelRuntime.DecoratedPotCapTexCropRawU, EntityModelRuntime.DecoratedPotCapTexCropV),
                1, 16, 1),
            PartWithCuboidPose(
                "bottom",
                PotCapCuboid(EntityModelRuntime.DecoratedPotCapTexCropRawU, EntityModelRuntime.DecoratedPotCapTexCropV),
                1, 0, 1),
            PartWithCuboidPose(
                "back",
                PotSideCuboid(0, 0, 0, 14, 16, 0, 1, 0),
                15, 16, 1, 0, 0, pi),
            PartWithCuboidPose(
                "left",
                PotSideCuboid(0, 0, 0, 14, 16, 0, 1, 0),
                1, 16, 1, 0, -pi / 2f, pi),
            PartWithCuboidPose(
                "right",
                PotSideCuboid(0, 0, 0, 14, 16, 0, 1, 0),
                15, 16, 15, 0, pi / 2f, pi),
            PartWithCuboidPose(
                "front",
                PotSideCuboid(0, 0, 0, 14, 16, 0, 1, 0),
                1, 16, 15, pi, 0, 0),
        };

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
            ["officialJvmName"] = "net.minecraft.client.model.DecoratedPotModel.previewComposite",
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["extractionNotes"] = new JsonArray
            {
                "DecoratedPotRenderer.createBaseLayer + createSidesLayer (26.1.2 client.jar javap).",
                "Base #base 32x32; sides #skin north-only 16x16."
            },
            ["textureWidth"] = 32,
            ["textureHeight"] = 32,
            ["factoryMethod"] = "createBaseLayer",
            ["roots"] = new JsonArray { root }
        };
        return ("net.minecraft.client.model.DecoratedPotModel.previewComposite", JsonDocument.Parse(doc.ToJsonString()));
    }

    private static JsonObject PotCapCuboid(int u, int v)
    {
        var c = new JsonObject
        {
            ["from"] = new JsonArray { 0, 0, 0 },
            ["to"] = new JsonArray { 14, 0, 14 },
            ["uvOrigin"] = new JsonArray { u, v },
            ["uvSpan"] = new JsonArray { 14, 0, 14 },
            ["textureKey"] = "#base",
            ["faceMask"] = new JsonArray { "down" },
            ["liftKind"] = "exact",
        };
        return c;
    }

    private static JsonObject PotBaseCuboid(
        float x0, float y0, float z0, float x1, float y1, float z1, int u, int v,
        int? uvW = null, int? uvH = null, int? uvD = null)
    {
        var c = CuboidNode(x0, y0, z0, x1, y1, z1, u, v, uvW, uvH, uvD);
        c["textureKey"] = "#base";
        return c;
    }

    private static JsonObject PotSideCuboid(float x0, float y0, float z0, float x1, float y1, float z1, int u, int v)
    {
        var c = CuboidNode(x0, y0, z0, x1, y1, z1, u, v, 14, 16, 0);
        c["textureKey"] = "#skin";
        c["faceMask"] = new JsonArray { "north" };
        return c;
    }

    private static JsonObject PartWithCuboidPose(
        string id,
        JsonObject cuboid,
        float tx = 0,
        float ty = 0,
        float tz = 0,
        float rx = 0,
        float ry = 0,
        float rz = 0) =>
        new()
        {
            ["id"] = id,
            ["pose"] = Pose(tx, ty, tz, rx, ry, rz),
            ["cuboids"] = new JsonArray { cuboid },
            ["children"] = new JsonArray()
        };

    private static JsonObject PartWithCuboidsPose(
        string id,
        JsonArray cuboids,
        float tx = 0,
        float ty = 0,
        float tz = 0,
        float rx = 0,
        float ry = 0,
        float rz = 0) =>
        new()
        {
            ["id"] = id,
            ["pose"] = Pose(tx, ty, tz, rx, ry, rz),
            ["cuboids"] = cuboids,
            ["children"] = new JsonArray()
        };

    private static JsonObject PartWithChildren(string id, JsonArray children) =>
        new()
        {
            ["id"] = id,
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray(),
            ["children"] = children
        };

    private static JsonObject ChainSheetCuboid(int u, int v)
    {
        var cuboid = Cuboid(-1.5f, 0f, -0.03f, 1.5f, 6f, 0.03f, u, v, 3, 6, 0);
        cuboid["faceMask"] = new JsonArray { "north", "south" };
        return cuboid;
    }

    private static JsonObject VerticalChainSheetCuboid()
    {
        var cuboid = Cuboid(-6f, -6f, 0f, 6f, 0f, 0f, 14, 6, 12, 6, 0);
        cuboid["faceMask"] = new JsonArray { "north", "south" };
        return cuboid;
    }

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

    private static (string, JsonDocument) HandLiftConduitLayer(
        string jvm,
        int tw,
        int th,
        string factoryMethod,
        JsonObject cuboid) =>
        (jvm, DocConduitLayer(tw, th, jvm, factoryMethod, cuboid));

    private static JsonDocument Doc(int tw, int th, string jvm, params JsonObject[] cuboids) =>
        Doc(tw, th, jvm, "createBodyLayer", cuboids);

    private static JsonDocument Doc(int tw, int th, string jvm, string factoryMethod, params JsonObject[] cuboids)
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
            ["factoryMethod"] = factoryMethod,
            ["roots"] = new JsonArray { root }
        };
        return JsonDocument.Parse(doc.ToJsonString());
    }

    private static JsonDocument DocConduitLayer(int tw, int th, string jvm, string factoryMethod, JsonObject cuboid)
    {
        var root = new JsonObject
        {
            ["id"] = "root",
            ["pose"] = Pose(),
            ["cuboids"] = new JsonArray { cuboid },
            ["children"] = new JsonArray()
        };
        var doc = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["versionLabel"] = "26.1.2",
            ["officialJvmName"] = jvm,
            ["profile"] = "parity_hand_lift",
            ["extractionStatus"] = "ok",
            ["extractionNotes"] = new JsonArray
            {
                "Preview layer from ConduitRenderer javap (26.1.2 client.jar).",
                "Block preview applies PoseStack.translate(0.5, 0.5, 0.5) via parity RootTransform."
            },
            ["textureWidth"] = tw,
            ["textureHeight"] = th,
            ["factoryMethod"] = factoryMethod,
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
            c["uvSpan"] = new JsonArray { uvW.Value, uvH.Value, uvD.Value };
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
