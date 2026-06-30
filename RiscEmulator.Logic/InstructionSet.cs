namespace RiscEmulator.Logic;

public enum AddressingMode
{
    AM = 0,
    AD = 1,
    AI = 2,
    AX = 3
}

public enum InstructionClass
{
    Class1 = 1,
    Class2 = 2,
    Class3 = 3,
    Class4 = 4
}

public enum FunctionalUnitType { ALU, MUL, LDST, JMP }

public enum Opcode
{
    LD, ST,
    ADD, SUB, AND, OR, XOR, MUL,
    CLR, NEG, INC, DEC, ASL, ASR, LSR, ROL, ROR, RLC, RRC,
    PUSH, POP, JMP, JAL, RET, RETI,
    BNE, BEQ, BL, BGE,
    NOP, HALT, WAIT
}

public static class InstructionSet
{
    public static InstructionClass GetClass(Opcode op)
    {
        switch (op)
        {
            case Opcode.LD:
            case Opcode.ST:
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
            case Opcode.MUL:
                return InstructionClass.Class1;

            case Opcode.CLR:
            case Opcode.NEG:
            case Opcode.INC:
            case Opcode.DEC:
            case Opcode.ASL:
            case Opcode.ASR:
            case Opcode.LSR:
            case Opcode.ROL:
            case Opcode.ROR:
            case Opcode.RLC:
            case Opcode.RRC:
            case Opcode.PUSH:
            case Opcode.POP:
            case Opcode.JMP:
            case Opcode.JAL:
            case Opcode.RET:
            case Opcode.RETI:
                return InstructionClass.Class2;

            case Opcode.BNE:
            case Opcode.BEQ:
            case Opcode.BL:
            case Opcode.BGE:
                return InstructionClass.Class3;

            default:
                return InstructionClass.Class4;
        }
    }

    public static FunctionalUnitType GetUnitType(Opcode op)
    {
        return op switch
        {
            Opcode.MUL => FunctionalUnitType.MUL,
            Opcode.LD or Opcode.ST or Opcode.PUSH or Opcode.POP => FunctionalUnitType.LDST,
            Opcode.JMP or Opcode.JAL or Opcode.RET or Opcode.RETI or
            Opcode.BEQ or Opcode.BNE or Opcode.BL or Opcode.BGE => FunctionalUnitType.JMP,
            _ => FunctionalUnitType.ALU
        };
    }

    public static bool WritesRd(Opcode op)
    {
        switch (op)
        {
            case Opcode.LD:
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
            case Opcode.MUL:
            case Opcode.CLR:
            case Opcode.NEG:
            case Opcode.INC:
            case Opcode.DEC:
            case Opcode.ASL:
            case Opcode.ASR:
            case Opcode.LSR:
            case Opcode.ROL:
            case Opcode.ROR:
            case Opcode.RLC:
            case Opcode.RRC:
            case Opcode.POP:
            case Opcode.JAL:
                return true;
            default:
                return false;
        }
    }

    public static bool ReadsRs1(Opcode op)
    {
        switch (op)
        {
            case Opcode.LD:
            case Opcode.ST:
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
            case Opcode.MUL:
            case Opcode.NEG:
            case Opcode.INC:
            case Opcode.DEC:
            case Opcode.ASL:
            case Opcode.ASR:
            case Opcode.LSR:
            case Opcode.ROL:
            case Opcode.ROR:
            case Opcode.RLC:
            case Opcode.RRC:
            case Opcode.PUSH:
            case Opcode.JMP:
            case Opcode.JAL:
            case Opcode.BNE:
            case Opcode.BEQ:
            case Opcode.BL:
            case Opcode.BGE:
                return true;
            default:
                return false;
        }
    }

    public static bool ReadsRs2(Opcode op)
    {
        switch (op)
        {
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
            case Opcode.MUL:
            case Opcode.BNE:
            case Opcode.BEQ:
            case Opcode.BL:
            case Opcode.BGE:
                return true;
            default:
                return false;
        }
    }

    public static int GetWordCount(Instruction instr)
    {
        if (instr.Class == InstructionClass.Class3)
            return 2;
        if (instr.SourceMode == AddressingMode.AX || instr.DestMode == AddressingMode.AX)
            return 2;
        if (instr.Class == InstructionClass.Class2 &&
            (instr.SourceMode == AddressingMode.AM || instr.SourceMode == AddressingMode.AX))
            return 2;
        if (instr.Class == InstructionClass.Class1 && instr.SourceMode == AddressingMode.AM)
            return 2;
        return 1;
    }

    public static int GetClass1Opcode(Opcode op)
    {
        switch (op)
        {
            case Opcode.LD:  return 0b0000;
            case Opcode.ST:  return 0b0001;
            case Opcode.ADD: return 0b0010;
            case Opcode.SUB: return 0b0011;
            case Opcode.AND: return 0b0100;
            case Opcode.OR:  return 0b0101;
            case Opcode.XOR: return 0b0110;
            case Opcode.MUL: return 0b0111;
            default: return -1;
        }
    }

    public static int GetClass2Opcode(Opcode op)
    {
        switch (op)
        {
            case Opcode.CLR:  return 0b0000000000;
            case Opcode.NEG:  return 0b0000000001;
            case Opcode.INC:  return 0b0000000010;
            case Opcode.DEC:  return 0b0000000011;
            case Opcode.ASL:  return 0b0000000100;
            case Opcode.ASR:  return 0b0000000101;
            case Opcode.LSR:  return 0b0000000110;
            case Opcode.ROL:  return 0b0000000111;
            case Opcode.ROR:  return 0b0000001000;
            case Opcode.RLC:  return 0b0000001001;
            case Opcode.RRC:  return 0b0000001010;
            case Opcode.PUSH: return 0b0000001011;
            case Opcode.POP:  return 0b0000001100;
            case Opcode.JMP:  return 0b0000001101;
            case Opcode.JAL:  return 0b0000001110;
            case Opcode.RET:  return 0b0000001111;
            case Opcode.RETI: return 0b0000010000;
            default: return -1;
        }
    }

    public static int GetClass3Opcode(Opcode op)
    {
        switch (op)
        {
            case Opcode.BNE: return 0b00000001;
            case Opcode.BEQ: return 0b00000010;
            case Opcode.BL:  return 0b00000011;
            case Opcode.BGE: return 0b00000100;
            default: return -1;
        }
    }
}
