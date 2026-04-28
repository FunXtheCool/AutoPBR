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
public sealed class DeepBumpNormalsGenerator : IDisposable
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
                          new InferenceSession(modelPath);
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

    private static void TilesSplit(float[] img, int channels, int imgW, int imgH, int stride, out List<float[]> tiles,
        out (int padLeft, int padRight, int padTop, int padBottom) paddings)
    {
        int padH = 0, padW = 0;
        var remainderH = (imgH - TileSize) % stride;
        var remainderW = (imgW - TileSize) % stride;
        if (remainderH != 0)
        {
            padH = stride - remainderH;
        }


        if (remainderW != 0)
        {
            padW = stride - remainderW;
        }


        if (TileSize > imgH)
        {
            padH = TileSize - imgH;
        }


        if (TileSize > imgW)
        {
            padW = TileSize - imgW;
        }


        var padLeft = padW / 2 + stride;
        var padRight = padLeft + (padW % 2 == 0 ? 0 : 1);
        var padTop = padH / 2 + stride;
        var padBottom = padTop + (padH % 2 == 0 ? 0 : 1);
        paddings = (padLeft, padRight, padTop, padBottom);
        var fullW = imgW + padLeft + padRight;
        var fullH = imgH + padTop + padBottom;
        var padded = new float[channels * fullW * fullH];
        for (var c = 0; c < channels; c++)
        {
            var srcBase = c * imgW * imgH;
            var padBase = c * fullW * fullH;
            for (var y = 0; y < fullH; y++)
            {
                var sy = (y - padTop) % imgH;
                if (sy < 0)
                {
                    sy += imgH;
                }

                for (var x = 0; x < fullW; x++)
                {
                    var sx = (x - padLeft) % imgW;
                    if (sx < 0)
                    {
                        sx += imgW;
                    }

                    padded[padBase + y * fullW + x] = img[srcBase + sy * imgW + sx];
                }
            }
        }

        tiles = new List<float[]>();
        var hRange = (fullH - TileSize) / stride + 1;
        var wRange = (fullW - TileSize) / stride + 1;
        for (var hy = 0; hy < hRange; hy++)
        {
            for (var wx = 0; wx < wRange; wx++)
            {
                var tile = new float[channels * TileSize * TileSize];
                var y0 = hy * stride;
                var x0 = wx * stride;
                for (var c = 0; c < channels; c++)
                {
                    var padBase = c * fullW * fullH;
                    var tileBase = c * TileSize * TileSize;
                    for (var y = 0; y < TileSize; y++)
                    {
                        for (var x = 0; x < TileSize; x++)
                        {
                            tile[tileBase + y * TileSize + x] = padded[padBase + (y0 + y) * fullW + (x0 + x)];
                        }
                    }
                }

                tiles.Add(tile);
            }
        }

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
                       new InferenceSession(_modelPath);
            }
            catch
            {
                // fall through to CPU
            }
        }

        return new InferenceSession(_modelPath);
    }

    private static float[] ConvertNhwcToNchw(float[] nhwc, int n, int h, int w, int c)
    {
        var nchw = new float[n * c * h * w];
        for (var ni = 0; ni < n; ni++)
        {
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    for (var ci = 0; ci < c; ci++)
                    {
                        nchw[(ni * c + ci) * h * w + y * w + x] = nhwc[((ni * h + y) * w + x) * c + ci];
                    }
                }
            }
        }


        return nchw;
    }

    /// <summary>Pyramidal-like mask from utils_inference.generate_mask + corner_mask + scaling_mask for correct tile blending.</summary>
    private static float[] GenerateMask(int stride)
    {
        var ramp = TileSize - stride; // ramp_h == ramp_w for square tiles
        var mask = new float[TileSize * TileSize];
        var rampMinus1 = ramp > 1 ? ramp - 1 : 1;
        for (var y = 0; y < TileSize; y++)
        {
            for (var x = 0; x < TileSize; x++)
            {
                float v = 1f;
                // Ramps in width direction: mask[ramp:-ramp, :ramp] = linspace(0,1), mask[ramp:-ramp, -ramp:] = linspace(1,0)
                if (y >= ramp && y < TileSize - ramp)
                {
                    if (x < ramp)
                    {
                        v = (float)x / rampMinus1;
                    }
                    else if (x >= TileSize - ramp)
                    {
                        v = (float)(TileSize - 1 - x) / rampMinus1;
                    }
                }
                // Ramps in height direction
                else if (x >= ramp && x < TileSize - ramp)
                {
                    if (y < ramp)
                    {
                        v = (float)y / rampMinus1;
                    }
                    else if (y >= TileSize - ramp)
                    {
                        v = (float)(TileSize - 1 - y) / rampMinus1;
                    }
                }
                else
                {
                    // Corners: utils_inference uses rot90(corner_mask,2) for top-left, then flip for top-right, flip for bottom-right, flip for bottom-left
                    int ch, cw;
                    if (y < ramp && x < ramp)
                    {
                        ch = ramp - 1 - y;
                        cw = ramp - 1 - x;
                    } // top-left
                    else if (y < ramp && x >= TileSize - ramp)
                    {
                        ch = ramp - 1 - y;
                        cw = x - (TileSize - ramp);
                    } // top-right
                    else if (y >= TileSize - ramp && x >= TileSize - ramp)
                    {
                        ch = y - (TileSize - ramp);
                        cw = x - (TileSize - ramp);
                    } // bottom-right
                    else
                    {
                        ch = TileSize - 1 - y;
                        cw = ramp - 1 - x;
                    } // bottom-left

                    v = CornerMaskValue(ramp, ch, cw);
                }

                mask[y * TileSize + x] = v;
            }
        }


        return mask;
    }

    /// <summary>corner_mask(side_length) - 0.25*scaling_mask(side_length) per utils_inference.</summary>
    private static float CornerMaskValue(int sideLength, int h, int w)
    {
        if (sideLength <= 0)
        {
            return 1f;
        }


        var s = sideLength;
        var s1 = (float)(s - 1);
        float corner = (h >= w) ? (1f - h / s1) : (1f - w / s1);
        if (corner < 0)
        {
            corner = 0;
        }


        float scaling = ScalingMaskValue(s, h, w); // Python: scaling_mask = 2*scaling, then corner - 0.25*scaling_mask
        return Math.Max(0f, corner - 0.25f * (2f * scaling));
    }

    /// <summary>Inner scaling value; Python returns 2*this as scaling_mask. Corner uses 0.25*scaling_mask = 0.5*this.</summary>
    private static float ScalingMaskValue(int sideLength, int h, int w)
    {
        if (sideLength <= 0)
        {
            return 0f;
        }


        var s = sideLength;
        var s1 = (float)(s - 1);
        var sh = h / s1;
        var sw = w / s1;
        if (h >= w && h <= s - 1 - w)
        {
            return sw;
        }


        if (h <= w && h <= s - 1 - w)
        {
            return sh;
        }


        if (h >= w && h >= s - 1 - w)
        {
            return 1f - sh;
        }


        if (h <= w && h >= s - 1 - w)
        {
            return 1f - sw;
        }


        return 0f;
    }

    private static float[] TilesMerge(List<float[]> tiles, int stride, int channels, int fullH, int fullW,
        (int padLeft, int padRight, int padTop, int padBottom) paddings)
    {
        var merged = new float[channels * fullH * fullW];
        var mask = GenerateMask(stride);
        var hRange = (fullH - TileSize) / stride + 1;
        var wRange = (fullW - TileSize) / stride + 1;
        var idx = 0;
        for (var hy = 0; hy < hRange; hy++)
        {
            for (var wx = 0; wx < wRange; wx++)
            {
                var tile = tiles[idx++];
                var y0 = hy * stride;
                var x0 = wx * stride;
                for (var c = 0; c < channels; c++)
                {
                    for (var y = 0; y < TileSize; y++)
                    {
                        for (var x = 0; x < TileSize; x++)
                        {
                            var my = y0 + y;
                            var mx = x0 + x;
                            if (my < fullH && mx < fullW)
                            {
                                merged[(c * fullH + my) * fullW + mx] +=
                        tile[c * TileSize * TileSize + y * TileSize + x] * mask[y * TileSize + x];
                            }
                        }
                    }
                }
            }
        }


        var padLeft = paddings.padLeft;
        var padTop = paddings.padTop;
        var padRight = paddings.padRight;
        var padBottom = paddings.padBottom;
        var outH = fullH - padTop - padBottom;
        var outW = fullW - padLeft - padRight;
        var result = new float[channels * outH * outW];
        for (var c = 0; c < channels; c++)
        {
            for (var y = 0; y < outH; y++)
            {
                for (var x = 0; x < outW; x++)
                {
                    result[(c * outH + y) * outW + x] = merged[(c * fullH + (y + padTop)) * fullW + (x + padLeft)];
                }
            }
        }


        return result;
    }

    private static void NormalizeInPlace(float[] data, int channels, int height, int width)
    {
        var n = height * width;
        for (var i = 0; i < n; i++)
        {
            float sumSq = 0;
            for (var c = 0; c < channels; c++)
            {
                var v = data[(c * height * width) + i] - 0.5f;
                data[(c * height * width) + i] = v;
                sumSq += v * v;
            }

            var norm = MathF.Sqrt(sumSq);
            if (norm < 1e-8f)
            {
                norm = 1f;
            }

            for (var c = 0; c < channels; c++)
            {
                data[(c * height * width) + i] = (data[(c * height * width) + i] / norm) * 0.5f + 0.5f;
            }

        }
    }

    private static Image<Rgba32> ToNormalImage(float[] data, int height, int width, bool forceBlue255)
    {
        var img = new Image<Rgba32>(width, height);
        img.ProcessPixelRows(acc =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = acc.GetRowSpan(y);
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    var r = (byte)Math.Clamp((int)(data[i] * 255f), 0, 255);
                    var g = (byte)Math.Clamp((int)(data[height * width + i] * 255f), 0, 255);
                    var b = forceBlue255
                        ? (byte)255
                        : (byte)Math.Clamp((int)(data[2 * height * width + i] * 255f), 0, 255);
                    row[x] = new Rgba32(r, g, b, 255);
                }
            }
        });
        return img;
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
