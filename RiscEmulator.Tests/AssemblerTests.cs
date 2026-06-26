using RiscEmulator.Logic;
using Xunit;

namespace RiscEmulator.Tests;

public class AssemblerTests
{
    private readonly Assembler _asm = new();

    [Fact]
    public void NopProducesOneWord()
    {
        var result = _asm.Assemble("NOP", 0);
        Assert.Single(result.Words);
        Assert.Single(result.Instructions);
    }

    [Fact]
    public void AddR9R8R7EncodesCorrectly()
    {
        var result = _asm.Assemble("ADD R9,R8,R7", 0x100);
        Assert.Single(result.Instructions);
        var (addr, instr) = result.Instructions[0];
        Assert.Equal(0x100, addr);
        Assert.Equal(Opcode.ADD, instr.Op);
        Assert.Equal(9, instr.Rd);
        Assert.Equal(8, instr.Rs1);
        Assert.Equal(7, instr.Rs2);
        Assert.Equal(AddressingMode.AD, instr.SourceMode);
        Assert.Equal(AddressingMode.AD, instr.DestMode);
    }

    [Fact]
    public void MovLoadAI()
    {
        var result = _asm.Assemble("MOV R1,(R8)", 0);
        var (_, instr) = result.Instructions[0];
        Assert.True(instr.IsLoad);
        Assert.False(instr.IsStore);
        Assert.Equal(1, instr.Rd);
        Assert.Equal(8, instr.Rs1);
        Assert.Equal(AddressingMode.AI, instr.SourceMode);
    }

    [Fact]
    public void MovLoadAX()
    {
        var result = _asm.Assemble("MOV R1,16(R8)", 0);
        Assert.Equal(2, result.Words.Count);
        var (_, instr) = result.Instructions[0];
        Assert.True(instr.IsLoad);
        Assert.Equal(AddressingMode.AX, instr.SourceMode);
        Assert.Equal(16, instr.Immediate);
        Assert.Equal(8, instr.Rs1);
        Assert.Equal(1, instr.Rd);
    }

    [Fact]
    public void MovStoreAI()
    {
        var result = _asm.Assemble("MOV (R8),R1", 0);
        var (_, instr) = result.Instructions[0];
        Assert.True(instr.IsStore);
        Assert.False(instr.IsLoad);
        Assert.Equal(8, instr.Rd);
        Assert.Equal(1, instr.Rs1);
        Assert.Equal(AddressingMode.AI, instr.DestMode);
    }

    [Fact]
    public void JmpImmediate()
    {
        var result = _asm.Assemble("JMP 100h", 0);
        var (_, instr) = result.Instructions[0];
        Assert.Equal(Opcode.JMP, instr.Op);
        Assert.Equal(AddressingMode.AM, instr.SourceMode);
        Assert.Equal(0x100, instr.Immediate);
        Assert.Equal(2, result.Words.Count);
    }

    [Fact]
    public void LabelResolution()
    {
        string src = "L1:\nNOP\nNOP\nBEQ L1";
        var result = _asm.Assemble(src, 0);
        var branchInstr = result.Instructions.Last().Instr;
        Assert.Equal(Opcode.BEQ, branchInstr.Op);
        Assert.Equal(-3, branchInstr.Offset);
    }

    [Fact]
    public void LabelOnSameLine()
    {
        var result = _asm.Assemble("START: ADD R1,R2,R3", 0x10);
        Assert.Single(result.Instructions);
        Assert.Equal(0x10, result.Instructions[0].Address);
    }

    [Fact]
    public void BranchOffsetTooLargeThrows()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("L1:");
        for (int i = 0; i < 200; i++) sb.AppendLine("NOP");
        sb.AppendLine("BEQ L1");
        Assert.Throws<AssemblerException>(() => _asm.Assemble(sb.ToString(), 0));
    }

    [Fact]
    public void CmpEncodesCorrectly()
    {
        var result = _asm.Assemble("CMP R1,R2", 0);
        Assert.Single(result.Words);
        var (_, instr) = result.Instructions[0];
        Assert.Equal(Opcode.CMP, instr.Op);
        Assert.Equal(InstructionClass.Class1, instr.Class);
        Assert.Equal(1, instr.Rs1);
        Assert.Equal(2, instr.Rs2);
        Assert.Equal(0, instr.Rd);
    }

    [Fact]
    public void CommentIsStripped()
    {
        var result = _asm.Assemble("ADD R1,R2,R3 ; acest comentariu e ignorat", 0);
        Assert.Single(result.Instructions);
        Assert.Equal(Opcode.ADD, result.Instructions[0].Instr.Op);
    }

    [Fact]
    public void MultipleInstructions()
    {
        string src = "ADD R9,R8,R7\nSUB R1,R2,R3\nNOP";
        var result = _asm.Assemble(src, 0x100);
        Assert.Equal(3, result.Instructions.Count);
        Assert.Equal(0x100, result.Instructions[0].Address);
        Assert.Equal(0x101, result.Instructions[1].Address);
        Assert.Equal(0x102, result.Instructions[2].Address);
    }
}
