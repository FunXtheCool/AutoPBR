using Microsoft.ML.OnnxRuntime;

namespace AutoPBR.Training.Ort;

/// <summary>
/// Owns per-batch <see cref="OrtValue"/> tensors for specular ORT training (Python feed names).
/// </summary>
public sealed class SpecularOrtBatchTensors : IDisposable
{
    private readonly List<OrtValue> _disposables = new();
    private bool _disposed;

    /// <summary>Feeds keyed by ONNX input name (e.g. input, target_rgba, valid, transparent_zero_weight).</summary>
    public Dictionary<string, OrtValue> Feeds { get; } = new(StringComparer.Ordinal);

    /// <param name="batchN">N in NCHW / N×4×H×W / N×H×W.</param>
    public static SpecularOrtBatchTensors Create(
        int batchN,
        int inChannels,
        int height,
        int width,
        ReadOnlySpan<float> inputNchw,
        ReadOnlySpan<float> targetNchw4,
        ReadOnlySpan<float> validNhw,
        float transparentZeroWeight)
    {
        var expectedIn = batchN * inChannels * height * width;
        var expectedT = batchN * 4 * height * width;
        var expectedV = batchN * height * width;
        if (inputNchw.Length < expectedIn)
        {
            throw new ArgumentException($"input slice length {inputNchw.Length} < {expectedIn}.", nameof(inputNchw));
        }

        if (targetNchw4.Length < expectedT)
        {
            throw new ArgumentException($"target slice length {targetNchw4.Length} < {expectedT}.", nameof(targetNchw4));
        }

        if (validNhw.Length < expectedV)
        {
            throw new ArgumentException($"valid slice length {validNhw.Length} < {expectedV}.", nameof(validNhw));
        }

        var self = new SpecularOrtBatchTensors();
        var inArr = inputNchw[..expectedIn].ToArray();
        var tArr = targetNchw4[..expectedT].ToArray();
        var vArr = validNhw[..expectedV].ToArray();
        var twArr = new[] { Math.Max(transparentZeroWeight, 0f) };

        self.AddFeed("input", inArr, [batchN, inChannels, height, width]);
        self.AddFeed("target_rgba", tArr, [batchN, 4, height, width]);
        self.AddFeed("valid", vArr, [batchN, height, width]);
        self.AddFeed("transparent_zero_weight", twArr, [1]);
        return self;
    }

    private void AddFeed(string name, float[] data, long[] shape)
    {
        var ov = OrtValue.CreateTensorValueFromMemory(data, shape);
        _disposables.Add(ov);
        Feeds[name] = ov;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var o in _disposables)
        {
            o.Dispose();
        }

        _disposables.Clear();
        Feeds.Clear();
    }
}
