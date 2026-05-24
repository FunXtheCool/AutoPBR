using AutoPBR.Tools.AnimationCompiler;

namespace AutoPBR.AnimationCompiler.Tests;

public sealed class JavapRunnerCacheTests
{
    [Fact]
    public void Disassembly_cache_avoids_second_subprocess()
    {
        JavapRunner.ClearDisassemblyCacheForTests();
        AnimationCompilerStats.Reset();
        var javap = JavapLocator.FindJavap();
        if (string.IsNullOrWhiteSpace(javap))
        {
            return;
        }

        var tmpJar = Path.Combine(Path.GetTempPath(), $"autopbr-anim-empty-{Guid.NewGuid():n}.jar");
        File.WriteAllText(tmpJar, "");
        try
        {
            const string bogus = "com.autopbr.NotARealAnimationClass";
            Assert.False(JavapRunner.TryDisassemble(javap, tmpJar, bogus, out _, out _));
            var inv = AnimationCompilerStats.JavapSubprocessInvocations;
            Assert.False(JavapRunner.TryDisassemble(javap, tmpJar, bogus, out _, out _));
            Assert.Equal(inv, AnimationCompilerStats.JavapSubprocessInvocations);
            Assert.True(AnimationCompilerStats.DisasmCacheHits > 0);
        }
        finally
        {
            try
            {
                File.Delete(tmpJar);
            }
            catch
            {
                // ignore
            }
        }
    }
}
