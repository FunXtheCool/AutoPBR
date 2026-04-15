using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Runs the shipped <c>all-MiniLM-L6-v2</c> ONNX bundle and returns L2-normalized 384-D embeddings for cosine similarity.
/// </summary>
public sealed class MiniLmEmbeddingEngine : IDisposable
{
    private const int MaxSequenceLength = 128;

    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly object _runLock = new();

    private MiniLmEmbeddingEngine(InferenceSession session, BertTokenizer tokenizer)
    {
        _session = session;
        _tokenizer = tokenizer;
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
            var session = new InferenceSession(modelPath);
            return new MiniLmEmbeddingEngine(session, tokenizer);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns a normalized embedding, or null if the model run fails.</summary>
    public float[]? EmbedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var ids = _tokenizer.EncodeToIds(
            text,
            MaxSequenceLength,
            addSpecialTokens: true,
            out _,
            out _);

        var seqLen = Math.Min(MaxSequenceLength, ids.Count);
        var inputIds = new long[MaxSequenceLength];
        var attention = new long[MaxSequenceLength];
        for (var i = 0; i < seqLen; i++)
        {
            inputIds[i] = ids[i];
            attention[i] = 1;
        }

        var padId = (long)_tokenizer.PaddingTokenId;
        for (var i = seqLen; i < MaxSequenceLength; i++)
        {
            inputIds[i] = padId;
            attention[i] = 0;
        }

        var inputIdsTensor = new DenseTensor<long>(inputIds, [1, MaxSequenceLength]);
        var attentionTensor = new DenseTensor<long>(attention, [1, MaxSequenceLength]);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionTensor),
        };

        DenseTensor<float>? sentence;
        lock (_runLock)
        {
            using var results = _session.Run(inputs);
            sentence = results.FirstOrDefault(o => o.Name == "sentence_embedding")?.AsTensor<float>() as DenseTensor<float>;
        }

        if (sentence is null || sentence.Length == 0)
        {
            return null;
        }

        var dim = sentence.Dimensions[^1];
        if (dim <= 0)
        {
            return null;
        }

        var vec = sentence.ToArray();
        NormalizeInPlace(vec);
        return vec;
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

    public void Dispose() => _session.Dispose();
}
