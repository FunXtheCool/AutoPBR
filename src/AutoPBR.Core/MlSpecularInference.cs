using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using AutoPBR.Core.Models;

namespace AutoPBR.Core;

/// <summary>
/// ONNX inference for direct per-pixel LabPBR specular prediction (RGBA).
/// </summary>
/// <remarks>
/// <para>
/// Exported models should emit NCHW (or NHWC) <b>spec</b> with channel order matching LabPBR <c>_s.png</c>:
/// <b>R</b> = smoothness, <b>G</b> = F0/metal (≥230 metal in <see cref="SpecularGenerator"/> heuristics),
/// <b>B</b> = porosity/subsurface, <b>A</b> = emission (255 = none). Values are normally <b>linear 0–1</b>
/// per channel (training uses sigmoid on logits); <see cref="Runner.Postprocess"/> scales by 255 unless
/// magnitudes look like pre-quantized 0–255 bytes. See <c>docs/ml-specular-labpbr-contract.md</c>.
/// </para>
/// <para>
/// Models may still export five channels for backward compatibility; only the first four are used.
/// </para>
/// </remarks>
internal static class MlSpecularInference
{
    private static readonly object CacheLock = new();

    /// <summary>Cache key: model path + TensorRT preference + desired CPU runner pool size.</summary>
    private static readonly Dictionary<(string Path, bool PreferTrt, int DesiredPoolSize), CachedRunner> RunnerCache = new();

    private sealed class CachedRunner
    {
        public required Runner[] Runners { get; init; }
        public string? LoadDiagnostic { get; init; }
        public int NextRunnerIndex;
    }

    public static bool TryPredictSpecular(
        Image<Rgba32> image,
        float[] edgeMagnitude,
        AutoPbrOptions options,
        out byte[] r,
        out byte[] g,
        out byte[] b,
        out byte[] a,
        out string? diagnostic)
    {
        diagnostic = null;
        var n = image.Width * image.Height;
        r = new byte[n];
        g = new byte[n];
        b = new byte[n];
        a = new byte[n];

        var textureSize = Math.Min(image.Width, image.Height);
        if (!MlSpecularModelResolution.TryResolveModelPath(options, textureSize, out var path, out var selectedRes,
                out var resolveDiag))
        {
            diagnostic = resolveDiag;
            return false;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            diagnostic = $"Specular ONNX file not found: {path}";
            return false;
        }

        Runner? runner = null;
        string? loadDiag;
        lock (CacheLock)
        {
            var preferTrt = options.PreferOnnxTensorRtExecutionProvider;
            var desiredPoolSize = ResolveCpuRunnerPoolSize(options);
            var key = (path, preferTrt, desiredPoolSize);
            if (!RunnerCache.TryGetValue(key, out var cached))
            {
                var created = CreateRunnerPool(path, preferTrt, desiredPoolSize, out loadDiag);
                cached = new CachedRunner { Runners = created, LoadDiagnostic = loadDiag };
                RunnerCache[key] = cached;
            }
            else
            {
                loadDiag = cached.LoadDiagnostic;
            }

            if (cached.Runners.Length > 0)
            {
                var next = Interlocked.Increment(ref cached.NextRunnerIndex);
                var idx = (next & int.MaxValue) % cached.Runners.Length;
                runner = cached.Runners[idx];
            }
        }

        if (runner is null)
        {
            diagnostic = string.IsNullOrWhiteSpace(loadDiag)
                ? "Failed to load specular ONNX (invalid model or unsupported input layout)."
                : loadDiag;
            return false;
        }

        try
        {
            var includeEdge = runner.InputChannelCount == 4 && options.MlSpecularUseEdgeChannel;
            if (!runner.Predict(image, edgeMagnitude, options, r, g, b, a, out var postErr))
            {
                diagnostic = string.IsNullOrWhiteSpace(postErr)
                    ? "Specular model ran but output could not be decoded (wrong output shape or layout)."
                    : postErr;
                return false;
            }

            if (options.SpecularDebugVerboseSpecularMl)
            {
                var resHint = selectedRes.HasValue ? $"; modelRes={selectedRes}" : "";
                diagnostic =
                    $"Specular ML OK {image.Width}x{image.Height}{resHint}; provider={runner.ExecutionProvider}; modelInChannels={runner.InputChannelCount}; " +
                    $"feedEdge={includeEdge}; firstPx RGBA=({r[0]},{g[0]},{b[0]},{a[0]})";
            }

            return true;
        }
        catch (Exception ex)
        {
            diagnostic = $"Specular ONNX inference error: {ex.Message}";
            return false;
        }
    }

    private static Runner[] CreateRunnerPool(
        string modelPath,
        bool preferTensorRtExecutionProvider,
        int desiredCpuPoolSize,
        out string? diagnostic)
    {
        diagnostic = null;
        var first = Runner.TryCreate(modelPath, preferTensorRtExecutionProvider, out diagnostic);
        if (first is null)
        {
            return [];
        }

        var runners = new List<Runner> { first };
        var targetCount = first.IsUsingGpu ? 1 : Math.Max(1, desiredCpuPoolSize);
        for (var i = 1; i < targetCount; i++)
        {
            var extra = Runner.TryCreate(modelPath, preferTensorRtExecutionProvider, out _);
            if (extra is null)
            {
                break;
            }

            runners.Add(extra);
        }

        return runners.ToArray();
    }

    private static int ResolveCpuRunnerPoolSize(AutoPbrOptions options)
    {
        // Multiple CPU sessions remove the single-runner lock bottleneck.
        // Cap pool size to avoid excessive model memory duplication.
        var conversionParallelism = ThreadingUtil.GetConversionParallelism(options);
        return Math.Clamp(conversionParallelism, 1, 4);
    }

    private sealed class Runner : IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly bool _inputIsNhwc;
        private readonly int _inputChannels;
        private readonly object _runLock = new();

        /// <summary>Declared input channel count (3 = RGB, 4 = RGB+edge).</summary>
        public int InputChannelCount => _inputChannels;
        public string ExecutionProvider { get; }
        public bool IsUsingGpu => ExecutionProvider is "CUDA" or "TensorRT";

        private Runner(
            InferenceSession session,
            string inputName,
            bool inputIsNhwc,
            int inputChannels,
            string executionProvider)
        {
            _session = session;
            _inputName = inputName;
            _inputIsNhwc = inputIsNhwc;
            _inputChannels = inputChannels;
            ExecutionProvider = string.IsNullOrWhiteSpace(executionProvider) ? "CPU" : executionProvider;
        }

        public static Runner? TryCreate(string modelPath, bool preferTensorRtExecutionProvider, out string? diagnostic)
        {
            diagnostic = null;
            try
            {
                InferenceSession session;
                string executionProvider;
                try
                {
                    session = OnnxRuntimeWindowsNative.TryCreateGpuSession(
                                  modelPath,
                                  preferTensorRtExecutionProvider,
                                  out executionProvider,
                                  out _)
                              ??
                              new InferenceSession(modelPath);
                }
                catch (Exception exGpu)
                {
                    try
                    {
                        session = new InferenceSession(modelPath);
                        executionProvider = "CPU";
                    }
                    catch (Exception exCpu)
                    {
                        diagnostic =
                            $"Could not create ONNX session (GPU: {exGpu.Message}; CPU: {exCpu.Message})";
                        return null;
                    }
                }

                var inputName = session.InputMetadata.Keys.FirstOrDefault() ?? "input";
                var inMeta = session.InputMetadata.Values.FirstOrDefault();
                var inputIsNhwc = false;
                int inputChannels;
                if (inMeta is not null && inMeta.Dimensions.Length == 4)
                {
                    var dims = inMeta.Dimensions;
                    if (dims[1] is 3 or 4)
                    {
                        inputChannels = dims[1];
                    }
                    else if (dims[^1] is 3 or 4)
                    {
                        inputIsNhwc = true;
                        inputChannels = dims[^1];
                    }
                    else
                    {
                        diagnostic =
                            $"Specular model input must have 3 or 4 channels (NCHW dim1 or NHWC last dim). " +
                            $"Got input '{inputName}' dimensions [{string.Join(",", dims)}].";
                        session.Dispose();
                        return null;
                    }
                }
                else
                {
                    diagnostic = inMeta is null
                        ? "Specular model has no input metadata."
                        : $"Specular model input must be rank-4 NCHW or NHWC. Got rank {inMeta.Dimensions.Length}.";
                    session.Dispose();
                    return null;
                }

                if (inputChannels is not (3 or 4))
                {
                    session.Dispose();
                    diagnostic = "Specular model input channel count must be 3 or 4.";
                    return null;
                }

                return new Runner(session, inputName, inputIsNhwc, inputChannels, executionProvider);
            }
            catch (Exception ex)
            {
                diagnostic = $"Specular ONNX load error: {ex.Message}";
                return null;
            }
        }

        public bool Predict(
            Image<Rgba32> image,
            float[] edgeMagnitude,
            AutoPbrOptions options,
            byte[] r,
            byte[] g,
            byte[] b,
            byte[] a,
            out string? postprocessError)
        {
            postprocessError = null;
            var w = image.Width;
            var h = image.Height;
            var n = w * h;
            if (edgeMagnitude.Length != n)
            {
                throw new ArgumentException("Edge magnitude size mismatch.", nameof(edgeMagnitude));
            }

            var includeEdge = _inputChannels == 4 && options.MlSpecularUseEdgeChannel;
            var inputTensor = CreateInputTensor(image, edgeMagnitude, includeEdge);
            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };

            lock (_runLock)
            {
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
                var outputTensor = results[0].AsTensor<float>();
                return Postprocess(outputTensor, w, h, r, g, b, a, out postprocessError);
            }
        }

        private DenseTensor<float> CreateInputTensor(Image<Rgba32> img, float[] edgeMagnitude, bool includeEdge)
        {
            var w = img.Width;
            var h = img.Height;
            var c = includeEdge ? 4 : 3;

            if (_inputIsNhwc)
            {
                var data = new float[h * w * c];
                img.ProcessPixelRows(acc =>
                {
                    for (var y = 0; y < h; y++)
                    {
                        var row = acc.GetRowSpan(y);
                        for (var x = 0; x < w; x++)
                        {
                            var p = row[x];
                            var baseIdx = (y * w + x) * c;
                            data[baseIdx] = p.R / 255f;
                            data[baseIdx + 1] = p.G / 255f;
                            data[baseIdx + 2] = p.B / 255f;
                            if (includeEdge)
                            {
                                data[baseIdx + 3] = Math.Clamp(edgeMagnitude[y * w + x], 0f, 1f);
                            }
                        }
                    }
                });
                return new DenseTensor<float>(data, [1, h, w, c]);
            }

            var nchw = new float[c * h * w];
            img.ProcessPixelRows(acc =>
            {
                for (var y = 0; y < h; y++)
                {
                    var row = acc.GetRowSpan(y);
                    for (var x = 0; x < w; x++)
                    {
                        var p = row[x];
                        var idx = y * w + x;
                        nchw[idx] = p.R / 255f;
                        nchw[h * w + idx] = p.G / 255f;
                        nchw[2 * h * w + idx] = p.B / 255f;
                        if (includeEdge)
                        {
                            nchw[3 * h * w + idx] = Math.Clamp(edgeMagnitude[idx], 0f, 1f);
                        }
                    }
                }
            });
            return new DenseTensor<float>(nchw, [1, c, h, w]);
        }

        /// <summary>
        /// Maps model output floats to LabPBR <c>_s</c> bytes. Expects channel order R, G, B, A (first four).
        /// Optional fifth channel is ignored. When sample magnitudes are ≤ ~1.5, treats channels 0–3 as 0–1 and multiplies by 255; otherwise assumes 0–255.
        /// </summary>
        private static bool Postprocess(
            Tensor<float> outTensor,
            int w,
            int h,
            byte[] r,
            byte[] g,
            byte[] b,
            byte[] a,
            out string? error)
        {
            error = null;
            var dims = outTensor.Dimensions.ToArray();
            if (dims.Length != 4)
            {
                error = $"Specular output rank is {dims.Length}, expected 4. Dimensions: [{string.Join(",", dims)}]";
                return false;
            }

            var nhwc = dims[^1] is 4 or 5;
            var nchw = dims[1] is 4 or 5;
            if (!nhwc && !nchw)
            {
                error =
                    $"Specular output must be NCHW (C=4 or 5) or NHWC (last=4 or 5). Dimensions: [{string.Join(",", dims)}]";
                return false;
            }

            var channels = nhwc ? dims[^1] : dims[1];
            if (channels is not (4 or 5))
            {
                error = $"Specular output channel count must be 4 (RGBA) or 5 (legacy; fifth ignored). Dimensions: [{string.Join(",", dims)}]";
                return false;
            }

            var use255Scale = false;
            var sampleH = Math.Min(h, 8);
            var sampleW = Math.Min(w, 8);
            for (var y = 0; y < sampleH && !use255Scale; y++)
            {
                for (var x = 0; x < sampleW; x++)
                {
                    float v0 = nhwc ? outTensor[0, y, x, 0] : outTensor[0, 0, y, x];
                    if (Math.Abs(v0) > 1.5f)
                    {
                        use255Scale = true;
                        break;
                    }
                }
            }

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    float pr, pg, pb, pa;
                    if (nhwc)
                    {
                        pr = outTensor[0, y, x, 0];
                        pg = outTensor[0, y, x, 1];
                        pb = outTensor[0, y, x, 2];
                        pa = outTensor[0, y, x, 3];
                    }
                    else
                    {
                        pr = outTensor[0, 0, y, x];
                        pg = outTensor[0, 1, y, x];
                        pb = outTensor[0, 2, y, x];
                        pa = outTensor[0, 3, y, x];
                    }

                    if (!use255Scale)
                    {
                        pr *= 255f;
                        pg *= 255f;
                        pb *= 255f;
                        pa *= 255f;
                    }

                    var idx = y * w + x;
                    r[idx] = (byte)Math.Clamp((int)MathF.Round(pr), 0, 255);
                    g[idx] = (byte)Math.Clamp((int)MathF.Round(pg), 0, 255);
                    b[idx] = (byte)Math.Clamp((int)MathF.Round(pb), 0, 255);
                    a[idx] = (byte)Math.Clamp((int)MathF.Round(pa), 0, 255);
                }
            }

            return true;
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}
