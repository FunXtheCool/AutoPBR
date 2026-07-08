using System.Text.Json;
using System.Text.Json.Nodes;


namespace AutoPBR.Tools.GeometryCompiler;

internal sealed partial class GeometryCompilerHost
{
    private static readonly JsonSerializerOptions WriteIndentedJson = new(JsonSerializerOptions.Default)
    {
        WriteIndented = true
    };

    private static readonly object ConsoleLogLock = new();

    private int _batchProgressCompleted;

    private readonly string _clientJar;
    private readonly string _versionLabel;
    private readonly string _outDir;
    private readonly string? _javap;
    private readonly MojangMappingsParser? _maps;
    private readonly int _maxBatchParallelism;
    private readonly bool _quiet;
    private readonly bool _emitStats;
    private readonly bool _useAsmLift;
    private readonly bool _compareLift;

    public GeometryCompilerHost(string clientJar, string? mappingsPath, string versionLabel, string outDir,
        string? javapOverride, int maxBatchParallelism = 1, bool quiet = false, bool emitStats = false,
        bool useAsmLift = false, bool compareLift = false)
    {
        _clientJar = clientJar;
        _versionLabel = versionLabel;
        _outDir = outDir;
        _javap = string.IsNullOrWhiteSpace(javapOverride) ? GeometryJavapLocator.FindJavap() : javapOverride;
        _maps = string.IsNullOrWhiteSpace(mappingsPath) || !File.Exists(mappingsPath)
            ? null
            : MojangMappingsParser.Load(mappingsPath);
        _maxBatchParallelism = Math.Max(1, maxBatchParallelism);
        _quiet = quiet;
        _emitStats = emitStats;
        _useAsmLift = useAsmLift;
        _compareLift = compareLift;
    }

    public int RunSingle(string officialJvmName, string factoryMethod) =>
        ProcessOne(officialJvmName, factoryMethod, writeIndex: true);

    private void LogLine(string message)
    {
        if (_quiet)
        {
            return;
        }

        lock (ConsoleLogLock)
        {
            Console.WriteLine(message);
        }
    }

    private static void LogErrorLine(string message)
    {
        lock (ConsoleLogLock)
        {
            Console.Error.WriteLine(message);
        }
    }
}
