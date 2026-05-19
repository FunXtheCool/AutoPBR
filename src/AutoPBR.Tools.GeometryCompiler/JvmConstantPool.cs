using System.Text;

namespace AutoPBR.Tools.GeometryCompiler;

internal sealed class JvmConstantPool
{
    private readonly ConstantEntry?[] _entries;

    private JvmConstantPool(ConstantEntry?[] entries) => _entries = entries;

    public static bool TryRead(ReadOnlySpan<byte> classFile, out JvmConstantPool pool)
    {
        pool = null!;
        var r = new SpanReader(classFile);
        if (r.ReadU4() != 0xCAFEBABE)
        {
            return false;
        }

        _ = r.ReadU2();
        _ = r.ReadU2();
        var cpCount = r.ReadU2();
        try
        {
            pool = new JvmConstantPool(ReadEntries(ref r, cpCount));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ConstantEntry?[] ReadEntries(ref SpanReader r, ushort cpCount)
    {
        var entries = new ConstantEntry?[cpCount];
        for (var i = 1; i < cpCount; i++)
        {
            var tag = r.ReadU1();
            entries[i] = tag switch
            {
                1 => new Utf8Entry(Encoding.UTF8.GetString(r.ReadBytes(r.ReadU2()))),
                3 => new IntEntry((int)r.ReadU4()),
                4 => new FloatEntry(ReadFloatBits(r.ReadU4())),
                5 => SkipWide(ref r, entries, ref i),
                6 => SkipWide(ref r, entries, ref i),
                7 => new ClassEntry(r.ReadU2()),
                8 => new StringEntry(r.ReadU2()),
                9 => new RefEntry(r.ReadU2(), r.ReadU2()),
                10 => new RefEntry(r.ReadU2(), r.ReadU2()),
                11 => new RefEntry(r.ReadU2(), r.ReadU2()),
                12 => new NameAndTypeEntry(r.ReadU2(), r.ReadU2()),
                15 => ReadMethodHandleEntry(ref r),
                16 => ReadMethodTypeEntry(ref r),
                17 => ReadDynamicEntry(ref r),
                18 => ReadInvokeDynamicEntry(ref r),
                19 => ReadModuleEntry(ref r),
                20 => ReadPackageEntry(ref r),
                _ => throw new InvalidDataException($"Unknown CP tag {tag}")
            };
        }

        return entries;
    }

    private static MethodHandleEntry ReadMethodHandleEntry(ref SpanReader r)
    {
        r.ReadU1();
        r.ReadU2();
        return new MethodHandleEntry();
    }

    private static MethodTypeEntry ReadMethodTypeEntry(ref SpanReader r)
    {
        r.ReadU2();
        return new MethodTypeEntry();
    }

    private static DynamicEntry ReadDynamicEntry(ref SpanReader r)
    {
        r.ReadU2();
        r.ReadU2();
        return new DynamicEntry();
    }

    private static InvokeDynamicEntry ReadInvokeDynamicEntry(ref SpanReader r)
    {
        r.ReadU2();
        r.ReadU2();
        return new InvokeDynamicEntry();
    }

    private static ModuleEntry ReadModuleEntry(ref SpanReader r)
    {
        r.ReadU2();
        return new ModuleEntry();
    }

    private static PackageEntry ReadPackageEntry(ref SpanReader r)
    {
        r.ReadU2();
        return new PackageEntry();
    }

    private static PaddingEntry SkipWide(ref SpanReader r, ConstantEntry?[] entries, ref int i)
    {
        r.Skip(8);
        entries[++i] = null;
        return new PaddingEntry();
    }

    private static float ReadFloatBits(uint bits) =>
        BitConverter.Int32BitsToSingle((int)bits);

    public bool TryGetConstant(int index, out JvmConstant constant)
    {
        constant = default;
        if (index <= 0 || index >= _entries.Length || _entries[index] is not { } e)
        {
            return false;
        }

        constant = e switch
        {
            FloatEntry f => JvmConstant.FromFloat(f.Value),
            IntEntry i => JvmConstant.FromInt(i.Value),
            Utf8Entry u => JvmConstant.FromString(u.Value),
            StringEntry s => TryResolveUtf8(s.Utf8Index, out var str)
                ? JvmConstant.FromString(str)
                : default,
            ClassEntry c => TryResolveUtf8(c.NameIndex, out var cn)
                ? JvmConstant.FromClass(cn.Replace('/', '.'))
                : default,
            _ => default
        };
        return constant.Tag != JvmConstantKind.None;
    }

    public string GetClassName(int classIndex)
    {
        if (classIndex <= 0 || classIndex >= _entries.Length || _entries[classIndex] is not ClassEntry c)
        {
            return string.Empty;
        }

        return TryResolveUtf8(c.NameIndex, out var n) ? n.Replace('/', '.') : string.Empty;
    }

    public bool TryGetMethodRef(int index, out JvmMethodRef methodRef)
    {
        methodRef = default;
        if (index <= 0 || index >= _entries.Length || _entries[index] is not RefEntry r)
        {
            return false;
        }

        if (_entries[r.NameAndTypeIndex] is not NameAndTypeEntry nt)
        {
            return false;
        }

        if (!TryResolveUtf8(nt.NameIndex, out var name) || !TryResolveUtf8(nt.DescriptorIndex, out var desc))
        {
            return false;
        }

        var className = GetClassName(r.ClassIndex);
        methodRef = new JvmMethodRef(className, name, desc);
        return true;
    }

    public bool TryGetFieldRef(int index, out JvmFieldRef fieldRef)
    {
        fieldRef = default;
        if (index <= 0 || index >= _entries.Length || _entries[index] is not RefEntry r)
        {
            return false;
        }

        if (_entries[r.NameAndTypeIndex] is not NameAndTypeEntry nt)
        {
            return false;
        }

        if (!TryResolveUtf8(nt.NameIndex, out var name) || !TryResolveUtf8(nt.DescriptorIndex, out var desc))
        {
            return false;
        }

        fieldRef = new JvmFieldRef(GetClassName(r.ClassIndex), name, desc);
        return true;
    }

    private bool TryResolveUtf8(int index, out string value)
    {
        value = string.Empty;
        if (index <= 0 || index >= _entries.Length || _entries[index] is not Utf8Entry u)
        {
            return false;
        }

        value = u.Value;
        return true;
    }

    private abstract class ConstantEntry;

    private sealed class Utf8Entry(string value) : ConstantEntry
    {
        public string Value { get; } = value;
    }

    private sealed class IntEntry(int value) : ConstantEntry
    {
        public int Value { get; } = value;
    }

    private sealed class FloatEntry(float value) : ConstantEntry
    {
        public float Value { get; } = value;
    }

    private sealed class PaddingEntry : ConstantEntry;

    private sealed class ClassEntry(ushort nameIndex) : ConstantEntry
    {
        public ushort NameIndex { get; } = nameIndex;
    }

    private sealed class StringEntry(ushort utf8Index) : ConstantEntry
    {
        public ushort Utf8Index { get; } = utf8Index;
    }

    private sealed class RefEntry(ushort classIndex, ushort nameAndTypeIndex) : ConstantEntry
    {
        public ushort ClassIndex { get; } = classIndex;
        public ushort NameAndTypeIndex { get; } = nameAndTypeIndex;
    }

    private sealed class NameAndTypeEntry(ushort nameIndex, ushort descriptorIndex) : ConstantEntry
    {
        public ushort NameIndex { get; } = nameIndex;
        public ushort DescriptorIndex { get; } = descriptorIndex;
    }

    private sealed class MethodHandleEntry : ConstantEntry;

    private sealed class MethodTypeEntry : ConstantEntry;

    private sealed class DynamicEntry : ConstantEntry;

    private sealed class InvokeDynamicEntry : ConstantEntry;

    private sealed class ModuleEntry : ConstantEntry;

    private sealed class PackageEntry : ConstantEntry;
}

internal enum JvmConstantKind
{
    None,
    Float,
    Int,
    String,
    Class
}

internal readonly struct JvmConstant
{
    public JvmConstantKind Tag { get; }
    public float FloatValue { get; }
    public int IntValue { get; }
    public string StringValue { get; }

    private JvmConstant(JvmConstantKind tag, float f = 0, int i = 0, string? s = null)
    {
        Tag = tag;
        FloatValue = f;
        IntValue = i;
        StringValue = s ?? string.Empty;
    }

    public static JvmConstant FromFloat(float v) => new(JvmConstantKind.Float, f: v);
    public static JvmConstant FromInt(int v) => new(JvmConstantKind.Int, i: v);
    public static JvmConstant FromString(string v) => new(JvmConstantKind.String, s: v);
    public static JvmConstant FromClass(string v) => new(JvmConstantKind.Class, s: v);
}

internal readonly record struct JvmMethodRef(string ClassName, string Name, string Descriptor);

internal readonly record struct JvmFieldRef(string ClassName, string Name, string Descriptor);
