using System.Numerics;
using System.Runtime.InteropServices;

namespace AutoPBR.Core.Tests;

/// <summary>
/// <see cref="Vector3.Transform(System.Numerics.Vector3, System.Numerics.Matrix4x4)"/> matches GLSL <c>mat4 * vec4</c> when the UBO stores the numerics matrix row-major
/// 16 floats (same byte order as <see cref="Matrix4x4"/>) and the shader treats them as a column-major <c>mat4</c>.
/// </summary>
public sealed class MatrixTransformGlColumnParityTests
{
    private static Vector4 GlslMat4TimesVec4(ReadOnlySpan<float> colMajor16, in Vector4 v) =>
        new(
            colMajor16[0] * v.X + colMajor16[4] * v.Y + colMajor16[8] * v.Z + colMajor16[12] * v.W,
            colMajor16[1] * v.X + colMajor16[5] * v.Y + colMajor16[9] * v.Z + colMajor16[13] * v.W,
            colMajor16[2] * v.X + colMajor16[6] * v.Y + colMajor16[10] * v.Z + colMajor16[14] * v.W,
            colMajor16[3] * v.X + colMajor16[7] * v.Y + colMajor16[11] * v.Z + colMajor16[15] * v.W);

    private static void CopyRowMajorStructBytes(in Matrix4x4 m, Span<float> dst16)
    {
        var mm = m;
        MemoryMarshal.CreateReadOnlySpan(ref mm.M11, 16).CopyTo(dst16);
    }

    [Fact]
    public void Row_major_Matrix4x4_bytes_used_as_gl_column_major_mat4_match_Vector3_Transform()
    {
        Span<float> buf = stackalloc float[16];
        var rnd = new Random(11);
        for (var iter = 0; iter < 100; iter++)
        {
            var t = new Vector3((float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 2f - 1f);
            var yaw = (float)(rnd.NextDouble() * 6.28);
            var pitch = (float)(rnd.NextDouble() * 6.28);
            var roll = (float)(rnd.NextDouble() * 6.28);
            var m = Matrix4x4.CreateFromYawPitchRoll(yaw, pitch, roll) * Matrix4x4.CreateTranslation(t);
            var p = new Vector3((float)rnd.NextDouble() * 3f - 1f, (float)rnd.NextDouble() * 3f - 1f, (float)rnd.NextDouble() * 3f - 1f);
            var cpu = Vector3.Transform(p, m);
            CopyRowMajorStructBytes(m, buf);
            var gl = GlslMat4TimesVec4(buf, new Vector4(p, 1f));
            Assert.Equal(cpu.X, gl.X, 5);
            Assert.Equal(cpu.Y, gl.Y, 5);
            Assert.Equal(cpu.Z, gl.Z, 5);
        }
    }

    [Fact]
    public void Row_major_bytes_of_Transpose_do_not_match_Vector3_Transform()
    {
        Span<float> buf = stackalloc float[16];
        var m = Matrix4x4.CreateFromYawPitchRoll(0.7f, -0.4f, 0.2f) * Matrix4x4.CreateTranslation(0.3f, -0.6f, 0.1f);
        var p = new Vector3(0.2f, -0.5f, 0.4f);
        var cpu = Vector3.Transform(p, m);
        CopyRowMajorStructBytes(Matrix4x4.Transpose(m), buf);
        var gl = GlslMat4TimesVec4(buf, new Vector4(p, 1f));
        Assert.True(Math.Abs(cpu.X - gl.X) > 0.05f || Math.Abs(cpu.Y - gl.Y) > 0.05f || Math.Abs(cpu.Z - gl.Z) > 0.05f,
            "Uploading Transpose(M) row-bytes as a GL mat4 should not reproduce Vector3.Transform(M).");
    }
}
