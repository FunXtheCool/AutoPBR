namespace AutoPBR.Core.Preview;

/// <summary>
/// Placeholder for future Bedrock geometry (.geo.json / render controllers). The GPU multi-batch path is format-agnostic;
/// only pack parsing would be added here.
/// </summary>
public static class BedrockPreviewModelSourceStub
{
    public const string FormatId = "bedrock";

    public static bool IsModelPreviewSupported => false;
}
