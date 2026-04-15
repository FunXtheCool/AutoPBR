using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoPBR.Training.Ort;

/// <summary>
/// Loads manifest.jsonl + splits; matches Python <c>SpecularManifestDataset</c> layout (HWC RGB, NCHW network input).
/// </summary>
public sealed class SpecularManifestDataset
{
    private readonly string _root;
    private readonly int _trainRes;
    private readonly int _inChannels;
    private readonly bool _augment;
    private readonly int _alphaIgnoreBelow;
    private readonly Random _rng;
    private readonly Dictionary<string, ManifestRecord> _manifest;
    private readonly IReadOnlyList<string> _ids;

    public int Count => _ids.Count;

    public SpecularManifestDataset(
        string dataRoot,
        string splitName,
        int trainRes = 128,
        int inChannels = 4,
        bool augment = false,
        int alphaIgnoreBelow = 128,
        int? randomSeed = null)
    {
        _root = Path.GetFullPath(dataRoot);
        _trainRes = trainRes;
        _inChannels = inChannels;
        _augment = augment;
        _alphaIgnoreBelow = Math.Clamp(alphaIgnoreBelow, 0, 255);
        _rng = randomSeed is { } s ? new Random(s) : new Random();

        var manifestPath = Path.Combine(_root, "manifest.jsonl");
        _manifest = LoadManifest(manifestPath);
        _ids = LoadSplit(Path.Combine(_root, "splits", $"{splitName}.txt"));
        var missing = _ids.Where(id => !_manifest.ContainsKey(id)).Take(5).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Split references unknown ids: {string.Join(", ", missing)}");
        }
    }

    private static Dictionary<string, ManifestRecord> LoadManifest(string path)
    {
        var map = new Dictionary<string, ManifestRecord>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var rec = JsonSerializer.Deserialize<ManifestRecord>(line);
            if (rec?.Id is null)
            {
                continue;
            }

            map[rec.Id] = rec;
        }

        return map;
    }

    private static List<string> LoadSplit(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Split file not found.", path);
        }

        return File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Packed floats: input NCHW flat [C*H*W], target [4*H*W] planar RGBA, valid [H*W].
    /// <paramref name="scratchRgb"/> must hold at least H×W×3 bytes (interleaved RGB).
    /// </summary>
    public void GetSample(int index, Span<float> input, Span<float> targetRgba, Span<float> validMask, byte[] scratchRgb)
    {
        var id = _ids[index];
        var rec = _manifest[id];
        if (rec.LabelSpec is null)
        {
            throw new InvalidOperationException($"manifest record {id} missing label_spec");
        }

        var imgPath = Path.Combine(_root, rec.Image.Replace('/', Path.DirectorySeparatorChar));
        var specPath = Path.Combine(_root, rec.LabelSpec.Replace('/', Path.DirectorySeparatorChar));

        using var img = Image.Load<Rgba32>(imgPath);
        using var spec = Image.Load<Rgba32>(specPath);
        if (img.Width != spec.Width || img.Height != spec.Height)
        {
            throw new InvalidOperationException($"Spec size mismatch for {id}: {img.Size} vs {spec.Size}");
        }

        img.Mutate(m => m.Resize(_trainRes, _trainRes, KnownResamplers.NearestNeighbor));
        spec.Mutate(m => m.Resize(_trainRes, _trainRes, KnownResamplers.NearestNeighbor));

        var w = _trainRes;
        var h = _trainRes;
        var rgbLen = w * h * 3;
        if (scratchRgb.Length < rgbLen)
        {
            throw new ArgumentException($"scratchRgb needs at least {rgbLen} bytes (HWC RGB).", nameof(scratchRgb));
        }

        CopyRgbInterleaved(img, scratchRgb);
        var rgb = scratchRgb.AsSpan(0, rgbLen);

        if (_augment)
        {
            ApplyAugmentRgb(rgb, w, h, _rng);
        }

        var expectedInput = _inChannels * w * h;
        if (input.Length < expectedInput)
        {
            throw new ArgumentException($"input span too small (need {expectedInput}).", nameof(input));
        }

        var idx = 0;
        for (var c = 0; c < 3; c++)
        {
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var p = (y * w + x) * 3 + c;
                    input[idx++] = rgb[p] / 255f;
                }
            }
        }

        if (_inChannels == 4)
        {
            var edge = VcEdgeChannel.FromRgb(rgb, w, h);
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    input[idx++] = edge[y, x];
                }
            }
        }
        else if (_inChannels != 3)
        {
            throw new ArgumentOutOfRangeException(nameof(_inChannels), "in_channels must be 3 or 4.");
        }

        var expT = 4 * w * h;
        if (targetRgba.Length < expT || validMask.Length < w * h)
        {
            throw new ArgumentException("targetRgba or validMask span too small.");
        }

        CopySpecAndValid(img, spec, targetRgba, validMask, w, h);
    }

    private static void CopyRgbInterleaved(Image<Rgba32> img, byte[] rgb)
    {
        var w = img.Width;
        var h = img.Height;
        for (var y = 0; y < h; y++)
        {
            var row = img.DangerousGetPixelRowMemory(y).Span;
            for (var x = 0; x < w; x++)
            {
                var p = row[x];
                var i = (y * w + x) * 3;
                rgb[i] = p.R;
                rgb[i + 1] = p.G;
                rgb[i + 2] = p.B;
            }
        }
    }

    private void CopySpecAndValid(
        Image<Rgba32> diffuseResized,
        Image<Rgba32> specImg,
        Span<float> targetRgba,
        Span<float> valid,
        int w,
        int h)
    {
        var t = new float[4 * w * h];
        var v = new float[w * h];
        for (var y = 0; y < h; y++)
        {
            var rowD = diffuseResized.DangerousGetPixelRowMemory(y).Span;
            var rowS = specImg.DangerousGetPixelRowMemory(y).Span;
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                var pd = rowD[x];
                var ps = rowS[x];
                v[i] = pd.A >= _alphaIgnoreBelow ? 1f : 0f;
                t[i] = ps.R / 255f;
                t[w * h + i] = ps.G / 255f;
                t[2 * w * h + i] = ps.B / 255f;
                t[3 * w * h + i] = ps.A / 255f;
            }
        }

        t.AsSpan().CopyTo(targetRgba);
        v.AsSpan().CopyTo(valid);
    }

    private static void ApplyAugmentRgb(Span<byte> rgb, int w, int h, Random rng)
    {
        var n = w * h * 3;
        var buf = ArrayPool<float>.Shared.Rent(n);
        try
        {
            for (var i = 0; i < n; i++)
            {
                buf[i] = rgb[i];
            }

            var span = buf.AsSpan(0, n);
            var bright = 1f + (float)(rng.NextDouble() * 0.14 - 0.07);
            for (var i = 0; i < n; i++)
            {
                span[i] *= bright;
            }

            float mean = 0;
            for (var i = 0; i < n; i++)
            {
                mean += span[i];
            }

            mean /= n;
            var c = 1f + (float)(rng.NextDouble() * 0.24 - 0.12);
            for (var i = 0; i < n; i++)
            {
                span[i] = (span[i] - mean) * c + mean;
            }

            for (var pix = 0; pix < w * h; pix++)
            {
                var r = span[pix * 3];
                var g = span[pix * 3 + 1];
                var bch = span[pix * 3 + 2];
                var gray = 0.299f * r + 0.587f * g + 0.114f * bch;
                var s = 1f + (float)(rng.NextDouble() * 0.24 - 0.12);
                span[pix * 3] = gray + s * (r - gray);
                span[pix * 3 + 1] = gray + s * (g - gray);
                span[pix * 3 + 2] = gray + s * (bch - gray);
            }

            if (rng.NextDouble() < 0.15)
            {
                for (var i = 0; i < n; i++)
                {
                    var noise = (float)NextGaussian(rng, 0, 4.0);
                    span[i] += noise;
                }
            }

            for (var i = 0; i < n; i++)
            {
                rgb[i] = (byte)Math.Clamp((int)MathF.Round(span[i]), 0, 255);
            }

            if (rng.NextDouble() < 0.2)
            {
                var ch = Math.Max(3, h / 8);
                var cw = Math.Max(3, w / 8);
                ch = rng.Next(2, ch + 1);
                cw = rng.Next(2, cw + 1);
                var cy = rng.Next(0, h - ch + 1);
                var cx = rng.Next(0, w - cw + 1);
                for (var yy = 0; yy < ch; yy++)
                {
                    for (var xx = 0; xx < cw; xx++)
                    {
                        var px = (cy + yy) * w + (cx + xx);
                        rgb[px * 3] = (byte)rng.Next(0, 256);
                        rgb[px * 3 + 1] = (byte)rng.Next(0, 256);
                        rgb[px * 3 + 2] = (byte)rng.Next(0, 256);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(buf);
        }
    }

    private static double NextGaussian(Random rng, double mean, double stdDev)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    private sealed class ManifestRecord
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("image")]
        public string Image { get; set; } = "";

        [JsonPropertyName("label_spec")]
        public string? LabelSpec { get; set; }
    }
}
