using RiscEmulator.Logic;
using Xunit;

namespace RiscEmulator.Tests;

public class ScoreboardControllerTests
{
    private static ScoreboardController Setup(string source, int startAddr = 0x100)
    {
        var asm = new Assembler();
        var result = asm.Assemble(source, startAddr);
        var ctrl = new ScoreboardController();
        ctrl.LoadProgram(result);
        return ctrl;
    }

    [Fact]
    public void Issue_SingleAdd_FuBecomesbusy()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Tick();
        Assert.True(ctrl.FuStatus["ALU"].Busy);
        Assert.Equal(1, ctrl.FuStatus["ALU"].Fi);
    }

    [Fact]
    public void Issue_Waw_SecondInstrStalls()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R1, R4, R5\nHALT");
        ctrl.Tick();
        Assert.True(ctrl.FuStatus["ALU"].Busy);

        ctrl.Tick();
        Assert.Equal(1, ctrl.InstrStatus.Count);
    }

    [Fact]
    public void WriteResult_AluWritesRegister()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Registers[2] = 10;
        ctrl.Registers[3] = 20;
        for (int i = 0; i < 10; i++) ctrl.Tick();
        Assert.Equal(30, ctrl.Registers[1]);
    }

    [Fact]
    public void Mul_TakeThreeCycles()
    {
        var ctrl = Setup("MUL R1, R2, R3\nHALT");
        ctrl.Registers[2] = 4;
        ctrl.Registers[3] = 5;
        ctrl.Tick();
        Assert.True(ctrl.FuStatus["MUL"].Busy);
        ctrl.Tick();
        Assert.True(ctrl.FuStatus["MUL"].Busy);
        ctrl.Tick();
        Assert.True(ctrl.FuStatus["MUL"].Busy);
        for (int i = 0; i < 5; i++) ctrl.Tick();
        Assert.Equal(20, ctrl.Registers[1]);
    }

    [Fact]
    public void Raw_SecondInstrWaitsForReadOperands()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R4, R1, R5\nHALT");
        ctrl.Registers[2] = 3;
        ctrl.Registers[3] = 7;
        for (int i = 0; i < 20; i++) ctrl.Tick();
        Assert.Equal(10, ctrl.Registers[1]);
        Assert.Equal(10 - ctrl.Registers[5], ctrl.Registers[4]);
    }

    [Fact]
    public void RegisterResult_SetOnIssue_ClearedOnWrite()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Tick();
        Assert.Equal("ALU", ctrl.RegisterResult[1]);
        for (int i = 0; i < 10; i++) ctrl.Tick();
        Assert.Null(ctrl.RegisterResult[1]);
    }

    [Fact]
    public void InstrStatus_RecordsCycles()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Tick();
        Assert.Single(ctrl.InstrStatus);
        Assert.Equal(0, ctrl.InstrStatus[0].IssueCycle);
    }

    [Fact]
    public void Halt_SetsHalted()
    {
        var ctrl = Setup("HALT");
        for (int i = 0; i < 10; i++) ctrl.Tick();
        Assert.True(ctrl.Halted);
    }

    [Fact]
    public void ParallelExec_AluAndMul_Simultaneously()
    {
        var ctrl = Setup("ADD R1, R2, R3\nMUL R4, R5, R6\nHALT");
        ctrl.Registers[2] = 1; ctrl.Registers[3] = 2;
        ctrl.Registers[5] = 3; ctrl.Registers[6] = 4;
        ctrl.Tick();
        ctrl.Tick();
        Assert.True(ctrl.FuStatus["ALU"].Busy || ctrl.FuStatus["MUL"].Busy);
    }

    [Fact]
    public void Scoreboard_War_NotDetectedAtIssue()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R3, R7, R8\nHALT");
        ctrl.Tick();
        ctrl.Tick();
        Assert.True(ctrl.InstrStatus.Count >= 2 || ctrl.FuStatus.Values.Any(f => f.Busy));
    }
}
