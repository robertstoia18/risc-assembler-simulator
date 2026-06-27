namespace RiscEmulator.Logic;

public class Instruction
{
    public Opcode Op { get; set; }
    public InstructionClass Class { get; set; }
    public AddressingMode SourceMode { get; set; }
    public AddressingMode DestMode { get; set; }
    public int Rs1 { get; set; }
    public int Rs2 { get; set; }
    public int Rd { get; set; }
    public int Immediate { get; set; }
    public int Offset { get; set; }
    public int OriginAddress { get; set; }

    public bool IsLoad => Op == Opcode.LD;

    public bool IsStore => Op == Opcode.ST;

    public bool IsMul => Op == Opcode.MUL;

    public bool IsAlu =>
        Class == InstructionClass.Class1 && !IsLoad && !IsStore;

    public bool IsJump => Op == Opcode.JMP || Op == Opcode.JAL;

    public bool IsBranch =>
        Class == InstructionClass.Class3;

    public bool IsNop => Op == Opcode.NOP;

    public bool IsHalt => Op == Opcode.HALT;

    public static Instruction MakeNop()
    {
        return new Instruction
        {
            Op = Opcode.NOP,
            Class = InstructionClass.Class4,
            SourceMode = AddressingMode.AD,
            DestMode = AddressingMode.AD,
            Rs1 = 0, Rs2 = 0, Rd = 0
        };
    }

    public override string ToString()
    {
        if (IsNop) return "NOP";
        if (IsHalt) return "HALT";

        switch (Class)
        {
            case InstructionClass.Class1:
                if (IsLoad)
                {
                    string src = SourceMode == AddressingMode.AI
                        ? $"(R{Rs1})"
                        : $"{Immediate}(R{Rs1})";
                    return $"LD R{Rd},{src}";
                }
                if (IsStore)
                {
                    string dst = DestMode == AddressingMode.AI
                        ? $"(R{Rd})"
                        : $"{Immediate}(R{Rd})";
                    return $"ST {dst},R{Rs1}";
                }
                if (IsMul)
                    return $"MUL R{Rd},R{Rs1},R{Rs2}";
                if (SourceMode == AddressingMode.AM)
                    return $"{Op} R{Rd},R{Rs1},#{Immediate}";
                if (InstructionSet.ReadsRs2(Op))
                    return $"{Op} R{Rd},R{Rs1},R{Rs2}";
                return $"{Op} R{Rd},R{Rs1}";

            case InstructionClass.Class2:
                if (Op == Opcode.JAL)
                {
                    if (SourceMode == AddressingMode.AM)
                        return $"JAL 0x{Immediate:X}";
                    return $"JAL (R{Rs1})";
                }
                if (Op == Opcode.RET) return "RET";
                if (Op == Opcode.RETI) return "RETI";
                if (SourceMode == AddressingMode.AM)
                    return $"{Op} 0x{Immediate:X}";
                if (SourceMode == AddressingMode.AI)
                    return $"{Op} (R{Rs1})";
                return $"{Op} R{Rs1}";

            case InstructionClass.Class3:
                return $"{Op} R{Rs1},R{Rs2},{Offset:+0;-0;0}";

            default:
                return Op.ToString();
        }
    }
}
