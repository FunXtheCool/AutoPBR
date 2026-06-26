using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Reads <c>LayerDefinition.create(meshDefinition, texWidth, texHeight)</c> int operands from javap/ASM mesh bytecode text.
/// </summary>
internal static partial class LayerDefinitionAtlasSizeProbe
{
    [GeneratedRegex(
        @"(?:bipush|sipush)\s+(-?\d+)\s*\r?\n\s*\d+:\s*(?:bipush|sipush)\s+(-?\d+)\s*\r?\n\s*\d+:\s*invokestatic\s+#\d+\s+//\s*Method\s+[\w$/.]+\.(?:create|m_\w+):\([^)]*\)L[\w$/.]+LayerDefinition;",
        RegexOptions.Multiline)]
    private static partial Regex CreateLayerDefinitionPairRegex();

    public static bool TryRead(string meshBytecodeText, out int textureWidth, out int textureHeight)
    {
        textureWidth = 0;
        textureHeight = 0;
        if (string.IsNullOrWhiteSpace(meshBytecodeText))
        {
            return false;
        }

        foreach (Match m in CreateLayerDefinitionPairRegex().Matches(meshBytecodeText))
        {
            if (!TryParseAtlasPair(m.Groups[1].Value, m.Groups[2].Value, out textureWidth, out textureHeight))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads the terminal <c>LayerDefinition.create</c> on the primary mesh island (before supplementary factories).
    /// Avoids false early matches from unrelated <c>bipush</c> pairs in the same bytecode stream.
    /// </summary>
    public static bool TryReadPrimaryIsland(string meshBytecodeText, out int textureWidth, out int textureHeight)
    {
        textureWidth = 0;
        textureHeight = 0;
        if (string.IsNullOrWhiteSpace(meshBytecodeText))
        {
            return false;
        }

        var marker = JavapClassDisassembly.GeometryMeshIslandBoundaryMarker;
        var primary = meshBytecodeText.Split(marker, 2, StringSplitOptions.None)[0];
        Match? last = null;
        foreach (Match m in CreateLayerDefinitionPairRegex().Matches(primary))
        {
            last = m;
        }

        return last is not null &&
               TryParseAtlasPair(last.Groups[1].Value, last.Groups[2].Value, out textureWidth, out textureHeight);
    }

    private static bool TryParseAtlasPair(string widthText, string heightText, out int textureWidth, out int textureHeight)
    {
        textureWidth = 0;
        textureHeight = 0;
        return int.TryParse(widthText, out textureWidth) &&
               int.TryParse(heightText, out textureHeight) &&
               textureWidth > 0 &&
               textureHeight > 0 &&
               textureWidth <= 512 &&
               textureHeight <= 512;
    }
}
