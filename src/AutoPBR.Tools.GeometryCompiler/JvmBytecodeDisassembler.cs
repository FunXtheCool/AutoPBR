using System.Buffers.Binary;
using System.Globalization;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Disassembles JVM method <c>Code</c> attribute bytes into javap-like lines for <see cref="JavapFloatGeometryMeshLift"/>.
/// Uses the constant pool for exact float/int/string constants.
/// </summary>
internal static class JvmBytecodeDisassembler
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

    private static string DisassembleWide(JvmConstantPool _, ReadOnlySpan<byte> code, ref int pc, int startPc)
    {
        if (pc >= code.Length)
        {
            return "wide";
        }

        var wideOp = code[pc++];
        if (wideOp is 0x15 or 0x16 or 0x17 or 0x18 or 0x19)
        {
            var slot = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
            pc += 2;
            return FormatLoad(wideOp, (byte)Math.Min((int)slot, 255));
        }

        if (wideOp is >= 0x36 and <= 0x3A)
        {
            var slot = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
            pc += 2;
            return FormatStore(wideOp, (byte)Math.Min((int)slot, 255));
        }

        if (wideOp == 0x84)
        {
            var slot = BinaryPrimitives.ReadUInt16BigEndian(code.Slice(pc, 2));
            pc += 2;
            var delta = BinaryPrimitives.ReadInt16BigEndian(code.Slice(pc, 2));
            pc += 2;
            return $"iinc {slot}, {delta}";
        }

        if (wideOp == 0xA7)
        {
            var rel = BinaryPrimitives.ReadInt32BigEndian(code.Slice(pc, 4));
            pc += 4;
            return $"goto {startPc + rel}";
        }

        pc += 2;
        return $"wide {wideOp:X2}";
    }

    private static string FormatBranch(byte op, ReadOnlySpan<byte> code, ref int pc, int startPc)
    {
        var name = op switch
        {
            0x99 => "ifeq",
            0x9A => "ifne",
            0x9B => "iflt",
            0x9C => "ifge",
            0x9D => "ifgt",
            0x9E => "ifle",
            0x9F => "if_icmpeq",
            0xA0 => "if_icmpne",
            0xA1 => "if_icmplt",
            0xA2 => "if_icmpge",
            0xA3 => "if_icmpgt",
            0xA4 => "if_icmple",
            0xA5 => "if_acmpeq",
            0xA6 => "if_acmpne",
            0xA7 => "goto",
            0xA8 => "jsr",
            0xC6 => "ifnull",
            0xC7 => "ifnonnull",
            0xC8 => "goto_w",
            0xC9 => "jsr_w",
            _ => $"branch_0x{op:X2}"
        };

        int target;
        if (op is 0xC8 or 0xC9)
        {
            var rel = BinaryPrimitives.ReadInt32BigEndian(code.Slice(pc, 4));
            pc += 4;
            target = startPc + rel;
        }
        else
        {
            var rel = BinaryPrimitives.ReadInt16BigEndian(code.Slice(pc, 2));
            pc += 2;
            target = startPc + rel;
        }

        return $"{name} {target}";
    }

    private static void SkipTableSwitch(ReadOnlySpan<byte> code, ref int pc)
    {
        pc += (4 - (pc % 4)) % 4;
        if (pc + 12 > code.Length)
        {
            pc = code.Length;
            return;
        }

        pc += 4;
        var low = BinaryPrimitives.ReadInt32BigEndian(code.Slice(pc, 4));
        pc += 4;
        var high = BinaryPrimitives.ReadInt32BigEndian(code.Slice(pc, 4));
        pc += 4;
        var count = (long)high - low + 1;
        if (count is > 0 and < 4096)
        {
            pc += (int)(count * 4);
        }
    }

    private static void SkipLookupSwitch(ReadOnlySpan<byte> code, ref int pc)
    {
        pc += (4 - (pc % 4)) % 4;
        if (pc + 8 > code.Length)
        {
            pc = code.Length;
            return;
        }

        pc += 4;
        var npairs = BinaryPrimitives.ReadInt32BigEndian(code.Slice(pc, 4));
        pc += 4;
        if (npairs is > 0 and < 4096)
        {
            pc += npairs * 8;
        }
    }

    private static string UnknownOpcodeFallback(ref int pc, int codeLength, byte op)
    {
        pc = Math.Min(pc, codeLength);
        return $"unknown_opcode 0x{op:X2}";
    }

    private static string FormatLoad(byte op, byte embedded)
    {
        var slot = op switch
        {
            >= 0x1A and <= 0x1D => op - 0x1A,
            >= 0x1E and <= 0x21 => op - 0x1E,
            >= 0x22 and <= 0x25 => op - 0x22,
            >= 0x26 and <= 0x29 => op - 0x26,
            >= 0x2A and <= 0x2D => op - 0x2A,
            0x15 => embedded,
            0x16 => embedded,
            0x17 => embedded,
            0x18 => embedded,
            0x19 => embedded,
            _ => embedded
        };
        var kind = op switch
        {
            >= 0x15 and <= 0x19 => op switch
            {
                0x15 => "iload",
                0x16 => "lload",
                0x17 => "fload",
                0x18 => "dload",
                _ => "aload"
            },
            >= 0x1A and <= 0x2D => "iload",
            >= 0x2E and <= 0x35 => "aload",
            _ => "load"
        };
        if (op is >= 0x1A and <= 0x2D)
        {
            kind = op <= 0x1D ? "iload" : (op <= 0x21 ? "lload" : (op <= 0x25 ? "fload" : "dload"));
        }

        if (op is >= 0x2A and <= 0x2D)
        {
            return $"aload_{slot}";
        }

        if (op is >= 0x1A and <= 0x25)
        {
            return $"{kind}_{slot}";
        }

        return $"{kind} {slot}";
    }

    private static string FormatStore(byte op, byte embedded) =>
        op switch
        {
            0x36 => $"istore {embedded}",
            0x37 => $"lstore {embedded}",
            0x38 => $"fstore {embedded}",
            0x39 => $"dstore {embedded}",
            0x3A => $"astore {embedded}",
            >= 0x3B and <= 0x3E => $"istore_{op - 0x3B}",
            >= 0x3F and <= 0x42 => $"lstore_{op - 0x3F}",
            >= 0x43 and <= 0x46 => $"fstore_{op - 0x43}",
            >= 0x47 and <= 0x4A => $"dstore_{op - 0x47}",
            >= 0x4B and <= 0x4E => $"astore_{op - 0x4B}",
            _ => $"store {embedded}"
        };

    private static string FormatLdc(JvmConstantPool pool, int idx) =>
        pool.TryGetConstant(idx, out var c)
            ? c.Tag switch
            {
                JvmConstantKind.Float =>
                    $"ldc #{idx} // float {c.FloatValue.ToString("G9", CultureInfo.InvariantCulture)}f",
                JvmConstantKind.Int => $"ldc #{idx} // int {c.IntValue}",
                JvmConstantKind.String => $"ldc #{idx} // String {c.StringValue}",
                JvmConstantKind.Class => $"ldc #{idx} // class {pool.GetClassName(idx)}",
                _ => $"ldc #{idx}"
            }
            : $"ldc #{idx}";

    private static string FormatLdc2(JvmConstantPool pool, int idx) =>
        pool.TryGetConstant(idx, out var c) && c.Tag == JvmConstantKind.Float
            ? $"ldc2_w #{idx} // float {c.FloatValue.ToString("G9", CultureInfo.InvariantCulture)}f"
            : $"ldc2_w #{idx}";

    private static string FormatGetStatic(JvmConstantPool pool, int idx)
    {
        if (pool.TryGetFieldRef(idx, out var fr))
        {
            return $"getstatic #{idx} // Field {fr.ClassName}.{fr.Name}:{fr.Descriptor}";
        }

        return $"getstatic #{idx}";
    }

    private static string FormatPutStatic(JvmConstantPool pool, int idx)
    {
        if (pool.TryGetFieldRef(idx, out var fr))
        {
            return $"putstatic #{idx} // Field {fr.ClassName}.{fr.Name}:{fr.Descriptor}";
        }

        return $"putstatic #{idx}";
    }

    private static string FormatInvoke(JvmConstantPool pool, int idx, string opcode)
    {
        if (pool.TryGetMethodRef(idx, out var mr))
        {
            return
                $"{opcode} #{idx} // Method {mr.ClassName}.{mr.Name}:{mr.Descriptor}";
        }

        return $"{opcode} #{idx}";
    }
}
