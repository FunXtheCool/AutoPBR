using System.Buffers.Binary;
using System.Globalization;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Disassembles JVM method <c>Code</c> attribute bytes into javap-like lines for <see cref="JavapFloatGeometryMeshLift"/>.
/// Uses the constant pool for exact float/int/string constants.
/// </summary>
internal static partial class JvmBytecodeDisassembler
{
    public static bool TryDisassembleMethodToJavapLines(
        ReadOnlySpan<byte> classFile,
        string methodName,
        out List<string> lines)
    {
        lines = [];
        if (JvmClassFileParser.TryGetMethodCode(classFile, methodName) is not { } method)
        {
            return false;
        }

        if (!JvmClassFileParser.TryReadConstantPool(classFile, out var pool))
        {
            return false;
        }

        var pc = 0;
        var code = method.Code;
        while (pc < code.Length)
        {
            var startPc = pc;
            var op = code[pc++];
            var line = DisassembleOne(pool, code, ref pc, op, startPc);
            lines.Add($"{startPc,4}: {line}");
        }

        return lines.Count > 0;
    }

    private static string DisassembleOne(JvmConstantPool pool, ReadOnlySpan<byte> code, ref int pc, byte op, int startPc)
    {
        switch (op)
        {
            case 0x00: return "nop";
            case 0x01: return "aconst_null";
            case 0x02: return "iconst_m1";
            case 0x03: return "iconst_0";
            case 0x04: return "iconst_1";
            case 0x05: return "iconst_2";
            case 0x06: return "iconst_3";
            case 0x07: return "iconst_4";
            case 0x08: return "iconst_5";
            case 0x09: return "lconst_0";
            case 0x0A: return "lconst_1";
            case 0x0B: return "fconst_0";
            case 0x0C: return "fconst_1";
            case 0x0D: return "fconst_2";
            case 0x0E: return "dconst_0";
            case 0x0F: return "dconst_1";
            case 0x10:
                {
                    var v = (sbyte)code[pc++];
                    return $"bipush {v}";
                }
            case 0x11:
                {
                    var v = BinaryPrimitives.ReadInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return $"sipush {v}";
                }
            case 0x12:
                {
                    var idx = code[pc++];
                    return FormatLdc(pool, idx);
                }
            case 0x13:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return FormatLdc(pool, idx);
                }
            case 0x14:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return FormatLdc2(pool, idx);
                }
            case 0x15:
            case 0x16:
            case 0x17:
            case 0x18:
            case 0x19:
                return FormatLoad(op, code[pc++]);
            case 0x1A:
            case 0x1B:
            case 0x1C:
            case 0x1D:
            case 0x1E:
            case 0x1F:
            case 0x20:
            case 0x21:
            case 0x22:
            case 0x23:
            case 0x24:
            case 0x25:
            case 0x26:
            case 0x27:
            case 0x28:
            case 0x29:
            case 0x2A:
            case 0x2B:
            case 0x2C:
            case 0x2D:
                return FormatLoad(op, 0);
            case 0x36:
            case 0x37:
            case 0x38:
            case 0x39:
            case 0x3A:
                return FormatStore(op, code[pc++]);
            case 0x3B:
            case 0x3C:
            case 0x3D:
            case 0x3E:
            case 0x3F:
            case 0x40:
            case 0x41:
            case 0x42:
            case 0x43:
            case 0x44:
            case 0x45:
            case 0x46:
            case 0x47:
            case 0x48:
            case 0x49:
            case 0x4A:
            case 0x4B:
            case 0x4C:
            case 0x4D:
            case 0x4E:
                return FormatStore(op, 0);
            case 0x4F: return "iastore";
            case 0x50: return "lastore";
            case 0x51: return "fastore";
            case 0x52: return "dastore";
            case 0x53: return "aastore";
            case 0x54: return "bastore";
            case 0x55: return "castore";
            case 0x56: return "sastore";
            case 0xBC:
                {
                    var atype = code[pc++];
                    var typeName = atype switch
                    {
                        4 => "boolean",
                        5 => "char",
                        6 => "float",
                        7 => "double",
                        8 => "byte",
                        9 => "short",
                        10 => "int",
                        11 => "long",
                        _ => atype.ToString(CultureInfo.InvariantCulture)
                    };
                    return $"newarray {typeName}";
                }
            case 0x2E: return "iaload";
            case 0x2F: return "laload";
            case 0x30: return "faload";
            case 0x31: return "daload";
            case 0x32: return "aaload";
            case 0x33: return "baload";
            case 0x34: return "caload";
            case 0x35: return "saload";
            case 0x86: return "i2f";
            case 0x87: return "i2d";
            case 0x88: return "l2f";
            case 0x89: return "l2d";
            case 0x8A: return "f2i";
            case 0x8B: return "f2l";
            case 0x8C: return "f2d";
            case 0x8D: return "d2i";
            case 0x8E: return "d2l";
            case 0x8F: return "d2f";
            case 0x90: return "i2b";
            case 0x91: return "i2c";
            case 0x92: return "i2s";
            case 0x57: return "pop";
            case 0x58: return "pop2";
            case 0x59: return "dup";
            case 0x5A: return "dup_x1";
            case 0x5B: return "dup_x2";
            case 0x5C: return "dup2";
            case 0x5D: return "dup2_x1";
            case 0x5E: return "dup2_x2";
            case 0x5F: return "swap";
            case 0x60: return "iadd";
            case 0x61: return "ladd";
            case 0x62: return "fadd";
            case 0x63: return "dadd";
            case 0x64: return "isub";
            case 0x65: return "lsub";
            case 0x66: return "fsub";
            case 0x67: return "dsub";
            case 0x68: return "imul";
            case 0x69: return "lmul";
            case 0x6A: return "fmul";
            case 0x6B: return "dmul";
            case 0x6C: return "idiv";
            case 0x6D: return "ldiv";
            case 0x6E: return "fdiv";
            case 0x6F: return "ddiv";
            case 0x70: return "irem";
            case 0x71: return "lrem";
            case 0x72: return "frem";
            case 0x73: return "drem";
            case 0x74: return "ineg";
            case 0x75: return "lneg";
            case 0x76: return "fneg";
            case 0x77: return "dneg";
            case 0xB2:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return FormatGetStatic(pool, idx);
                }
            case 0xB3:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return FormatPutStatic(pool, idx);
                }
            case 0xB4:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return $"getfield #{idx}";
                }
            case 0xB5:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return $"putfield #{idx}";
                }
            case 0xB6:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return FormatInvoke(pool, idx, "invokevirtual");
                }
            case 0xB7:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return FormatInvoke(pool, idx, "invokespecial");
                }
            case 0xB8:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return FormatInvoke(pool, idx, "invokestatic");
                }
            case 0xB9:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 4;
                    return FormatInvoke(pool, idx, "invokeinterface");
                }
            case 0xBB:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return $"new #{idx} // class {pool.GetClassName(idx)}";
                }
            case 0xC0:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return $"checkcast #{idx} // class {pool.GetClassName(idx)}";
                }
            case 0xC1:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return $"instanceof #{idx}";
                }
            case 0xBD:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    return $"anewarray #{idx}";
                }
            case 0xBE: return "arraylength";
            case 0xBF: return "athrow";
            case 0xC5:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 2;
                    var dims = code[pc++];
                    return $"multianewarray #{idx}, {dims}";
                }
            case 0x84:
                {
                    var slot = code[pc++];
                    var delta = (sbyte)code[pc++];
                    return $"iinc {slot}, {delta}";
                }
            case 0xAC: return "ireturn";
            case 0xAD: return "lreturn";
            case 0xAE: return "freturn";
            case 0xAF: return "dreturn";
            case 0xB0: return "areturn";
            case 0xB1: return "return";
            case 0xBA:
                {
                    var idx = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
                    pc += 4;
                    return $"invokedynamic #{idx}";
                }
            case 0xC4:
                return DisassembleWide(pool, code, ref pc, startPc);
            case 0xC6:
            case 0xC7:
            case 0x99:
            case 0x9A:
            case 0x9B:
            case 0x9C:
            case 0x9D:
            case 0x9E:
            case 0x9F:
            case 0xA0:
            case 0xA1:
            case 0xA2:
            case 0xA3:
            case 0xA4:
            case 0xA5:
            case 0xA6:
            case 0xA7:
            case 0xA8:
            case 0xC8:
            case 0xC9:
                return FormatBranch(op, code, ref pc, startPc);
            case 0xAA:
                SkipTableSwitch(code, ref pc);
                return "tableswitch";
            case 0xAB:
                SkipLookupSwitch(code, ref pc);
                return "lookupswitch";
            default:
                return UnknownOpcodeFallback(ref pc, code.Length, op);
        }
    }
}
