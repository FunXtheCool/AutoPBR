using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Tools.AnimationCompiler;

internal static class RendererStateLift
{
    private static readonly Regex RenderStateTypeRegex = new(
        @"(?<type>net[/\.]minecraft[/\.]client[/\.]renderer[/\.]entity[/\.]state[/\.][A-Za-z0-9_$]+RenderState)",
        RegexOptions.Compiled);

    private static readonly Regex ModelTypeRegex = new(
        @"net/minecraft/client/model/[A-Za-z0-9_/$]+Model",
        RegexOptions.Compiled);

    private static readonly Regex AnimationStateFieldRegex = new(
        @"Field\s+(?:(?:[A-Za-z0-9_/$]+\.)?)(?<field>[A-Za-z0-9_$]+):Lnet/minecraft/world/entity/AnimationState;",
        RegexOptions.Compiled);

    private static readonly Regex ScalarFieldRegex = new(
        @"Field\s+(?:(?:[A-Za-z0-9_/$]+\.)?)(?<field>[A-Za-z0-9_$]+):(?<type>[FZI])",
        RegexOptions.Compiled);

    private static readonly HashSet<string> LivingEntityFields = new(StringComparer.Ordinal)
    {
        "ageInTicks",
        "walkAnimationPos",
        "walkAnimationSpeed",
        "xRot",
        "yRot",
        "isBaby",
        "ageScale",
        "scale"
    };

    public static bool TryLift(
        string javapOutput,
        string officialRendererJvmName,
        out JsonObject shard,
        out List<string> notes)
    {
        notes = [];
        shard = new JsonObject();
        if (string.IsNullOrWhiteSpace(javapOutput) ||
            !officialRendererJvmName.Contains(".renderer.entity.", StringComparison.Ordinal))
        {
            notes.Add("Not a renderer javap disassembly.");
            return false;
        }

        var renderStateType = ResolveRenderStateType(javapOutput);
        var modelJvmNames = ResolveModelJvmNames(javapOutput);
        var animationFields = ResolveAnimationStateFields(javapOutput);
        var scalarFields = ResolveScalarFields(javapOutput);
        if (string.IsNullOrWhiteSpace(renderStateType) &&
            modelJvmNames.Length == 0 &&
            animationFields.Length == 0 &&
            scalarFields.Length == 0)
        {
            notes.Add("No renderer-state MVP patterns found.");
            return false;
        }

        var shortName = officialRendererJvmName[(officialRendererJvmName.LastIndexOf('.') + 1)..];
        var driverStem = ToSnakeCase(shortName.EndsWith("Renderer", StringComparison.Ordinal)
            ? shortName[..^"Renderer".Length]
            : shortName);
        shard["schemaVersion"] = 1;
        shard["officialJvmName"] = officialRendererJvmName;
        shard["renderStateType"] = renderStateType;
        shard["modelJvmNames"] = ToJsonArray(modelJvmNames);
        shard["extractionStatus"] = "compiler-lift-preview";
        shard["extractionNotes"] = new JsonArray
        {
            "RendererStateLift MVP: direct renderer-state field discovery only.",
            "Layer/emissive/entity-method semantics remain deferred."
        };
        shard["animationStateFields"] = ToAnimationFieldArray(animationFields);
        shard["scalarRenderStateFields"] = ToScalarFieldArray(scalarFields);
        shard["livingEntityFields"] = ToJsonArray(LivingEntityFields.Order(StringComparer.Ordinal));
        var modelLayers = ResolveModelLayers(javapOutput);
        if (modelLayers is not null)
        {
            shard["modelLayers"] = modelLayers;
        }
        if (animationFields.Length > 0)
        {
            shard["previewDriver"] = $"{driverStem}_clip_cycle";
            var clips = new JsonObject { ["walkOnly"] = 2f };
            foreach (var field in animationFields)
            {
                clips[field] = 1f;
            }

            shard["clipLengthsSeconds"] = clips;
        }
        else
        {
            shard["previewDriver"] = "static_scalar_state";
            var defaults = new JsonObject();
            foreach (var field in scalarFields)
            {
                defaults[field] = field.StartsWith("is", StringComparison.Ordinal) ? true : 0f;
            }

            shard["scalarDefaults"] = defaults;
        }

        return true;
    }

    private static string ResolveRenderStateType(string javapOutput)
    {
        var match = RenderStateTypeRegex.Match(javapOutput);
        return match.Success ? match.Groups["type"].Value.Replace('/', '.') : "";
    }

    private static string[] ResolveModelJvmNames(string javapOutput) =>
        ModelTypeRegex
            .Matches(javapOutput)
            .Select(m => m.Value.Replace('/', '.'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] ResolveAnimationStateFields(string javapOutput) =>
        AnimationStateFieldRegex
            .Matches(javapOutput)
            .Select(m => m.Groups["field"].Value)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] ResolveScalarFields(string javapOutput) =>
        ScalarFieldRegex
            .Matches(javapOutput)
            .Select(m => m.Groups["field"].Value)
            .Where(f => !string.IsNullOrWhiteSpace(f) && !LivingEntityFields.Contains(f))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonArray ToAnimationFieldArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(new JsonObject
            {
                ["renderStateField"] = value,
                ["setupAnimStateField"] = value,
                ["entityAnimationState"] = value
            });
        }

        return array;
    }

    private static JsonArray ToScalarFieldArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(new JsonObject
            {
                ["renderStateField"] = value,
                ["setupAnimStateField"] = value
            });
        }

        return array;
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "renderer";
        }

        var chars = new List<char>(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c) && i > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray());
    }

    private static readonly (string FactoryMethod, string TextureKey, PreviewDepthLayerKind Layer)[] SupplementaryLayerFactories =
    [
        ("createWindLayer", "#wind", PreviewDepthLayerKind.CutoutOverlay),
        ("createEyesLayer", "#eyes", PreviewDepthLayerKind.CosmeticOverlay),
        ("createOuterLayer", "#outer", PreviewDepthLayerKind.CutoutOverlay),
        ("createEmissiveLayer", "#emissive", PreviewDepthLayerKind.EmissiveOverlay),
    ];

    private static JsonArray? ResolveModelLayers(string javapOutput)
    {
        var array = new JsonArray();
        foreach (var (factoryMethod, textureKey, layer) in SupplementaryLayerFactories)
        {
            if (!javapOutput.Contains(factoryMethod, StringComparison.Ordinal))
            {
                continue;
            }

            array.Add(new JsonObject
            {
                ["factoryMethod"] = factoryMethod,
                ["textureKey"] = textureKey,
                ["previewDepthLayer"] = GeometryIrCuboidMetadata.ToPreviewDepthLayerJsonName(layer),
            });
        }

        return array.Count > 0 ? array : null;
    }
}
