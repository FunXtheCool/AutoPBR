using System.Text;

using AutoPBR.Tools.GeometryCompiler;

namespace AutoPBR.Tools.AnimationCompiler;

/// <summary>
/// Rewrites obfuscated <c>javap -c</c> output into Mojang-style comments/signatures so
/// <see cref="AnimationClinitLift"/> can parse ProGuard 1.21.11 animation definition classes.
/// </summary>
internal static class AnimationJavapObfuscationNormalizer
{
    private const string AnimationDefinition = "net.minecraft.client.animation.AnimationDefinition";
    private const string Builder = "net.minecraft.client.animation.AnimationDefinition$Builder";
    private const string Channel = "net.minecraft.client.animation.AnimationChannel";
    private const string Keyframe = "net.minecraft.client.animation.Keyframe";
    private const string KeyframeAnimations = "net.minecraft.client.animation.KeyframeAnimations";
    private const string Targets = "net.minecraft.client.animation.AnimationChannel$Targets";
    private const string Interpolations = "net.minecraft.client.animation.AnimationChannel$Interpolations";
    private const string Target = "net.minecraft.client.animation.AnimationChannel$Target";
    private const string Interpolation = "net.minecraft.client.animation.AnimationChannel$Interpolation";

    public static string Normalize(string javapStdout, string officialAnimationJvmName, MojangMappingsParser maps)
    {
        if (!maps.TryGetObfuscated(officialAnimationJvmName, out _))
        {
            return javapStdout;
        }

        var ad = ObfJavapInner(maps, AnimationDefinition);
        var builder = ObfJavapInner(maps, Builder);
        var channel = ObfShort(maps, Channel);
        var keyframe = ObfShort(maps, Keyframe);
        var kfa = ObfShort(maps, KeyframeAnimations);
        var targets = ObfJavapInner(maps, Targets);
        var interps = ObfJavapInner(maps, Interpolations);
        var target = ObfShort(maps, Target);
        var interp = ObfShort(maps, Interpolation);

        var sb = new StringBuilder(javapStdout);

        // Method comments while obfuscated short names are still intact ($ in javap inner names breaks $"{name}" interpolation).
        Replace(sb, builder + ".a:(F)", "net/minecraft/client/animation/AnimationDefinition$Builder.withLength:(F)");
        Replace(sb, builder + ".a:()L" + builder + ";",
            "net/minecraft/client/animation/AnimationDefinition$Builder.looping:()Lnet/minecraft/client/animation/AnimationDefinition$Builder;");
        Replace(sb, builder + ".a:(Ljava/lang/String;L" + channel + ";)",
            "net/minecraft/client/animation/AnimationDefinition$Builder.addAnimation:(Ljava/lang/String;Lnet/minecraft/client/animation/AnimationChannel;)");
        Replace(sb, builder + ".b:()L" + ad + ";", "net/minecraft/client/animation/AnimationDefinition.build:()Lnet/minecraft/client/animation/AnimationDefinition;");

        Replace(sb, kfa + ".b:(FFF)", "net/minecraft/client/animation/KeyframeAnimations.degreeVec:(FFF)");
        Replace(sb, kfa + ".a:(FFF)", "net/minecraft/client/animation/KeyframeAnimations.posVec:(FFF)");
        Replace(sb, kfa + ".a:(DDD)", "net/minecraft/client/animation/KeyframeAnimations.scaleVec:(DDD)");

        Replace(sb, "Field " + targets + ".a:L" + target + ";",
            "Field net/minecraft/client/animation/AnimationChannel$Targets.POSITION:Lnet/minecraft/client/animation/AnimationChannel$Target;");
        Replace(sb, "Field " + targets + ".b:L" + target + ";",
            "Field net/minecraft/client/animation/AnimationChannel$Targets.ROTATION:Lnet/minecraft/client/animation/AnimationChannel$Target;");
        Replace(sb, "Field " + targets + ".c:L" + target + ";",
            "Field net/minecraft/client/animation/AnimationChannel$Targets.SCALE:Lnet/minecraft/client/animation/AnimationChannel$Target;");
        Replace(sb, targets + ".a:", "net/minecraft/client/animation/AnimationChannel$Targets.POSITION:");
        Replace(sb, targets + ".b:", "net/minecraft/client/animation/AnimationChannel$Targets.ROTATION:");
        Replace(sb, targets + ".c:", "net/minecraft/client/animation/AnimationChannel$Targets.SCALE:");

        Replace(sb, "Field " + interps + ".a:L" + interp + ";",
            "Field net/minecraft/client/animation/AnimationChannel$Interpolations.LINEAR:Lnet/minecraft/client/animation/AnimationChannel$Interpolation;");
        Replace(sb, "Field " + interps + ".b:L" + interp + ";",
            "Field net/minecraft/client/animation/AnimationChannel$Interpolations.CATMULLROM:Lnet/minecraft/client/animation/AnimationChannel$Interpolation;");
        Replace(sb, interps + ".a:", "net/minecraft/client/animation/AnimationChannel$Interpolations.LINEAR:");
        Replace(sb, interps + ".b:", "net/minecraft/client/animation/AnimationChannel$Interpolations.CATMULLROM:");

        Replace(sb, "Method " + channel + ".\"<init>\"",
            "Method net/minecraft/client/animation/AnimationChannel.\"<init>\"");
        Replace(sb, "Method " + keyframe + ".\"<init>\"",
            "Method net/minecraft/client/animation/Keyframe.\"<init>\"");

        // Inner-class L descriptors before outer (Lgfz; must not run before Lgfz$a;).
        Replace(sb, "L" + builder + ";", "Lnet/minecraft/client/animation/AnimationDefinition$Builder;");
        Replace(sb, "L" + targets + ";", "Lnet/minecraft/client/animation/AnimationChannel$Targets;");
        Replace(sb, "L" + interps + ";", "Lnet/minecraft/client/animation/AnimationChannel$Interpolations;");
        Replace(sb, "L" + channel + ";", "Lnet/minecraft/client/animation/AnimationChannel;");
        Replace(sb, "L" + keyframe + ";", "Lnet/minecraft/client/animation/Keyframe;");
        Replace(sb, "L" + target + ";", "Lnet/minecraft/client/animation/AnimationChannel$Target;");
        Replace(sb, "L" + interp + ";", "Lnet/minecraft/client/animation/AnimationChannel$Interpolation;");
        Replace(sb, "L" + ad + ";", "Lnet/minecraft/client/animation/AnimationDefinition;");

        Replace(sb, "public static final " + ad + " ", "public static final net.minecraft.client.animation.AnimationDefinition ");
        Replace(sb, "class " + channel, "class net/minecraft/client/animation/AnimationChannel");
        Replace(sb, "class " + keyframe, "class net/minecraft/client/animation/Keyframe");

        foreach (var (obfField, namedField) in maps.GetObfToNamedFields(officialAnimationJvmName))
        {
            Replace(sb, $"Field {obfField}:Lnet/minecraft/client/animation/AnimationDefinition;",
                $"Field {namedField}:Lnet/minecraft/client/animation/AnimationDefinition;");
        }

        return sb.ToString();
    }

    private static string ObfShort(MojangMappingsParser maps, string namedFqn) =>
        maps.TryGetObfuscated(namedFqn, out var obf)
            ? MojangMappingsParser.GetJavapClassArgForObfuscated(obf)
            : namedFqn;

    private static string ObfJavapInner(MojangMappingsParser maps, string namedFqn) =>
        maps.TryGetObfuscated(namedFqn, out var obf)
            ? MojangMappingsParser.GetJavapClassArgForObfuscated(obf)
            : MojangMappingsParser.GetJavapClassArgForObfuscated(namedFqn);

    private static void Replace(StringBuilder sb, string oldValue, string newValue)
    {
        if (oldValue.Length == 0 || !sb.ToString().Contains(oldValue, StringComparison.Ordinal))
        {
            return;
        }

        var text = sb.ToString().Replace(oldValue, newValue, StringComparison.Ordinal);
        sb.Clear();
        sb.Append(text);
    }
}
