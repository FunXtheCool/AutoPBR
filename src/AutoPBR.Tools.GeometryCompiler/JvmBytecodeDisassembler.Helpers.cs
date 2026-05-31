using System.Buffers.Binary;
using System.Globalization;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JvmBytecodeDisassembler
{
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
