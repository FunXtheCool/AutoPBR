using System.Diagnostics;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>Optional process-wide counters for geometry compiler runs (e.g. <c>--stats</c>).</summary>
internal static class GeometryCompilerStats
{
    private static long _javapSubprocessInvocations;
    private static long _disasmCacheHits;

    /// <summary>Wall-clock batch timing when <see cref="BeginBatch"/> / <see cref="EndBatchWallMs"/> are used.</summary>
    private static Stopwatch? _batchWall;

    public static long JavapSubprocessInvocations => Interlocked.Read(ref _javapSubprocessInvocations);

    public static long DisasmCacheHits => Interlocked.Read(ref _disasmCacheHits);

    public static void Reset()
    {
        Interlocked.Exchange(ref _javapSubprocessInvocations, 0);
        Interlocked.Exchange(ref _disasmCacheHits, 0);
        _batchWall = null;
    }

    internal static void NoteJavapSubprocess() => Interlocked.Increment(ref _javapSubprocessInvocations);

    internal static void NoteDisasmCacheHit() => Interlocked.Increment(ref _disasmCacheHits);

    internal static void BeginBatch() => _batchWall = Stopwatch.StartNew();

    internal static long EndBatchWallMs()
    {
        var w = _batchWall;
        _batchWall = null;
        return w?.ElapsedMilliseconds ?? 0;
    }
}
