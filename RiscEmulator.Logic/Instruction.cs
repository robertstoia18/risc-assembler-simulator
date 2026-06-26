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

    public bool IsLoad =>
        Op == Opcode.MOV &&
        (SourceMode == AddressingMode.AI || SourceMode == AddressingMode.AX);

    public bool IsStore =>
        Op == Opcode.MOV &&
        (DestMode == AddressingMode.AI || DestMode == AddressingMode.AX);

    public bool IsAlu =>
        Class == InstructionClass.Class1 && !IsLoad && !IsStore;

    public bool IsJump => Op == Opcode.JMP || Op == Opcode.CALL;

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
                    return $"MOV R{Rd},{src}";
                }
                if (IsStore)
                {
                    string dst = DestMode == AddressingMode.AI
                        ? $"(R{Rd})"
                        : $"{Immediate}(R{Rd})";
                    return $"MOV {dst},R{Rs1}";
                }
                if (InstructionSet.ReadsRs2(Op))
                    return $"{Op} R{Rd},R{Rs1},R{Rs2}";
                return $"{Op} R{Rd},R{Rs1}";

            case InstructionClass.Class2:
                return $"{Op} R{Rs1}";

            case InstructionClass.Class3:
                return $"{Op} {Offset:+0;-0;0}";

            default:
                return Op.ToString();
        }
    }
}
