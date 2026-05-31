package autopbr.reference;

import com.mojang.blaze3d.vertex.PoseStack;
import org.joml.Matrix4f;
import org.joml.Vector4f;

/**
 * Walks baked {@code ModelPart} trees using vanilla {@code translateAndRotate} (bind pose) and
 * transforms cuboid centers into model texel space for live render parity.
 */
final class ModelPartRenderPoseMath {
    static final class Texel3 {
        final float x;
        final float y;
        final float z;

        Texel3(float x, float y, float z) {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }

    static Texel3 cuboidCenterTexel(Object cube, Matrix4f renderPose) throws Exception {
        var minX = cube.getClass().getField("minX").getFloat(cube);
        var minY = cube.getClass().getField("minY").getFloat(cube);
        var minZ = cube.getClass().getField("minZ").getFloat(cube);
        var maxX = cube.getClass().getField("maxX").getFloat(cube);
        var maxY = cube.getClass().getField("maxY").getFloat(cube);
        var maxZ = cube.getClass().getField("maxZ").getFloat(cube);
        var cx = (minX + maxX) * 0.5f;
        var cy = (minY + maxY) * 0.5f;
        var cz = (minZ + maxZ) * 0.5f;
        var v = new Vector4f(cx / 16f, cy / 16f, cz / 16f, 1f);
        renderPose.transform(v);
        return new Texel3(v.x * 16f, v.y * 16f, v.z * 16f);
    }

    static void walkRenderPart(
            Object part,
            String id,
            PoseStack stack,
            StringBuilder cuboidLines,
            StringBuilder partAffines) throws Exception {
        stack.pushPose();
        part.getClass().getMethod("translateAndRotate", PoseStack.class).invoke(part, stack);
        var renderPose = stack.last().pose();

        if (!"root".equals(id)) {
            partAffines.append(formatRenderAffine(id, renderPose));
        }

        var cubes = GeometryReferenceBake.getCubes(part);
        for (var cube : cubes) {
            var center = cuboidCenterTexel(cube, renderPose);
            cuboidLines.append("""
                ,{
                  "partId": "%s",
                  "renderCenterTexel": [%s, %s, %s]
                }
                """.formatted(
                    GeometryReferenceBake.escape(id),
                    fmt(center.x),
                    fmt(center.y),
                    fmt(center.z)));
        }

        var children = GeometryReferenceBake.getChildren(part);
        for (var entry : children.entrySet()) {
            walkRenderPart(entry.getValue(), entry.getKey(), stack, cuboidLines, partAffines);
        }

        stack.popPose();
    }

    private static String formatRenderAffine(String id, Matrix4f m) {
        return """
            ,{
              "id": "%s",
              "matrixRowMajor": [
                [%s, %s, %s, %s],
                [%s, %s, %s, %s],
                [%s, %s, %s, %s],
                [%s, %s, %s, %s]
              ]
            }
            """.formatted(
            GeometryReferenceBake.escape(id),
            fmt(m.m00()), fmt(m.m01()), fmt(m.m02()), fmt(m.m03()),
            fmt(m.m10()), fmt(m.m11()), fmt(m.m12()), fmt(m.m13()),
            fmt(m.m20()), fmt(m.m21()), fmt(m.m22()), fmt(m.m23()),
            fmt(m.m30()), fmt(m.m31()), fmt(m.m32()), fmt(m.m33()));
    }

    static void appendRenderCenters(Object rootPart, StringBuilder sink) throws Exception {
        sink.append("\n  \"renderCuboidCenters\": [");
        var lines = new StringBuilder();
        var affines = new StringBuilder();
        var stack = new PoseStack();
        resetPartTreePose(rootPart);
        walkRenderPart(rootPart, "root", stack, lines, affines);
        var body = lines.toString();
        if (body.startsWith(",")) {
            body = body.substring(1);
        }

        sink.append(body);
        sink.append("\n  ],\n  \"renderPartAffines\": [");
        var affBody = affines.toString();
        if (affBody.startsWith(",")) {
            affBody = affBody.substring(1);
        }

        sink.append(affBody);
        sink.append("\n  ]");
    }

    private static void resetPartTreePose(Object part) throws Exception {
        var initial = part.getClass().getMethod("getInitialPose").invoke(part);
        part.getClass().getMethod("loadPose", initial.getClass()).invoke(part, initial);
        var children = GeometryReferenceBake.getChildren(part);
        for (var child : children.values()) {
            resetPartTreePose(child);
        }
    }

    private static String fmt(float v) {
        return String.format(java.util.Locale.ROOT, "%.10g", v);
    }

    private ModelPartRenderPoseMath() {}
}
