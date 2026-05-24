namespace AutoPBR.Tools.AnimationCompiler;

internal static class AnimationCompilerStats
{
    private static long _javapSubprocessInvocations;
    private static long _disasmCacheHits;
    private static long? _batchWallMs;

    public static long JavapSubprocessInvocations => Interlocked.Read(ref _javapSubprocessInvocations);

    public static long DisasmCacheHits => Interlocked.Read(ref _disasmCacheHits);

    public static void Reset()
    {
        Interlocked.Exchange(ref _javapSubprocessInvocations, 0);
        Interlocked.Exchange(ref _disasmCacheHits, 0);
        _batchWallMs = null;
    }

    internal static void NoteJavapSubprocess() => Interlocked.Increment(ref _javapSubprocessInvocations);

    internal static void NoteDisasmCacheHit() => Interlocked.Increment(ref _disasmCacheHits);

    internal static void BeginBatch() => _batchWallMs = Environment.TickCount64;

    internal static long EndBatchWallMs()
    {
        if (_batchWallMs is not { } start)
        {
            return 0;
        }

        _batchWallMs = null;
        return Math.Max(0, Environment.TickCount64 - start);
    }
}
