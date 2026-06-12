namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Background parallel prewarm of flattened GLSL sources (CPU-only, safe off the GL thread).</summary>
internal static class PreviewShaderPrewarm
{
    private static readonly object Gate = new();
    private static Task? _task;
    private static int _completed;
    private static int _total;

    public static event Action? ProgressChanged;

    public static bool IsComplete { get; private set; }

    public static int CompletedCount => Volatile.Read(ref _completed);

    public static int TotalCount => Volatile.Read(ref _total);

    public static double Fraction
    {
        get
        {
            var total = TotalCount;
            return total <= 0 ? (IsComplete ? 1.0 : 0.0) : (double)CompletedCount / total;
        }
    }

    public static void EnsureStarted()
    {
        lock (Gate)
        {
            if (_task is not null)
            {
                return;
            }

            IsComplete = false;
            _completed = 0;
            _total = GlslPreparedSourceCache.PrewarmWorkItemCount;
            _task = Task.Run(RunPrewarm);
        }
    }

    public static void ClearAndRestart()
    {
        Task? prior;
        lock (Gate)
        {
            GlslPreparedSourceCache.Clear();
            prior = _task;
            _task = null;
            IsComplete = false;
            _completed = 0;
            _total = GlslPreparedSourceCache.PrewarmWorkItemCount;
            _task = Task.Run(RunPrewarm);
        }

        _ = prior;
    }

    private static void RunPrewarm()
    {
        try
        {
            GlslPreparedSourceCache.PrewarmAllPreviewShadersParallel(() =>
            {
                Interlocked.Increment(ref _completed);
                ProgressChanged?.Invoke();
            });
            IsComplete = true;
        }
        finally
        {
            ProgressChanged?.Invoke();
        }
    }
}
