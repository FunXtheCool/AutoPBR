using Microsoft.ML.Tokenizers;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Paths and tokenizer setup for the Optimum-exported <c>sentence-transformers/all-MiniLM-L6-v2</c> bundle under
/// <c>Data/ONNX-AI/all-MiniLM-L6-v2-onnx</c>. Files are shipped next to the app (see Ml/App/CLI csproj Content items), same as other ONNX assets.
/// Tokenization uses <see cref="BertTokenizer"/> over <c>vocab.txt</c> (BERT WordPiece), matching the Hugging Face checkpoint.
/// </summary>
public static class MiniLmOnnxResources
{
    public const string OnnxRootFolderName = "ONNX-AI";
    public const string BundleFolderName = "all-MiniLM-L6-v2-onnx";
    public const string VocabFileName = "vocab.txt";
    public const string PrimaryModelFileName = "model.onnx";

    /// <param name="baseDirectory">Host base directory; default <see cref="AppContext.BaseDirectory"/>.</param>
    public static string GetBundleDirectory(string? baseDirectory = null) =>
        Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "Data", OnnxRootFolderName, BundleFolderName);

    public static string GetVocabPath(string? baseDirectory = null) =>
        Path.Combine(GetBundleDirectory(baseDirectory), VocabFileName);

    /// <summary>
    /// Resolves the ONNX graph: prefers <see cref="PrimaryModelFileName"/>, otherwise the first <c>*.onnx</c> in the bundle folder.
    /// </summary>
    public static string? TryGetModelOnnxPath(string? baseDirectory = null)
    {
        var dir = GetBundleDirectory(baseDirectory);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        var primary = Path.Combine(dir, PrimaryModelFileName);
        if (File.Exists(primary))
        {
            return primary;
        }

        return Directory.EnumerateFiles(dir, "*.onnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    public static bool IsVocabPresent(string? baseDirectory = null) =>
        File.Exists(GetVocabPath(baseDirectory));

    /// <summary>
    /// Creates a BERT tokenizer aligned with MiniLM / sentence-transformers (uncased, basic tokenization).
    /// Returns null if <c>vocab.txt</c> is missing or invalid.
    /// </summary>
    public static BertTokenizer? TryCreateBertTokenizer(string? baseDirectory = null)
    {
        if (!IsVocabPresent(baseDirectory))
        {
            return null;
        }

        var vocab = GetVocabPath(baseDirectory);

        try
        {
            var options = new BertOptions
            {
                LowerCaseBeforeTokenization = true,
                ApplyBasicTokenization = true,
            };

            return BertTokenizer.Create(vocab, options);
        }
        catch
        {
            return null;
        }
    }
}
