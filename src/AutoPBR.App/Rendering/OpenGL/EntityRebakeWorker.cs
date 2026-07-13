using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed class EntityRebakeWorker : IDisposable
{
    private readonly object _gate = new();
    private readonly ManualResetEventSlim _workAvailable = new(false);
    private readonly Thread _thread;
    private EntityRebakeRequest? _pending;
    private EntityRebakeResult? _completedA;
    private EntityRebakeResult? _completedB;
    private EntityRebakeResult? _latestCompleted;
    private volatile bool _disposed;

    public EntityRebakeWorker()
    {
        _thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "EntityRebakeWorker"
        };
        _thread.Start();
    }

    public long Enqueue(EntityRebakeRequest request)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return request.Sequence;
            }

            _pending = request;
        }

        _workAvailable.Set();
        return request.Sequence;
    }

    public bool TryTakeCompleted(long afterSequence, out EntityRebakeResult result)
    {
        lock (_gate)
        {
            if (_latestCompleted is null || _latestCompleted.Sequence <= afterSequence)
            {
                result = default!;
                return false;
            }

            result = _latestCompleted;
            return result.Success;
        }
    }

    private void WorkerLoop()
    {
        while (true)
        {
            _workAvailable.Wait();
            _workAvailable.Reset();

            EntityRebakeRequest? request;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                request = _pending;
                _pending = null;
            }

            if (request is null)
            {
                continue;
            }

            var success = EntityEmulatedPreviewRebaker.TryRebakeMesh(
                request.RebakeContext,
                request.Materials,
                request.AnimationTimeSeconds,
                out var verts,
                out var indices,
                out var batches,
                applyGeometryIrSetupAnimMotion: request.ApplyGeometryIrSetupAnimMotion);

            var completed = new EntityRebakeResult
            {
                Sequence = request.Sequence,
                Success = success,
                InterleavedVertices = verts,
                Indices = indices,
                DrawBatches = batches
            };

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (_completedA is null)
                {
                    _completedA = completed;
                    _latestCompleted = _completedA;
                }
                else if (_completedB is null || ReferenceEquals(_latestCompleted, _completedB))
                {
                    _completedB = completed;
                    _latestCompleted = _completedB;
                }
                else
                {
                    _completedA = completed;
                    _latestCompleted = _completedA;
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending = null;
        }

        _workAvailable.Set();
        if (_thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        _workAvailable.Dispose();
    }
}

internal sealed class EntityRebakeRequest
{
    public required long Sequence { get; init; }
    public required EntityEmulatedPreviewRebakeContext RebakeContext { get; init; }
    public required PreviewTextureMaps[] Materials { get; init; }
    public required float AnimationTimeSeconds { get; init; }
    public required bool ApplyGeometryIrSetupAnimMotion { get; init; }
}

internal sealed class EntityRebakeResult
{
    public long Sequence { get; init; }
    public bool Success { get; init; }
    public float[]? InterleavedVertices { get; init; }
    public uint[]? Indices { get; init; }
    public PreviewDrawBatch[]? DrawBatches { get; init; }
}
