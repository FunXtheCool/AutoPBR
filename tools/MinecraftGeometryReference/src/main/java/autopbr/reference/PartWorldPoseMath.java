package autopbr.reference;

/**
 * Composes vanilla {@code PartPose} chains for reference bakes — mirrors AutoPBR
 * {@code EntityParityTemplate} / {@code GeometryIrMeshEmitter.TryComposePartPose} (row-vector {@code System.Numerics}).
 */
final class PartWorldPoseMath {
    /** Row-major 4×4; translation in {@code m[3][0..2]} (M41–M43). */
    static final class Mat4 {
        final float[][] m = new float[4][4];

        static Mat4 identity() {
            var out = new Mat4();
            for (var i = 0; i < 4; i++) {
                out.m[i][i] = 1f;
            }
            return out;
        }

        static Mat4 mul(Mat4 a, Mat4 b) {
            var out = new Mat4();
            for (var row = 0; row < 4; row++) {
                for (var col = 0; col < 4; col++) {
                    var sum = 0f;
                    for (var k = 0; k < 4; k++) {
                        sum += a.m[row][k] * b.m[k][col];
                    }
                    out.m[row][col] = sum;
                }
            }
            return out;
        }

        static Mat4 translation(float x, float y, float z) {
            var out = identity();
            out.m[3][0] = x;
            out.m[3][1] = y;
            out.m[3][2] = z;
            return out;
        }

        static Mat4 rotationX(float radians) {
            var c = (float) Math.cos(radians);
            var s = (float) Math.sin(radians);
            var out = identity();
            out.m[1][1] = c;
            out.m[1][2] = s;
            out.m[2][1] = -s;
            out.m[2][2] = c;
            return out;
        }

        static Mat4 rotationY(float radians) {
            var c = (float) Math.cos(radians);
            var s = (float) Math.sin(radians);
            var out = identity();
            out.m[0][0] = c;
            out.m[0][2] = -s;
            out.m[2][0] = s;
            out.m[2][2] = c;
            return out;
        }

        static Mat4 rotationZ(float radians) {
            var c = (float) Math.cos(radians);
            var s = (float) Math.sin(radians);
            var out = identity();
            out.m[0][0] = c;
            out.m[0][1] = s;
            out.m[1][0] = -s;
            out.m[1][1] = c;
            return out;
        }

        /** {@code Rz * Ry * Rx} — geometry IR default {@code eulerOrder: XYZ}. */
        static Mat4 eulerXyz(float xRad, float yRad, float zRad) {
            return mul(mul(rotationZ(zRad), rotationY(yRad)), rotationX(xRad));
        }

        static Mat4 fromVanillaPose(Object pose) throws Exception {
            var x = ((Number) pose.getClass().getMethod("x").invoke(pose)).floatValue();
            var y = ((Number) pose.getClass().getMethod("y").invoke(pose)).floatValue();
            var z = ((Number) pose.getClass().getMethod("z").invoke(pose)).floatValue();
            var xRot = ((Number) pose.getClass().getMethod("xRot").invoke(pose)).floatValue();
            var yRot = ((Number) pose.getClass().getMethod("yRot").invoke(pose)).floatValue();
            var zRot = ((Number) pose.getClass().getMethod("zRot").invoke(pose)).floatValue();
            return mul(translation(x, y, z), eulerXyz(xRot, yRot, zRot));
        }

        float tx() {
            return m[3][0];
        }

        float ty() {
            return m[3][1];
        }

        float tz() {
            return m[3][2];
        }
    }

    private PartWorldPoseMath() {}
}
