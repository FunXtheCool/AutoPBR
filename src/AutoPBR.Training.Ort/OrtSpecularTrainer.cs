using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace AutoPBR.Training.Ort;

/// <summary>
/// ORT Training loop mirroring Python <c>train_spec_ort.run_ort_training</c> (TrainStep → OptimizerStep → LazyResetGrad).
/// </summary>
public sealed class OrtSpecularTrainer : IDisposable
{
    private readonly TrainingSession _session;
    private readonly CheckpointState _state;
    private readonly RunOptions _runOptions = new();
    private readonly IReadOnlyList<string> _trainInputNames;
    private bool _disposed;

    public IReadOnlyList<string> TrainInputNames => _trainInputNames;

    public OrtSpecularTrainer(
        string trainModelPath,
        string evalModelPath,
        string optimizerModelPath,
        string checkpointLoadPath,
        SessionOptions? sessionOptions = null,
        bool preferCudaSessionOptions = true)
    {
        if (!File.Exists(trainModelPath))
        {
            throw new FileNotFoundException("Training ONNX not found.", trainModelPath);
        }

        if (!File.Exists(evalModelPath))
        {
            throw new FileNotFoundException("Eval ONNX not found.", evalModelPath);
        }

        if (!File.Exists(optimizerModelPath))
        {
            throw new FileNotFoundException("Optimizer ONNX not found.", optimizerModelPath);
        }

        if (!Directory.Exists(checkpointLoadPath) && !File.Exists(checkpointLoadPath))
        {
            throw new FileNotFoundException("ORT checkpoint path not found.", checkpointLoadPath);
        }

        WindowsGpuNativePath.PrependIfPresent();

        _state = CheckpointState.LoadCheckpoint(checkpointLoadPath);

        SessionOptions? ownedOptions = null;
        SessionOptions effective;
        if (sessionOptions is not null)
        {
            effective = sessionOptions;
        }
        else if (preferCudaSessionOptions)
        {
            try
            {
                ownedOptions = SessionOptions.MakeSessionOptionWithCudaProvider();
                effective = ownedOptions;
            }
            catch
            {
                ownedOptions = new SessionOptions();
                effective = ownedOptions;
            }
        }
        else
        {
            ownedOptions = new SessionOptions();
            effective = ownedOptions;
        }

        try
        {
            _session = new TrainingSession(effective, _state, trainModelPath, evalModelPath, optimizerModelPath);
        }
        catch
        {
            _state.Dispose();
            throw;
        }
        finally
        {
            if (sessionOptions is null)
            {
                ownedOptions?.Dispose();
            }
        }

        _trainInputNames = _session.InputNames(training: true);
        Console.WriteLine($"[ORT.NET] Training model inputs (ordered): {string.Join(", ", _trainInputNames)}");
    }

    public void SetLearningRate(float lr) => _session.SetLearningRate(lr);

    /// <summary>Train step, optimizer step, lazy grad reset; returns mean of first output (loss).</summary>
    public float TrainOneStep(IReadOnlyDictionary<string, OrtValue> feedsByName)
    {
        var inputs = new List<OrtValue>(_trainInputNames.Count);
        foreach (var name in _trainInputNames)
        {
            if (!feedsByName.TryGetValue(name, out var ov))
            {
                throw new InvalidOperationException(
                    $"Training model expects input '{name}'. Provided: {string.Join(", ", feedsByName.Keys)}");
            }

            inputs.Add(ov);
        }

        using var outputs = _session.TrainStep(inputs);
        var loss = MeanFirstOutput(outputs);
        _session.OptimizerStep(_runOptions);
        _session.LazyResetGrad();
        return loss;
    }

    /// <summary>Evaluation step; mean of first output.</summary>
    public float EvalOneStep(IReadOnlyDictionary<string, OrtValue> feedsByName)
    {
        var evalNames = _session.InputNames(training: false);
        var inputs = new List<OrtValue>(evalNames.Count);
        foreach (var name in evalNames)
        {
            if (!feedsByName.TryGetValue(name, out var ov))
            {
                throw new InvalidOperationException(
                    $"Eval model expects input '{name}'. Provided: {string.Join(", ", feedsByName.Keys)}");
            }

            inputs.Add(ov);
        }

        using var outputs = _session.EvalStep(inputs);
        return MeanFirstOutput(outputs);
    }

    public void SaveCheckpoint(string path, bool includeOptimizerState)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        try
        {
            CheckpointState.SaveCheckpoint(_state, path, includeOptimizerState);
        }
        catch
        {
            CheckpointState.SaveCheckpoint(_state, path);
        }
    }

    private static float MeanFirstOutput(IDisposableReadOnlyCollection<OrtValue> outputs)
    {
        if (outputs is not IReadOnlyList<OrtValue> list || list.Count == 0)
        {
            return 0;
        }

        var first = list[0];
        if (first.GetTensorTypeAndShape().ElementDataType != TensorElementType.Float)
        {
            return 0;
        }

        if (first.GetTensorTypeAndShape().ElementCount <= 0)
        {
            return 0;
        }

        var span = first.GetTensorDataAsSpan<float>();
        double s = 0;
        for (var i = 0; i < span.Length; i++)
        {
            s += span[i];
        }

        return (float)(s / span.Length);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _runOptions.Dispose();
        _session.Dispose();
        _state.Dispose();
    }
}
