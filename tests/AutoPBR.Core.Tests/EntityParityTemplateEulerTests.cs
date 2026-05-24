using System.Numerics;



namespace AutoPBR.Core.Tests;

public sealed class EntityParityTemplateEulerTests
{
    [Fact]
    public void ComposeEuler_XYZ_matches_Er()
    {
        const float x = 0.1f;
        const float y = 0.2f;
        const float z = 0.3f;
        var a = CleanRoomEntityModelRuntime.ErForTests(x, y, z);
        var b = CleanRoomEntityModelRuntime.ComposeEulerForTests("XYZ", x, y, z);
        AssertMatricesClose(a, b);
    }

    [Fact]
    public void ComposeEuler_ZYX_differs_from_XYZ_when_all_axes_nonzero()
    {
        const float x = 0.4f;
        const float y = 0.2f;
        const float z = 0.1f;
        var xyz = CleanRoomEntityModelRuntime.ComposeEulerForTests("XYZ", x, y, z);
        var zyx = CleanRoomEntityModelRuntime.ComposeEulerForTests("ZYX", x, y, z);
        Assert.False(MatricesClose(xyz, zyx));
    }

    private static void AssertMatricesClose(Matrix4x4 a, Matrix4x4 b, float tol = 1e-5f) =>
        Assert.True(MatricesClose(a, b, tol));

    private static bool MatricesClose(Matrix4x4 a, Matrix4x4 b, float tol = 1e-5f) =>
        MathF.Abs(a.M11 - b.M11) < tol && MathF.Abs(a.M22 - b.M22) < tol && MathF.Abs(a.M33 - b.M33) < tol &&
        MathF.Abs(a.M41 - b.M41) < tol && MathF.Abs(a.M42 - b.M42) < tol && MathF.Abs(a.M43 - b.M43) < tol;
}
