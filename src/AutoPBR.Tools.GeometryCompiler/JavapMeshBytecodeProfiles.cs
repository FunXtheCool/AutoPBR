using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>Vanilla / obfuscated <c>CubeListBuilder.addBox</c> overload kind inferred from merged <c>javap</c> comments.</summary>
internal enum AddBoxInvokeShape
{
    Unknown,
    /// <summary>Six float sizes after current <c>texOffs</c> stack.</summary>
    Float6,
    /// <summary><c>addBox</c> ending with <c>CubeDeformation;FF)</c> inflation floats.</summary>
    Float6CubeDeformationTailFloats,
    /// <summary><c>addBox(Ljava/lang/String;FFFFFF)</c> mirrored quad key.</summary>
    Float6StringQuadKey,
    /// <summary><c>addBox(FFFFFFLjava/util/Set;)</c> direction mask — geometry lifted as full box (approximation).</summary>
    Float6DirectionFaceSet,
    /// <summary><c>addBox(Ljava/lang/String;FFFFFFLjava/util/Set;)</c> mirrored quad + direction mask (full box approximation).</summary>
    Float6StringQuadKeyDirectionFaceSet,
    /// <summary>
    /// <c>String</c> quad key + origin floats + integer dimensions + <c>CubeDeformation</c> + UV crop ints (Forge/Mojang texCrop overload).
    /// </summary>
    StringQuadThreeFloatThreeIntsCubeDefTexCropInts,
    /// <summary>Same without deformation parameter.</summary>
    StringQuadThreeFloatThreeIntsTexCropIntsNoDef,
}

/// <summary>Detects mesh factory / lifter signals in <c>javap -c</c> text for named or obfuscated (ProGuard) output.</summary>
internal static class JavapMeshBytecodeProfiles
{
    private static readonly Regex ObfPartDefinitionAddChildRegex = new(
        @"//\s*Method\s+(\w+)\.(\w+):\(Ljava/lang/String;L(\w+);L(\w+);\)L\1;",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>Fluent CubeListBuilder-style <c>addBox</c> with obfuscated type names (receiver == return).</summary>
    private static readonly Regex ObfFluentAddBoxRegex = new(
        @"//\s*Method\s+(\w+)\.\w+:\((?:Ljava/lang/String;)?(?:FFFFFFL[\w$]+;|FFFFFF|Ljava/lang/String;FFFFFF|Ljava/lang/String;FFFFFFL[\w$]+;)\)L(\w+);\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>Obfuscated <c>…FFFFFFLjava/util/Set;…)Lbuilder;</c> direction-mask cubes.</summary>
    private static readonly Regex ObfFluentDirectionMaskAddBoxRegex = new(
        @"//\s*Method\s+(\w+)\.\w+:\([^)]*(?:Ljava/lang/String;)?FFFFFFLjava/util/Set[^)]*\)\s*L(\w+);\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>Obfuscated string-quad texCrop-style <c>…Ljava/lang/String;FFFIII…</c> overloads.</summary>
    private static readonly Regex ObfFluentTexCropAddBoxRegex = new(
        @"//\s*Method\s+(\w+)\.\w+:\([^)]*Ljava/lang/String;FFFIII[^)]*\)\s*L(\w+);\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex ObfFluentTexOffsRegex = new(
        @"//\s*Method\s+(\w+)\.\w+:\(II\)L(\w+);\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// Named-jar <c>javap -c</c> uses slash-qualified types in comments, e.g.
    /// <c>// Method net/minecraft/.../Foo.createBar:(...)Lnet/minecraft/.../MeshDefinition;</c>.
    /// </summary>
    private static readonly Regex InvokeStaticMethodCommentRegex = new(
        @"invokestatic\s+#\d+\s+//\s*Method\s+(?:([\w\./]+)\.)?([\w$]+):(\([^)]*\))L[^;]+;",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// Named <c>javap -c</c> often wraps long <c>// Method … addBox:(</c> comments so <c>(FFFFFF)</c> appears on the next line;
    /// obfuscated output may omit the <c>CubeListBuilder</c> token on a single line.
    /// </summary>
    private static bool ContainsFluentAddBoxLiftSignature(string javapC)
    {
        if (javapC.Contains("Ljava/lang/String;FFFIII", StringComparison.Ordinal) &&
            (javapC.Contains("addBox:(", StringComparison.Ordinal) ||
             javapC.Contains("CubeListBuilder.addBox", StringComparison.Ordinal)))
        {
            return true;
        }

        if (javapC.Contains("FFFFFFLjava/util/Set", StringComparison.Ordinal) &&
            (javapC.Contains("addBox:(", StringComparison.Ordinal) ||
             javapC.Contains("CubeListBuilder.addBox", StringComparison.Ordinal)))
        {
            return true;
        }

        if (javapC.Contains("(FFFFFF)", StringComparison.Ordinal) ||
            (javapC.Contains("FFFFFF)", StringComparison.Ordinal) &&
             (javapC.Contains("addBox:(", StringComparison.Ordinal) ||
              javapC.Contains("CubeListBuilder.addBox", StringComparison.Ordinal))))
        {
            return true;
        }

        // ProGuard: fluent cube overloads use short method names (e.g. hdl.a:(FFFFFFLhdk;)Lhdl;) without "addBox".
        if (Regex.IsMatch(javapC,
                @"//\s*Method\s+[\w$]+\.[\w$]+:\([^)]*FFFFFFL[\w$]+;\)[^;]*;",
                RegexOptions.CultureInvariant))
        {
            return true;
        }

        foreach (var line in javapC.Split('\n'))
        {
            if (ObfFluentAddBoxRegex.IsMatch(line.TrimEnd('\r')))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsMeshSignals(string javapC) =>
        javapC.Contains("CubeListBuilder", StringComparison.Ordinal) ||
        javapC.Contains("PartDefinition.addOrReplaceChild", StringComparison.Ordinal) ||
        javapC.Contains("MeshDefinition", StringComparison.Ordinal) ||
        javapC.Contains("LayerDefinition", StringComparison.Ordinal) ||
        (javapC.Contains("invokevirtual", StringComparison.Ordinal) &&
         ContainsFluentAddBoxLiftSignature(javapC) &&
         javapC.Contains("// Method", StringComparison.Ordinal));

    public static bool IsNamedOrObfuscatedMeshBindingLine(string line) =>
        (line.Contains("PartDefinition.addOrReplaceChild", StringComparison.Ordinal) &&
         line.Contains("Ljava/lang/String;", StringComparison.Ordinal)) ||
        (line.Contains("addOrReplaceChild:(Ljava/lang/String;", StringComparison.Ordinal) &&
         line.Contains("CubeListBuilder", StringComparison.Ordinal) &&
         line.Contains("PartPose", StringComparison.Ordinal)) ||
        (line.Contains("invokevirtual", StringComparison.Ordinal) &&
         line.Contains("addOrReplaceChild", StringComparison.Ordinal) &&
         line.Contains("CubeListBuilder", StringComparison.Ordinal) &&
         line.Contains("PartPose", StringComparison.Ordinal)) ||
        ObfPartDefinitionAddChildRegex.IsMatch(line);

    public static bool IsNamedOrObfuscatedFloatAddBoxLine(string line, out bool stringMirrorOverload)
    {
        stringMirrorOverload = line.Contains("Ljava/lang/String;FFFFFF", StringComparison.Ordinal);
        if (line.Contains("CubeListBuilder.addBox", StringComparison.Ordinal))
        {
            return true;
        }

        return ObfFluentAddBoxRegex.IsMatch(line) ||
               ObfFluentDirectionMaskAddBoxRegex.IsMatch(line) ||
               ObfFluentTexCropAddBoxRegex.IsMatch(line);
    }

    /// <summary>Infers overload shape from a merged <c>invokevirtual … addBox:(…)L…;</c> comment line.</summary>
    public static AddBoxInvokeShape ClassifyAddBoxInvokeShape(string mergedInvokeLine)
    {
        if (!mergedInvokeLine.Contains("addBox", StringComparison.Ordinal))
        {
            if (ObfFluentAddBoxRegex.IsMatch(mergedInvokeLine))
            {
                if (mergedInvokeLine.Contains("Ljava/lang/String;FFFFFF", StringComparison.Ordinal))
                {
                    return AddBoxInvokeShape.Float6StringQuadKey;
                }

                return AddBoxInvokeShape.Float6;
            }

            return AddBoxInvokeShape.Unknown;
        }

        if (mergedInvokeLine.Contains("Ljava/lang/String;FFFFFF", StringComparison.Ordinal) &&
            mergedInvokeLine.Contains("Ljava/util/Set", StringComparison.Ordinal))
        {
            return AddBoxInvokeShape.Float6StringQuadKeyDirectionFaceSet;
        }

        if (mergedInvokeLine.Contains("FFFFFFLjava/util/Set", StringComparison.Ordinal))
        {
            return AddBoxInvokeShape.Float6DirectionFaceSet;
        }

        if (mergedInvokeLine.Contains("Ljava/lang/String;FFFIII", StringComparison.Ordinal))
        {
            return mergedInvokeLine.Contains("CubeDeformation", StringComparison.Ordinal)
                ? AddBoxInvokeShape.StringQuadThreeFloatThreeIntsCubeDefTexCropInts
                : AddBoxInvokeShape.StringQuadThreeFloatThreeIntsTexCropIntsNoDef;
        }

        if (mergedInvokeLine.Contains("Ljava/lang/String;FFFFFF", StringComparison.Ordinal))
        {
            return AddBoxInvokeShape.Float6StringQuadKey;
        }

        if (mergedInvokeLine.Contains("CubeDeformation;FF)", StringComparison.Ordinal) ||
            mergedInvokeLine.Contains("CubeDeformation;FF)L", StringComparison.Ordinal))
        {
            return AddBoxInvokeShape.Float6CubeDeformationTailFloats;
        }

        if (Regex.IsMatch(mergedInvokeLine,
                @"addBox:\(FFFFFFL[\w$]+;\)", RegexOptions.CultureInvariant))
        {
            return AddBoxInvokeShape.Float6;
        }

        if (mergedInvokeLine.Contains("(FFFFFF)", StringComparison.Ordinal))
        {
            return AddBoxInvokeShape.Float6;
        }

        if (ObfFluentAddBoxRegex.IsMatch(mergedInvokeLine))
        {
            return AddBoxInvokeShape.Float6;
        }

        if (ObfFluentDirectionMaskAddBoxRegex.IsMatch(mergedInvokeLine))
        {
            return mergedInvokeLine.Contains("Ljava/lang/String;FFFFFF", StringComparison.Ordinal)
                ? AddBoxInvokeShape.Float6StringQuadKeyDirectionFaceSet
                : AddBoxInvokeShape.Float6DirectionFaceSet;
        }

        if (ObfFluentTexCropAddBoxRegex.IsMatch(mergedInvokeLine))
        {
            return mergedInvokeLine.Contains("CubeDeformation", StringComparison.Ordinal)
                ? AddBoxInvokeShape.StringQuadThreeFloatThreeIntsCubeDefTexCropInts
                : AddBoxInvokeShape.StringQuadThreeFloatThreeIntsTexCropIntsNoDef;
        }

        return AddBoxInvokeShape.Unknown;
    }

    public static bool IsNamedOrObfuscatedTexTwoIntsLine(string line)
    {
        if (line.Contains("texOffs:(II)", StringComparison.Ordinal))
        {
            return true;
        }

        var m = ObfFluentTexOffsRegex.Match(line);
        return m.Success && string.Equals(m.Groups[1].Value, m.Groups[2].Value, StringComparison.Ordinal);
    }

    /// <summary>Named <c>CubeListBuilder.mirror()</c> or obfuscated fluent <c>…mirror:()L…;</c> before <c>addBox</c> (humanoid arms/legs, etc.).</summary>
    public static bool IsNamedOrObfuscatedMirrorNoArgFluentLine(string line)
    {
        if (line.Contains("CubeListBuilder.mirror:()", StringComparison.Ordinal) ||
            line.Contains("CubeListBuilder/mirror:()", StringComparison.Ordinal))
        {
            return true;
        }

        return line.Contains("invokevirtual", StringComparison.Ordinal) &&
               Regex.IsMatch(line, @"//\s*Method\s+[\w$/]+\.mirror:\(\)L[\w$/]+;", RegexOptions.CultureInvariant);
    }

    /// <summary>Named <c>CubeListBuilder.mirror(boolean)</c> or obfuscated <c>mirror:(Z)</c> overload.</summary>
    public static bool IsNamedOrObfuscatedMirrorBooleanFluentLine(string line) =>
        line.Contains("CubeListBuilder.mirror:(Z)", StringComparison.Ordinal) ||
        line.Contains("CubeListBuilder/mirror:(Z)", StringComparison.Ordinal) ||
        (line.Contains("invokevirtual", StringComparison.Ordinal) &&
         Regex.IsMatch(line, @"//\s*Method\s+[\w$/]+\.mirror:\(Z\)L[\w$/]+;", RegexOptions.CultureInvariant));

    public static bool TryInferBindingTypesFromLine(string line, out string builderShort, out string poseShort)
    {
        builderShort = string.Empty;
        poseShort = string.Empty;
        if (line.Contains("PartDefinition.addOrReplaceChild", StringComparison.Ordinal) &&
            line.Contains("Ljava/lang/String;", StringComparison.Ordinal))
        {
            var needle = "PartDefinition.addOrReplaceChild:(Ljava/lang/String;L";
            var idx = line.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0)
            {
                return false;
            }

            var start = idx + needle.Length;
            var bEnd = line.IndexOf(';', start);
            if (bEnd < 0)
            {
                return false;
            }

            builderShort = line[start..bEnd];
            var p = bEnd + 1;
            if (p >= line.Length || line[p] != 'L')
            {
                return false;
            }

            p++;
            var pEnd = line.IndexOf(';', p);
            if (pEnd < 0)
            {
                return false;
            }

            poseShort = line[p..pEnd];
            return poseShort.Length > 0;
        }

        var m = ObfPartDefinitionAddChildRegex.Match(line);
        if (!m.Success)
        {
            return false;
        }

        builderShort = m.Groups[3].Value;
        poseShort = m.Groups[4].Value;
        return builderShort.Length > 0 && poseShort.Length > 0;
    }

    private static readonly Regex InvokeStaticVoidMethodCommentRegex = new(
        @"invokestatic\s+#\d+\s+//\s*Method\s+(?:([\w\./]+)\.)?([\w$]+):(\([^)]*\))V;?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>Parses <c>invokestatic … // Method [owner.]method:(args)Lret;</c> from a <c>createBodyLayer</c> code slice.</summary>
    public static IEnumerable<InvokeStaticMeshRef> EnumerateInvokeStaticMeshRefs(string layerCodeBlock)
    {
        foreach (Match m in InvokeStaticMethodCommentRegex.Matches(layerCodeBlock))
        {
            var owner = m.Groups[1].Value;
            var method = m.Groups[2].Value;
            var args = m.Groups[3].Value;
            if (method.Length == 0)
            {
                continue;
            }

            yield return new InvokeStaticMeshRef(
                string.IsNullOrEmpty(owner) ? null : owner.Replace('/', '.'),
                method,
                args.Trim('(', ')'));
        }
    }

    /// <summary>
    /// Static void helpers (e.g. <c>QuadrupedModel.createLegs</c>) invoked from mesh factories; cuboids live in these methods.
    /// </summary>
    public static IEnumerable<InvokeStaticMeshRef> EnumerateInvokeStaticVoidMeshHelperRefs(string meshFactoryBytecode)
    {
        foreach (Match m in InvokeStaticVoidMethodCommentRegex.Matches(meshFactoryBytecode))
        {
            var owner = m.Groups[1].Value;
            var method = m.Groups[2].Value;
            var args = m.Groups[3].Value;
            if (method.Length == 0 || !IsVoidMeshHelperMethodName(method))
            {
                continue;
            }

            yield return new InvokeStaticMeshRef(
                string.IsNullOrEmpty(owner) ? null : owner.Replace('/', '.'),
                method,
                args.Trim('(', ')'));
        }
    }

    internal static bool IsVoidMeshHelperMethodName(string method) =>
        method is "createLegs" or "addHead" or "addHat" or "addBody" or "addArms" or "addBoots" or "addCloak" or
        "addLeftArm" or "addRightArm" or "addLeftLeg" or "addRightLeg" or "addHorn" or "addAntlers" ||
        (method.StartsWith("add", StringComparison.Ordinal) && method.Length > 3) ||
        (method.StartsWith("create", StringComparison.Ordinal) &&
         !method.EndsWith("Layer", StringComparison.Ordinal) &&
         !string.Equals(method, "createBodyLayer", StringComparison.Ordinal) &&
         !string.Equals(method, "createMesh", StringComparison.Ordinal));

    internal readonly record struct InvokeStaticMeshRef(string? OwnerJarSimple, string Method, string ArgsInner);
}
