package autopbr.reference;

import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * Calls vanilla {@code createBodyLayer()} / {@code createMesh()} on a pinned client.jar and dumps
 * part/cuboid JSON aligned with AutoPBR geometry IR schema v2 (model-local corners + PartPose).
 */
public final class GeometryReferenceBake {
    private static final String VERSION_LABEL = "26.1.2";

    private static final String[] PREFERRED_FACTORY_METHODS = {
            "createBodyLayer",
            "createBodyModel",
            "createNoHatModel",
            "createHeadModel",
            "createBoatModel",
            "createRaftModel",
            "createFlagLayer",
            "createArmorLayerSet",
            "createLayer",
            "createSpiderBodyLayer",
            "createOuterBodyLayer",
            "createInnerBodyLayer",
            "createMesh",
            "createCapeLayer",
            "createBabyMesh",
            "createBabyLayer",
            "createBaseChickenModel",
            "createBodyMesh",
            "createSingleBodyLayer",
            "createDoubleBodyRightLayer",
            "createDoubleBodyLeftLayer",
            "createHeadLayer",
            "createHatLayer",
            "createEarsLayer",
            "createTranslucentBodyLayer",
            "createSaddleLayer",
            "createHarnessLayer",
            "createFurLayer",
            "createBasePigModel",
            "createArmorLayer",
            "createWindLayer",
    };

    private static final String[] SUPPLEMENTARY_LAYER_FACTORIES = {
            "createWindLayer",
    };

    private record MeshHostResolution(Class<?> host, String factoryMethod) {}

    public static void main(String[] args) throws Exception {
        if (args.length == 0) {
            System.err.println("Usage: GeometryReferenceBake <official.jvm.ModelClass> [factoryMethod]");
            System.exit(2);
        }

        var fqn = args[0];
        var factoryMethod = args.length > 1 ? args[1] : "createBodyLayer";
        var modelClass = Class.forName(fqn);
        var resolved = resolveMeshHostWithFactory(modelClass, factoryMethod);

        var layer = invokeMeshFactory(resolved.host(), resolved.factoryMethod());
        var bakeRoot = layer.getClass().getMethod("bakeRoot").invoke(layer);
        var rootPart = (Object) bakeRoot;

        for (var supplementary : SUPPLEMENTARY_LAYER_FACTORIES) {
            if (supplementary.equals(resolved.factoryMethod())) {
                continue;
            }

            if (!hasMeshFactory(resolved.host(), supplementary)) {
                continue;
            }

            var extraLayer = invokeMeshFactory(resolved.host(), supplementary);
            var extraRoot = extraLayer.getClass().getMethod("bakeRoot").invoke(extraLayer);
            mergeSupplementaryPartCuboids(rootPart, extraRoot);
        }

        var roots = new ArrayList<String>();
        roots.add(walkPart(rootPart, "root", PartWorldPoseMath.Mat4.identity()));

        var json = """
            {
              "schemaVersion": 2,
              "versionLabel": "%s",
              "officialJvmName": "%s",
              "extractionStatus": "reference_java",
              "factoryMethod": "%s",
              "meshHostJvmName": "%s",
              "roots": [%s]
            }
            """.formatted(VERSION_LABEL, fqn, resolved.factoryMethod(), resolved.host().getName(),
                String.join(",", roots));

        var outDir = Path.of("reference-output");
        Files.createDirectories(outDir);
        var out = outDir.resolve(fqn + ".json");
        Files.writeString(out, json);
        System.out.println("Wrote reference: " + out.toAbsolutePath());
    }

    private static MeshHostResolution resolveMeshHostWithFactory(Class<?> modelClass, String requestedFactoryMethod)
            throws ClassNotFoundException {
        var methods = factoryMethodsToTry(requestedFactoryMethod);
        for (var candidate : enumerateMeshHostCandidates(modelClass)) {
            for (var method : methods) {
                if (hasMeshFactory(candidate, method)) {
                    return new MeshHostResolution(candidate, method);
                }
            }
        }

        throw new ClassNotFoundException(
                "No static mesh factory for " + modelClass.getName() + " (tried mesh-host candidates)");
    }

    private static List<String> factoryMethodsToTry(String requested) {
        var out = new ArrayList<String>();
        if (requested != null && !requested.isBlank()) {
            out.add(requested);
        }

        for (var m : PREFERRED_FACTORY_METHODS) {
            if (!out.contains(m)) {
                out.add(m);
            }
        }

        return out;
    }

    private static boolean hasMeshFactory(Class<?> modelClass, String factoryMethod) {
        for (var m : modelClass.getDeclaredMethods()) {
            if (!Modifier.isStatic(m.getModifiers()) || !m.getName().equals(factoryMethod)) {
                continue;
            }

            var ret = m.getReturnType().getName();
            if (ret.contains("LayerDefinition") || ret.contains("MeshDefinition")
                    || ret.contains("ArmorModelSet")) {
                return true;
            }
        }

        return false;
    }

    /** Mirrors AutoPBR MeshHostClassCandidates.Enumerate for reference bakes. */
    private static List<Class<?>> enumerateMeshHostCandidates(Class<?> modelClass) {
        var out = new ArrayList<Class<?>>();
        var seen = new java.util.HashSet<String>();
        appendCandidate(out, seen, modelClass);
        var pkg = modelClass.getPackageName();
        var simple = modelClass.getSimpleName();
        if (!simple.endsWith("Model")) {
            return out;
        }

        var stem = simple.substring(0, simple.length() - "Model".length());
        if (stem.isEmpty()) {
            return out;
        }

        if (stem.startsWith("Abstract") && stem.length() > "Abstract".length()) {
            var rest = stem.substring("Abstract".length());
            appendCandidate(out, seen, tryLoad(pkg + "." + rest + "Model"));
            appendCandidate(out, seen, tryLoad(pkg + ".Adult" + rest + "Model"));
            appendCandidate(out, seen, tryLoad(pkg + ".Baby" + rest + "Model"));
        }

        if (pkg.endsWith(".animal.feline")
                && (stem.startsWith("AdultCat")
                        || stem.startsWith("BabyCat")
                        || stem.startsWith("AdultOcelot")
                        || stem.startsWith("BabyOcelot")
                        || stem.equals("Cat")
                        || stem.equals("Ocelot"))) {
            appendCandidate(out, seen, tryLoad(pkg + ".AdultFelineModel"));
            appendCandidate(out, seen, tryLoad(pkg + ".BabyFelineModel"));
        }

        appendCandidate(out, seen, tryLoad(pkg + ".Adult" + stem + "Model"));
        appendCandidate(out, seen, tryLoad(pkg + ".Baby" + stem + "Model"));
        appendCandidate(out, seen, tryLoad(pkg + ".Cold" + stem + "Model"));
        appendCandidate(out, seen, tryLoad(pkg + ".Warm" + stem + "Model"));
        return out;
    }

    private static void appendCandidate(List<Class<?>> out, java.util.HashSet<String> seen, Class<?> c) {
        if (c != null && seen.add(c.getName())) {
            out.add(c);
        }
    }

    private static Class<?> tryLoad(String fqn) {
        try {
            return Class.forName(fqn);
        } catch (ClassNotFoundException ignored) {
            return null;
        }
    }

    private static Object invokeMeshFactory(Class<?> modelClass, String factoryMethod) throws Exception {
        for (var m : modelClass.getDeclaredMethods()) {
            if (!Modifier.isStatic(m.getModifiers()) || !m.getName().equals(factoryMethod)) {
                continue;
            }

            var ret = m.getReturnType().getName();
            if (ret.contains("ArmorModelSet")) {
                m.setAccessible(true);
                var raw = invokeWithDefaults(m);
                return wrapMeshFactoryResult(unwrapArmorModelSetLayer(raw));
            }

            if (!ret.contains("LayerDefinition") && !ret.contains("MeshDefinition")) {
                continue;
            }

            m.setAccessible(true);
            var raw = invokeWithDefaults(m);
            return wrapMeshFactoryResult(raw);
        }

        throw new IllegalStateException("No static mesh factory " + factoryMethod + " on " + modelClass.getName());
    }

    private static Object unwrapArmorModelSetLayer(Object armorModelSet) throws Exception {
        for (var slot : new String[] { "head", "chest", "legs", "feet" }) {
            var layer = armorModelSet.getClass().getMethod(slot).invoke(armorModelSet);
            if (layer != null) {
                return layer;
            }
        }

        throw new IllegalStateException("ArmorModelSet has no layer slots");
    }

    private static Object invokeWithDefaults(Method m) throws Exception {
        var params = m.getParameterTypes();
        if (params.length == 0) {
            return m.invoke(null);
        }

        var args = new Object[params.length];
        for (var i = 0; i < params.length; i++) {
            var p = params[i];
            if (p.getName().contains("CubeDeformation")) {
                var none = p.getField("NONE").get(null);
                args[i] = none;
            } else if (p == int.class) {
                args[i] = defaultIntArgument(m, i);
            } else if (p == float.class) {
                args[i] = 0f;
            } else if (p == boolean.class) {
                args[i] = false;
            } else {
                throw new IllegalStateException("Unsupported factory param: " + p.getName());
            }
        }

        return m.invoke(null, args);
    }

    /**
     * {@code QuadrupedModel.createBodyMesh(int,…)} passes the first int through {@code createLegs} as leg/body
     * height (adult template default 6). Texture atlas dimensions still use 64 via {@link #wrapMeshFactoryResult}.
     */
    private static int defaultIntArgument(Method method, int paramIndex) {
        if (paramIndex == 0 && "createBodyMesh".equals(method.getName())) {
            var params = method.getParameterTypes();
            if (params.length > 0
                    && params[0] == int.class
                    && method.getReturnType().getName().contains("MeshDefinition")) {
                return 6;
            }
        }

        return 64;
    }

    private static Object wrapMeshFactoryResult(Object raw) throws Exception {
        if (raw == null) {
            throw new IllegalStateException("Mesh factory returned null");
        }

        if (raw.getClass().getName().contains("LayerDefinition")) {
            return raw;
        }

        if (raw.getClass().getName().contains("MeshDefinition")) {
            var layerClass = Class.forName("net.minecraft.client.model.geom.builders.LayerDefinition");
            var create = layerClass.getMethod("create", raw.getClass(), int.class, int.class);
            return create.invoke(null, raw, 64, 64);
        }

        throw new IllegalStateException("Unexpected factory return: " + raw.getClass().getName());
    }

    /** Merges cuboids from supplementary layer parts (e.g. Breeze createWindLayer) into the primary bake tree by part id. */
    private static void mergeSupplementaryPartCuboids(Object primaryRoot, Object supplementaryRoot) throws Exception {
        var supplementaryById = new java.util.HashMap<String, Object>();
        indexPartsById(supplementaryRoot, supplementaryById);
        for (var e : supplementaryById.entrySet()) {
            var primaryPart = findPartById(primaryRoot, e.getKey());
            if (primaryPart == null) {
                continue;
            }

            var mergedCubes = new ArrayList<>(getCubes(primaryPart));
            mergedCubes.addAll(getCubes(e.getValue()));
            setCubes(primaryPart, mergedCubes);
        }
    }

    private static void indexPartsById(Object part, java.util.Map<String, Object> byId) throws Exception {
        for (var e : getChildren(part).entrySet()) {
            byId.putIfAbsent(e.getKey(), e.getValue());
            indexPartsById(e.getValue(), byId);
        }
    }

    private static Object findPartById(Object part, String id) throws Exception {
        var children = getChildren(part);
        if (children.containsKey(id)) {
            return children.get(id);
        }

        for (var child : children.values()) {
            var found = findPartById(child, id);
            if (found != null) {
                return found;
            }
        }

        return null;
    }

    private static String walkPart(Object part, String id, PartWorldPoseMath.Mat4 parentWorld) throws Exception {
        var pose = part.getClass().getMethod("getInitialPose").invoke(part);
        var poseJson = formatPose(pose);
        var local = PartWorldPoseMath.Mat4.fromVanillaPose(pose);
        var world = PartWorldPoseMath.Mat4.mul(parentWorld, local);
        var worldPoseJson = formatWorldPose(world);

        var cuboids = new ArrayList<String>();
        var cubes = getCubes(part);
        for (var cube : cubes) {
            cuboids.add(formatCuboid(cube));
        }

        var children = new ArrayList<String>();
        var childMap = getChildren(part);
        for (var e : childMap.entrySet()) {
            children.add(walkPart(e.getValue(), e.getKey(), world));
        }

        var cuboidArr = cuboids.isEmpty() ? "" : String.join(",", cuboids);
        var childArr = children.isEmpty() ? "" : String.join(",", children);

        return """
            {
              "id": "%s",
              "pose": %s,
              "worldPose": %s,
              "cuboids": [%s],
              "children": [%s]
            }
            """.formatted(escape(id), poseJson, worldPoseJson, cuboidArr, childArr);
    }

    private static String formatWorldPose(PartWorldPoseMath.Mat4 world) {
        return """
            {
              "translation": [%s, %s, %s],
              "eulerOrder": "XYZ"
            }
            """.formatted(fmt(world.tx()), fmt(world.ty()), fmt(world.tz()));
    }

    @SuppressWarnings("unchecked")
    private static List<Object> getCubes(Object part) throws Exception {
        var f = part.getClass().getDeclaredField("cubes");
        f.setAccessible(true);
        return (List<Object>) f.get(part);
    }

    private static void setCubes(Object part, List<Object> cubes) throws Exception {
        var f = part.getClass().getDeclaredField("cubes");
        f.setAccessible(true);
        f.set(part, cubes);
    }

    @SuppressWarnings("unchecked")
    private static Map<String, Object> getChildren(Object part) throws Exception {
        var f = part.getClass().getDeclaredField("children");
        f.setAccessible(true);
        return (Map<String, Object>) f.get(part);
    }

    private static String formatPose(Object pose) throws Exception {
        return """
            {
              "translation": [%s, %s, %s],
              "rotationEulerRad": [%s, %s, %s],
              "eulerOrder": "XYZ"
            }
            """.formatted(
                fmt(pose.getClass().getMethod("x").invoke(pose)),
                fmt(pose.getClass().getMethod("y").invoke(pose)),
                fmt(pose.getClass().getMethod("z").invoke(pose)),
                fmt(pose.getClass().getMethod("xRot").invoke(pose)),
                fmt(pose.getClass().getMethod("yRot").invoke(pose)),
                fmt(pose.getClass().getMethod("zRot").invoke(pose)));
    }

    private static String formatCuboid(Object cube) throws Exception {
        var minX = cube.getClass().getField("minX").getFloat(cube);
        var minY = cube.getClass().getField("minY").getFloat(cube);
        var minZ = cube.getClass().getField("minZ").getFloat(cube);
        var maxX = cube.getClass().getField("maxX").getFloat(cube);
        var maxY = cube.getClass().getField("maxY").getFloat(cube);
        var maxZ = cube.getClass().getField("maxZ").getFloat(cube);
        return """
            {
              "from": [%s, %s, %s],
              "to": [%s, %s, %s],
              "uvOrigin": [0, 0],
              "liftKind": "reference_java"
            }
            """.formatted(
                fmt(minX), fmt(minY), fmt(minZ),
                fmt(maxX), fmt(maxY), fmt(maxZ));
    }

    private static String fmt(Object v) {
        if (v instanceof Float f) {
            return String.format(java.util.Locale.ROOT, "%.10g", f);
        }

        if (v instanceof Double d) {
            return String.format(java.util.Locale.ROOT, "%.10g", d);
        }

        return String.valueOf(v);
    }

    private static String escape(String s) {
        return s.replace("\\", "\\\\").replace("\"", "\\\"");
    }
}
