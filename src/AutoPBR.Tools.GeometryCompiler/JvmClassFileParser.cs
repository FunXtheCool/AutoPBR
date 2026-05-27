using System.Security.Cryptography;
using System.Text;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Minimal JVM classfile reader: constant pool UTF8 lookup and raw bytecode of a named method (for SHA256 drift).
/// Float probes use <see cref="JavapMethodFloatProbe"/> (javap -c) to avoid a full bytecode decoder.
/// </summary>
internal static class JvmClassFileParser
{
    public sealed record MethodCode(string Name, string Descriptor, byte[] Code);

    public static bool TryReadConstantPool(ReadOnlySpan<byte> classFile, out JvmConstantPool pool) =>
        JvmConstantPool.TryRead(classFile, out pool);

    public static MethodCode? TryGetMethodCode(ReadOnlySpan<byte> classFile, string methodName)
    {
        var r = new SpanReader(classFile);
        if (r.ReadU4() != 0xCAFEBABE)
        {
            return null;
        }

        _ = r.ReadU2(); // minor
        _ = r.ReadU2(); // major
        var cpCount = r.ReadU2();
        var pool = ConstantPool.Read(ref r, cpCount);
        _ = r.ReadU2(); // access
        _ = r.ReadU2(); // this_class
        _ = r.ReadU2(); // super
        var ifaces = r.ReadU2();
        r.Skip(ifaces * 2);
        SkipFields(ref r);
        var methods = r.ReadU2();
        for (var mi = 0; mi < methods; mi++)
        {
            _ = r.ReadU2(); // access
            var nameIdx = r.ReadU2();
            var descIdx = r.ReadU2();
            var name = pool.GetUtf8(nameIdx);
            var desc = pool.GetUtf8(descIdx);
            var attrCount = r.ReadU2();
            byte[]? code = null;
            for (var ai = 0; ai < attrCount; ai++)
            {
                var an = r.ReadU2();
                var alen = (int)r.ReadU4();
                var attrName = pool.GetUtf8(an);
                var payload = r.ReadBytes(alen);
                if (string.Equals(attrName, "Code", StringComparison.Ordinal))
                {
                    var cr = new SpanReader(payload);
                    _ = cr.ReadU2(); // max_stack
                    _ = cr.ReadU2(); // max_locals
                    var codeLength = (int)cr.ReadU4();
                    code = cr.ReadBytes(codeLength).ToArray();
                }
            }

            if (code is not null && string.Equals(name, methodName, StringComparison.Ordinal))
            {
                return new MethodCode(name, desc, code);
            }
        }

        return null;
    }

    public static string ComputeSha256Hex(ReadOnlySpan<byte> classFile)
    {
        var hash = SHA256.HashData(classFile);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool TryGetSuperClassName(ReadOnlySpan<byte> classFile, out string superClassName)
    {
        superClassName = string.Empty;
        if (!JvmConstantPool.TryRead(classFile, out var pool))
        {
            return false;
        }

        var r = new SpanReader(classFile);
        if (r.ReadU4() != 0xCAFEBABE)
        {
            return false;
        }

        _ = r.ReadU2();
        _ = r.ReadU2();
        var cpCount = r.ReadU2();
        SkipConstantPool(ref r, cpCount);
        _ = r.ReadU2();
        _ = r.ReadU2();
        var superIdx = r.ReadU2();
        superClassName = pool.GetClassName(superIdx);
        return !string.IsNullOrEmpty(superClassName);
    }

    public const ushort AccInterface = 0x0200;

    public static bool TryGetClassAccessFlags(ReadOnlySpan<byte> classFile, out ushort accessFlags)
    {
        accessFlags = 0;
        var r = new SpanReader(classFile);
        if (r.ReadU4() != 0xCAFEBABE)
        {
            return false;
        }

        _ = r.ReadU2();
        _ = r.ReadU2();
        var cpCount = r.ReadU2();
        _ = ConstantPool.Read(ref r, cpCount);
        accessFlags = r.ReadU2();
        return true;
    }

    public static bool IsInterface(ReadOnlySpan<byte> classFile) =>
        TryGetClassAccessFlags(classFile, out var access) && (access & AccInterface) != 0;

    public static bool HasStaticMeshFactoryMethod(ReadOnlySpan<byte> classFile, MojangMappingsParser? maps = null)
    {
        foreach (var (_, desc, isStatic) in EnumerateMethods(classFile))
        {
            if (isStatic && IsMeshFactoryDescriptor(desc, maps))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Extracts the return-type jar simple name from a method descriptor (e.g. <c>(Lgzk;)Lgzl;</c> → <c>gzl</c>).</summary>
    public static bool TryGetMethodReturnTypeJarSimple(string methodDescriptor, out string jarSimple)
    {
        jarSimple = string.Empty;
        var close = methodDescriptor.LastIndexOf(')');
        if (close < 0 || close >= methodDescriptor.Length - 1)
        {
            return false;
        }

        var ret = methodDescriptor[(close + 1)..];
        if (ret.Length <= 1 || ret[0] != 'L' || ret[^1] != ';')
        {
            return false;
        }

        var internalName = ret[1..^1];
        var slash = internalName.LastIndexOf('/');
        jarSimple = slash >= 0 ? internalName[(slash + 1)..] : internalName;
        return jarSimple.Length > 0;
    }

    /// <summary>Detects mesh/layer factory methods on deobfuscated or ProGuard descriptors.</summary>
    public static bool IsMeshFactoryDescriptor(string descriptor, MojangMappingsParser? maps = null)
    {
        if (descriptor.Contains("MeshDefinition", StringComparison.Ordinal) &&
            !descriptor.Contains("ArmorModelSet", StringComparison.Ordinal))
        {
            return true;
        }

        if (descriptor.Contains("LayerDefinition", StringComparison.Ordinal))
        {
            return true;
        }

        if (maps is null || !TryGetMethodReturnTypeJarSimple(descriptor, out var retShort))
        {
            return false;
        }

        if (maps.TryIsObfuscatedReturnType(retShort, "MeshDefinition"))
        {
            return true;
        }

        return maps.TryIsObfuscatedReturnType(retShort, "LayerDefinition");
    }

    public static IReadOnlyList<(string Name, string Descriptor, bool IsStatic)> EnumerateMethods(ReadOnlySpan<byte> classFile)
    {
        var list = new List<(string, string, bool)>();
        var r = new SpanReader(classFile);
        if (r.ReadU4() != 0xCAFEBABE)
        {
            return list;
        }

        _ = r.ReadU2();
        _ = r.ReadU2();
        var cpCount = r.ReadU2();
        var pool = ConstantPool.Read(ref r, cpCount);
        _ = r.ReadU2();
        _ = r.ReadU2();
        _ = r.ReadU2();
        var ifaces = r.ReadU2();
        r.Skip(ifaces * 2);
        SkipFields(ref r);
        var methods = r.ReadU2();
        for (var mi = 0; mi < methods; mi++)
        {
            var access = r.ReadU2();
            var nameIdx = r.ReadU2();
            var descIdx = r.ReadU2();
            var name = pool.GetUtf8(nameIdx);
            var desc = pool.GetUtf8(descIdx);
            var attrCount = r.ReadU2();
            for (var ai = 0; ai < attrCount; ai++)
            {
                _ = r.ReadU2();
                var alen = (int)r.ReadU4();
                r.Skip(alen);
            }

            list.Add((name, desc, (access & 0x0008) != 0));
        }

        return list;
    }

    private static void SkipConstantPool(ref SpanReader r, ushort cpCount)
    {
        for (var i = 1; i < cpCount; i++)
        {
            var tag = r.ReadU1();
            switch (tag)
            {
                case 1:
                    r.Skip(r.ReadU2());
                    break;
                case 3:
                case 4:
                    r.Skip(4);
                    break;
                case 5:
                case 6:
                    r.Skip(8);
                    i++;
                    break;
                case 7:
                case 8:
                case 16:
                case 19:
                case 20:
                    r.Skip(2);
                    break;
                case 9:
                case 10:
                case 11:
                case 12:
                case 17:
                case 18:
                    r.Skip(4);
                    break;
                case 15:
                    r.Skip(3);
                    break;
                default:
                    throw new InvalidDataException($"Unknown constant pool tag {tag} at index {i}");
            }
        }
    }

    private static void SkipFields(ref SpanReader r)
    {
        var n = r.ReadU2();
        for (var i = 0; i < n; i++)
        {
            _ = r.ReadU2();
            _ = r.ReadU2();
            _ = r.ReadU2();
            var ac = r.ReadU2();
            for (var a = 0; a < ac; a++)
            {
                _ = r.ReadU2();
                var alen = (int)r.ReadU4();
                r.Skip(alen);
            }
        }
    }

    private sealed class ConstantPool
    {
        private readonly string?[] _utf8;

        private ConstantPool(string?[] utf8) => _utf8 = utf8;

        public static ConstantPool Read(ref SpanReader r, ushort cpCount)
        {
            var utf8 = new string?[cpCount];
            for (var i = 1; i < cpCount; i++)
            {
                var tag = r.ReadU1();
                switch (tag)
                {
                    case 1: // Utf8
                        {
                            var len = r.ReadU2();
                            utf8[i] = Encoding.UTF8.GetString(r.ReadBytes(len));
                            break;
                        }
                    case 3: // Integer
                        r.Skip(4);
                        break;
                    case 4: // Float
                        r.Skip(4);
                        break;
                    case 5: // Long
                        r.Skip(8);
                        i++;
                        break;
                    case 6: // Double
                        r.Skip(8);
                        i++;
                        break;
                    case 7: // Class
                    case 8: // String
                        r.Skip(2);
                        break;
                    case 9: // Fieldref
                    case 10: // Methodref
                    case 11: // InterfaceMethodref
                    case 12: // NameAndType
                    case 18: // InvokeDynamic
                        r.Skip(4);
                        break;
                    case 15: // MethodHandle
                        r.Skip(3);
                        break;
                    case 16: // MethodType
                        r.Skip(2);
                        break;
                    case 17: // Dynamic
                        r.Skip(4);
                        break;
                    case 19: // Module
                    case 20: // Package
                        r.Skip(2);
                        break;
                    default:
                        throw new InvalidDataException($"Unknown constant pool tag {tag} at index {i}");
                }
            }

            return new ConstantPool(utf8);
        }

        public string GetUtf8(ushort idx) =>
            idx == 0 || idx >= _utf8.Length ? string.Empty : _utf8[idx] ?? string.Empty;
    }
}
