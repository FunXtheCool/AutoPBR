using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using AutoPBR.Core;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Runs the shipped <c>all-MiniLM-L6-v2</c> ONNX bundle and returns L2-normalized 384-D embeddings for cosine similarity.
/// </summary>
public sealed class MiniLmEmbeddingEngine : IDisposable
{
    private const int MaxSequenceLength = 128;
    private const int MaxPersistentEntries = 40000;

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly object _runLock = new();
    private readonly object _cacheLock = new();
    private readonly string _modelSignature;
    private readonly Dictionary<string, MiniLmEmbeddingCacheEntry> _persistentCache = new(StringComparer.Ordinal);
    private bool _persistentCacheDirty;
    private int _vectorDimension;

    private MiniLmEmbeddingEngine(InferenceSession session, BertTokenizer tokenizer, string modelSignature)
    {
        _session = session;
        _tokenizer = tokenizer;
        _modelSignature = modelSignature;
        LoadPersistentCache();
    }

    public static MiniLmEmbeddingEngine? TryCreate(string? baseDirectory = null)
    {
        var modelPath = MiniLmOnnxResources.TryGetModelOnnxPath(baseDirectory);
        var tokenizer = MiniLmOnnxResources.TryCreateBertTokenizer(baseDirectory);
        if (modelPath is null || tokenizer is null)
        {
            return null;
        }

        try
        {
            using var options = OnnxRuntimeSessionOptions.CreateCpuSingleThreaded();
            var session = new InferenceSession(modelPath, options);
            var modelSignature = MiniLmEmbeddingPersistentCache.ComputeModelSignature(
                modelPath,
                MiniLmOnnxResources.GetVocabPath(baseDirectory));
            return new MiniLmEmbeddingEngine(session, tokenizer, modelSignature);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns a normalized embedding, or null if the model run fails.</summary>
    public float[]? EmbedText(string text)
    {
        var map = EmbedTexts([text]);
        return map.GetValueOrDefault(text);
    }

    /// <summary>
    /// Returns normalized embeddings for a batch of distinct texts.
    /// Missing entries indicate failed tokenization/inference for that input.
    /// </summary>
    public Dictionary<string, float[]> EmbedTexts(IReadOnlyCollection<string> texts)
    {
        var result = new Dictionary<string, float[]>(StringComparer.Ordinal);
        if (texts.Count == 0)
        {
            return result;
        }

        var unique = new List<string>(texts.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in texts)
        {
            if (string.IsNullOrWhiteSpace(t))
            {
                continue;
            }

            if (seen.Add(t))
            {
                unique.Add(t);
            }
        }
        if (unique.Count == 0)
        {
            return result;
        }

        var nowTicks = DateTime.UtcNow.Ticks;
        var missing = new List<string>(unique.Count);
        lock (_cacheLock)
        {
            foreach (var text in unique)
            {
                if (_persistentCache.TryGetValue(text, out var cached))
                {
                    if (_vectorDimension <= 0)
                    {
                        _vectorDimension = cached.Vector.Length;
                    }

                    if (_vectorDimension > 0 && cached.Vector.Length != _vectorDimension)
                    {
                        missing.Add(text);
                        continue;
                    }

                    cached.LastAccessUtcTicks = nowTicks;
                    result[text] = cached.Vector;
                }
                else
                {
                    missing.Add(text);
                }
            }
        }

        if (missing.Count == 0)
        {
            return result;
        }

        var batchSize = missing.Count;
        var inputIds = new long[batchSize * MaxSequenceLength];
        var attention = new long[batchSize * MaxSequenceLength];
        var padId = (long)_tokenizer.PaddingTokenId;

        for (var row = 0; row < batchSize; row++)
        {
            var ids = _tokenizer.EncodeToIds(
                missing[row],
                MaxSequenceLength,
                addSpecialTokens: true,
                out _,
                out _);
            var seqLen = Math.Min(MaxSequenceLength, ids.Count);
            var rowOffset = row * MaxSequenceLength;
            for (var i = 0; i < seqLen; i++)
            {
                inputIds[rowOffset + i] = ids[i];
                attention[rowOffset + i] = 1;
            }

            for (var i = seqLen; i < MaxSequenceLength; i++)
            {
                inputIds[rowOffset + i] = padId;
                attention[rowOffset + i] = 0;
            }
        }

        var inputIdsTensor = new DenseTensor<long>(inputIds, [batchSize, MaxSequenceLength]);
        var attentionTensor = new DenseTensor<long>(attention, [batchSize, MaxSequenceLength]);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionTensor),
        };

        float[] flat;
        int embeddingDim;
        lock (_runLock)
        {
            using var results = _session.Run(inputs);
            DenseTensor<float>? sentence = null;
            foreach (var output in results)
            {
                if (!string.Equals(output.Name, "sentence_embedding", StringComparison.Ordinal))
                {
                    continue;
                }

                sentence = output.AsTensor<float>() as DenseTensor<float>;
                break;
            }

            if (sentence is null || sentence.Length == 0 || sentence.Rank < 2)
            {
                return result;
            }

            embeddingDim = sentence.Dimensions[^1];
            if (embeddingDim <= 0)
            {
                return result;
            }

            flat = sentence.ToArray();
        }

        for (var row = 0; row < batchSize; row++)
        {
            var vec = new float[embeddingDim];
            Array.Copy(flat, row * embeddingDim, vec, 0, embeddingDim);
            NormalizeInPlace(vec);
            var text = missing[row];
            result[text] = vec;
            lock (_cacheLock)
            {
                _vectorDimension = embeddingDim;
                _persistentCache[text] = new MiniLmEmbeddingCacheEntry
                {
                    Text = text,
                    Vector = vec,
                    LastAccessUtcTicks = nowTicks
                };
                _persistentCacheDirty = true;
            }
        }

        return result;
    }

    private void LoadPersistentCache()
    {
        var snapshot = MiniLmEmbeddingPersistentCache.Load(_modelSignature);
        if (snapshot is null || snapshot.Entries.Count == 0)
        {
            return;
        }

        if (snapshot.VectorDimension <= 0)
        {
            return;
        }

        lock (_cacheLock)
        {
            _vectorDimension = snapshot.VectorDimension;
            foreach (var entry in snapshot.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Text) ||
                    entry.Vector is null ||
                    entry.Vector.Length != _vectorDimension)
                {
                    continue;
                }

                _persistentCache[entry.Text] = entry;
            }
        }
    }

    private void PersistCacheIfNeeded()
    {
        MiniLmEmbeddingCacheSnapshot? snapshot = null;
        lock (_cacheLock)
        {
            if (!_persistentCacheDirty || _persistentCache.Count == 0 || _vectorDimension <= 0)
            {
                return;
            }

            if (_persistentCache.Count > MaxPersistentEntries)
            {
                var toRemove = _persistentCache.Values
                    .OrderBy(static e => e.LastAccessUtcTicks)
                    .Take(_persistentCache.Count - MaxPersistentEntries)
                    .Select(static e => e.Text)
                    .ToList();
                foreach (var key in toRemove)
                {
                    _persistentCache.Remove(key);
                }
            }

            snapshot = new MiniLmEmbeddingCacheSnapshot
            {
                ModelSignature = _modelSignature,
                VectorDimension = _vectorDimension,
                Entries = _persistentCache.Values
                    .OrderByDescending(static e => e.LastAccessUtcTicks)
                    .Take(MaxPersistentEntries)
                    .ToList()
            };
            _persistentCacheDirty = false;
        }

        MiniLmEmbeddingPersistentCache.Save(snapshot);
    }

    private static void NormalizeInPlace(float[] v)
    {
        double sumSq = 0;
        foreach (var x in v)
        {
            sumSq += x * (double)x;
        }

        if (sumSq < 1e-20)
        {
            return;
        }

        var inv = 1.0 / Math.Sqrt(sumSq);
        for (var i = 0; i < v.Length; i++)
        {
            v[i] = (float)(v[i] * inv);
        }
    }

    public void Dispose()
    {
        PersistCacheIfNeeded();
        _session.Dispose();
    }
}
