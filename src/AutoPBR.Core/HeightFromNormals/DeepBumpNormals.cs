// Uses DeepBump deepbump256.onnx (https://github.com/HugoTini/DeepBump) via ONNX Runtime
// for color → normal map generation. GPL-3.0; see LICENSE-DeepBump.txt.
// GPU: OnnxRuntime.Managed 1.24.x + redistributed CUDA 13 ORT/CUDA/cuDNN/TensorRT DLLs: Data\native → runtimes\win-x64\native.

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.HeightFromNormals;

/// <summary>
/// Generates a normal map from a diffuse (color) image using the DeepBump ONNX model (deepbump256.onnx).
/// Mirrors the tiling and merge logic from the official Python implementation.
/// </summary>
public sealed partial class DeepBumpNormalsGenerator : IDisposable
{
    private const int TileSize = 256;
    private readonly string _inputName;
    private readonly bool _inputIsNhwc;
    private readonly int _inputChannels;
    private readonly bool _outputIsNhwc;
    private readonly string _modelPath;
    private readonly bool _preferGpu;
    private readonly bool _preferOnnxTensorRt;
    private readonly object _poolLock = new();
    private readonly Queue<InferenceSession> _sessionPool = new();
    private readonly int _maxConcurrentRuns;
    private readonly SemaphoreSlim _sessionSlots;

    public enum Overlap
    {
        Small = TileSize / 6,
        Medium = TileSize / 4,
        Large = TileSize / 2
    }

    /// <summary>True if the session is using the CUDA execution provider (GPU/Tensor Cores).</summary>
    public bool IsUsingGpu { get; }

    public static DeepBumpNormalsGenerator? TryCreate(
        string modelPath,
        int maxConcurrentRuns = 1,
        bool preferOnnxTensorRtExecutionProvider = false)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {

            return null;
        }


        try
        {
            maxConcurrentRuns = Math.Max(1, maxConcurrentRuns);

            var session = OnnxRuntimeWindowsNative.TryCreateGpuSession(
                              modelPath,
                              preferOnnxTensorRtExecutionProvider,
                              out var provider,
                              out _) ??
                          CreateCpuSession(modelPath);
            var useGpu = provider is "CUDA" or "TensorRT";
            if (useGpu)
            {
                // ORT CUDA EP can become unstable when multiple sessions run concurrently.
                // Keep GPU inference single-flight unless/ until a dedicated tuning option is introduced.
                maxConcurrentRuns = 1;
            }

            var inputName = session.InputMetadata.Keys.FirstOrDefault() ?? "input";
            var inputIsNhwc = false;
            var inputChannels = 1;
            if (session.InputMetadata.Values.FirstOrDefault() is { } inMeta && inMeta.Dimensions.Length == 4)
            {
                var dims = inMeta.Dimensions;
                // Accept NCHW and NHWC, with either 1 or 3 channels.
                // Common: [1,1,256,256] or [1,3,256,256] (NCHW) OR [1,256,256,1]/[1,256,256,3] (NHWC)
                if (dims[1] is 1 or 3)
                {
                    inputIsNhwc = false;
                    inputChannels = dims[1];
                }
                else if (dims[^1] is 1 or 3)
                {
                    inputIsNhwc = true;
                    inputChannels = dims[^1];
                }
            }

            var outputIsNhwc = false;
            if (session.OutputMetadata.Values.FirstOrDefault() is { } outMeta && outMeta.Dimensions.Length == 4)
            {
                var dims = outMeta.Dimensions;
                outputIsNhwc = dims[^1] == 3;
            }

            return new DeepBumpNormalsGenerator(
                modelPath,
                inputName,
                inputIsNhwc,
                inputChannels,
                outputIsNhwc,
                useGpu,
                preferOnnxTensorRtExecutionProvider,
                maxConcurrentRuns,
                firstSession: session);
        }
        catch
        {
            return null;
        }
    }

    private DeepBumpNormalsGenerator(
        string modelPath,
        string inputName,
        bool inputIsNhwc,
        int inputChannels,
        bool outputIsNhwc,
        bool isUsingGpu,
        bool preferOnnxTensorRtExecutionProvider,
        int maxConcurrentRuns,
        InferenceSession firstSession)
    {
        _modelPath = modelPath;
        _inputName = inputName;
        _inputIsNhwc = inputIsNhwc;
        _inputChannels = inputChannels is 1 or 3 ? inputChannels : 1;
        _outputIsNhwc = outputIsNhwc;
        IsUsingGpu = isUsingGpu;
        _preferGpu = isUsingGpu;
        _preferOnnxTensorRt = preferOnnxTensorRtExecutionProvider;
        _maxConcurrentRuns = maxConcurrentRuns;
        _sessionSlots = new SemaphoreSlim(_maxConcurrentRuns, _maxConcurrentRuns);
        _sessionPool.Enqueue(firstSession);
        for (var i = 1; i < _maxConcurrentRuns; i++)
        {
            _sessionPool.Enqueue(CreateSession());
        }
    }


    /// <summary>
    /// Generates a normal map from the diffuse image. Returns Rgba32 image with R=nx, G=ny, B=255 (LabPBR style).
    /// </summary>
    public Image<Rgba32> Generate(
        Image<Rgba32> diffuse,
        Overlap overlap = Overlap.Medium,
        DeepBumpInputMode inputMode = DeepBumpInputMode.Auto,
        bool forceBlue255 = false)
    {
        var width = diffuse.Width;
        var height = diffuse.Height;
        var input = ToModelInputFloat(diffuse, inputMode);
        var stride = TileSize - (int)overlap;
        if (stride % 2 != 0)
        {
            stride--;
        }


        TilesSplit(input, _inputChannels, width, height, stride, out var tiles, out var paddings);
        var predTiles = new List<float[]>();
        var session = RentSession();
        try
        {
            foreach (var tile in tiles)
            {
                var pred = RunTile(session, tile);
                predTiles.Add(pred);
            }
        }
        finally
        {
            ReturnSession(session);
        }

        var merged = TilesMerge(predTiles, stride, 3, height + paddings.padTop + paddings.padBottom,
            width + paddings.padLeft + paddings.padRight, paddings);
        NormalizeInPlace(merged, 3, height, width);
        return ToNormalImage(merged, height, width, forceBlue255);
    }

    private float[] ToModelInputFloat(Image<Rgba32> img, DeepBumpInputMode mode)
    {
        var w = img.Width;
        var h = img.Height;
        var channels = _inputChannels;
        var data = new float[channels * w * h];
        img.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < h; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < w; x++)
                {
                    var p = row[x];
                    var idx = y * w + x;
                    if (channels == 1)
                    {
                        data[idx] = (p.R + p.G + p.B) / (3f * 255f);
                    }
                    else
                    {
                        var useRgb = mode == DeepBumpInputMode.Rgb
                                     || (mode == DeepBumpInputMode.Auto && _inputChannels == 3);
                        if (!useRgb)
                        {
                            var gray = (p.R + p.G + p.B) / (3f * 255f);
                            data[idx] = gray;
                            data[w * h + idx] = gray;
                            data[2 * w * h + idx] = gray;
                        }
                        else
                        {
                            data[idx] = p.R / 255f;
                            data[w * h + idx] = p.G / 255f;
                            data[2 * w * h + idx] = p.B / 255f;
                        }
                    }
                }
            }
        });
        return data;
    }


    private float[] RunTile(InferenceSession session, float[] tile)
    {
        var inputTensor = CreateInputTensor(tile);
        List<NamedOnnxValue> inputs = [NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)];
        using var outputs = session.Run(inputs);
        var outTensor = outputs[0];
        var outputFloats = outTensor.AsEnumerable<float>().ToArray();
        if (_outputIsNhwc)
        {
            outputFloats = ConvertNhwcToNchw(outputFloats, 1, TileSize, TileSize, 3);
        }


        return outputFloats;
    }

    private DenseTensor<float> CreateInputTensor(float[] tilePlanarNchw)
    {
        if (!_inputIsNhwc)
        {
            return new DenseTensor<float>(tilePlanarNchw, [1, _inputChannels, TileSize, TileSize]);
        }

        var nhwc = new float[TileSize * TileSize * _inputChannels];
        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                var i = y * TileSize + x;
                for (var c = 0; c < _inputChannels; c++)
                {
                    nhwc[i * _inputChannels + c] = tilePlanarNchw[(c * TileSize * TileSize) + i];
                }
            }
        }
        return new DenseTensor<float>(nhwc, [1, TileSize, TileSize, _inputChannels]);
    }

    private InferenceSession RentSession()
    {
        _sessionSlots.Wait();
        lock (_poolLock)
        {
            if (_sessionPool.Count > 0)
            {
                return _sessionPool.Dequeue();
            }
        }

        // Should be rare (e.g., if session pre-creation failed); keep behavior resilient.
        return CreateSession();
    }

    private void ReturnSession(InferenceSession session)
    {
        lock (_poolLock)
        {
            if (_sessionPool.Count < _maxConcurrentRuns)
            {
                _sessionPool.Enqueue(session);
            }
            else
            {
                session.Dispose();
            }
        }

        _sessionSlots.Release();
    }

    private InferenceSession CreateSession()
    {
        if (_preferGpu)
        {
            try
            {
                return OnnxRuntimeWindowsNative.TryCreateGpuSession(_modelPath, _preferOnnxTensorRt, out _, out _) ??
                       CreateCpuSession(_modelPath);
            }
            catch
            {
                // fall through to CPU
            }
        }

        return CreateCpuSession(_modelPath);
    }

    private static InferenceSession CreateCpuSession(string modelPath)
    {
        using var options = OnnxRuntimeSessionOptions.CreateCpuSingleThreaded();
        return new InferenceSession(modelPath, options);
    }

    public void Dispose()
    {
        _sessionSlots.Dispose();
        lock (_poolLock)
        {
            while (_sessionPool.Count > 0)
            {
                _sessionPool.Dequeue().Dispose();
            }
        }
    }
}
