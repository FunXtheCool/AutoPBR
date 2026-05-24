namespace AutoPBR.Core.Tests;

/// <summary>
/// Shared filesystem paths so tests do not assume Windows drive letters or developer-specific folders.
/// </summary>
internal static class TestEnvironmentPaths
{
    /// <summary>
    /// A path under the temp directory that tests never create — suitable for native profile roots when
    /// native assets must not be loaded from disk.
    /// </summary>
    public static string AbsentNativeRoot =>
        Path.Combine(Path.GetTempPath(), "AutoPBR.Tests.AbsentNative", Guid.NewGuid().ToString("N"));
}
