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
            if (!int.TryParse(m.Groups[1].Value, out var w) ||
                !int.TryParse(m.Groups[2].Value, out var h) ||
                w <= 0 ||
                h <= 0 ||
                w > 512 ||
                h > 512)
            {
                continue;
            }

            textureWidth = w;
            textureHeight = h;
            return true;
        }

        return false;
    }
}
