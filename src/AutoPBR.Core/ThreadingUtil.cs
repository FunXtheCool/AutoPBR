using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// Shared helpers for thread naming and computing reasonable parallelism based on AutoPBROptions.
/// </summary>
internal static class ThreadingUtil
{
    private static int GetEffectiveThreads(int requested)
    {
        var logical = Math.Max(1, Environment.ProcessorCount);
        if (requested <= 0)
        {
            return Math.Max(1, logical - 2);
        }

        return Math.Clamp(requested, 1, logical);
    }

    public static int GetZipParallelism(AutoPBROptions options) => GetEffectiveThreads(options.MaxThreads);

    public static int GetConversionParallelism(AutoPBROptions options) => GetEffectiveThreads(options.MaxThreads);

    /// <summary>Set thread name for debugging (e.g. in Visual Studio Threads window). Name can only be set once per thread.</summary>
    public static void SetThreadName(string name)
    {
        try
        {
            Thread.CurrentThread.Name ??= name;
        }
        catch (InvalidOperationException)
        {
            /* already set */
        }
    }
}

