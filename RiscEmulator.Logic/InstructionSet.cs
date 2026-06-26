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

public enum Opcode
{
    MOV, ADD, SUB, CMP, AND, OR, XOR,
    CLR, NEG, INC, DEC, ASL, ASR, LSR, ROL, ROR, RLC, RRC, PUSH, POP, JMP, CALL,
    BR, BNE, BEQ, BPL, BMI, BCS, BCC, BVS, BVC,
    CLC, CLV, CLZ, CLS, CCC, SEC, SEV, SEZ, SES, SCC,
    NOP, HALT, PUSHPC, POPPC, PUSHFLAG, POPFLAG, RET, RETI, WAIT
}

public static class InstructionSet
{
    public static InstructionClass GetClass(Opcode op)
    {
        switch (op)
        {
            case Opcode.MOV:
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.CMP:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
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
            case Opcode.CALL:
                return InstructionClass.Class2;

            case Opcode.BR:
            case Opcode.BNE:
            case Opcode.BEQ:
            case Opcode.BPL:
            case Opcode.BMI:
            case Opcode.BCS:
            case Opcode.BCC:
            case Opcode.BVS:
            case Opcode.BVC:
                return InstructionClass.Class3;

            default:
                return InstructionClass.Class4;
        }
    }

    public static bool WritesRd(Opcode op)
    {
        switch (op)
        {
            case Opcode.MOV:
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
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
                return true;
            default:
                return false;
        }
    }

    public static bool ReadsRs1(Opcode op)
    {
        switch (op)
        {
            case Opcode.MOV:
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.CMP:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
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
            case Opcode.CALL:
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
            case Opcode.CMP:
            case Opcode.AND:
            case Opcode.OR:
            case Opcode.XOR:
                return true;
            default:
                return false;
        }
    }

    public static int GetClass1Opcode(Opcode op)
    {
        switch (op)
        {
            case Opcode.MOV: return 0b0000;
            case Opcode.ADD: return 0b0001;
            case Opcode.SUB: return 0b0010;
            case Opcode.CMP: return 0b0011;
            case Opcode.AND: return 0b0100;
            case Opcode.OR:  return 0b0101;
            case Opcode.XOR: return 0b0110;
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
            case Opcode.CALL: return 0b0000001110;
            default: return -1;
        }
    }

    public static int GetClass3Opcode(Opcode op)
    {
        switch (op)
        {
            case Opcode.BR:  return 0b00000000;
            case Opcode.BNE: return 0b00000001;
            case Opcode.BEQ: return 0b00000010;
            case Opcode.BPL: return 0b00000011;
            case Opcode.BMI: return 0b00000100;
            case Opcode.BCS: return 0b00000101;
            case Opcode.BCC: return 0b00000110;
            case Opcode.BVS: return 0b00000111;
            case Opcode.BVC: return 0b00001000;
            default: return -1;
        }
    }
}
