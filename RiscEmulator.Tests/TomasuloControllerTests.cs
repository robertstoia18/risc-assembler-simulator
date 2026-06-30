using RiscEmulator.Logic;
using Xunit;

namespace RiscEmulator.Tests;

public class TomasuloControllerTests
{
    private static TomasuloController Setup(string source, int startAddr = 0x100)
    {
        var asm = new Assembler();
        var result = asm.Assemble(source, startAddr);
        var ctrl = new TomasuloController();
        ctrl.LoadProgram(result);
        return ctrl;
    }

    [Fact]
    public void Issue_SingleAdd_RSBusyAndQiSet()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Tick();
        var busyRs = ctrl.ReservationStations["ALU"].FirstOrDefault(r => r.Busy);
        Assert.NotNull(busyRs);
        Assert.Equal(Opcode.ADD, busyRs.Op);
        Assert.Equal("ALU.0", ctrl.RegisterFile[1].Qi);
    }

    [Fact]
    public void Execute_AddProducesResult()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.RegisterFile[2].Value = 5;
        ctrl.RegisterFile[3].Value = 7;
        for (int i = 0; i < 10; i++) ctrl.Tick();
        Assert.Equal(12, ctrl.RegisterFile[1].Value);
    }

    [Fact]
    public void Cdb_BroadcastsToWaitingRS()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R4, R1, R5\nHALT");
        ctrl.RegisterFile[2].Value = 3;
        ctrl.RegisterFile[3].Value = 4;
        ctrl.RegisterFile[5].Value = 1;
        for (int i = 0; i < 15; i++) ctrl.Tick();
        Assert.Equal(7, ctrl.RegisterFile[1].Value);
        Assert.Equal(6, ctrl.RegisterFile[4].Value);
    }

    [Fact]
    public void Issue_Waw_SecondInstrCapturesCorrectTag()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R1, R4, R5\nHALT");
        ctrl.Tick();
        ctrl.Tick();
        var tags = ctrl.ReservationStations.Values.SelectMany(rs => rs)
            .Where(rs => rs.Busy)
            .Select(rs => rs.Tag)
            .ToList();
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void Mul_ThreeCycles_CorrectResult()
    {
        var ctrl = Setup("MUL R1, R2, R3\nHALT");
        ctrl.RegisterFile[2].Value = 6;
        ctrl.RegisterFile[3].Value = 7;
        for (int i = 0; i < 15; i++) ctrl.Tick();
        Assert.Equal(42, ctrl.RegisterFile[1].Value);
    }

    [Fact]
    public void RegisterFile_QiClearedAfterWrite()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        for (int i = 0; i < 10; i++) ctrl.Tick();
        Assert.Null(ctrl.RegisterFile[1].Qi);
    }

    [Fact]
    public void CdbTag_ExposedForUI()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.RegisterFile[2].Value = 1;
        ctrl.RegisterFile[3].Value = 2;
        string? seenTag = null;
        for (int i = 0; i < 10; i++)
        {
            ctrl.Tick();
            if (ctrl.CdbTag != null) { seenTag = ctrl.CdbTag; break; }
        }
        Assert.NotNull(seenTag);
    }

    [Fact]
    public void Halt_SetsHalted()
    {
        var ctrl = Setup("HALT");
        for (int i = 0; i < 10; i++) ctrl.Tick();
        Assert.True(ctrl.Halted);
    }

    [Fact]
    public void RsSlots_ALUFour_MulTwo()
    {
        var ctrl = Setup("HALT");
        Assert.Equal(4, ctrl.ReservationStations["ALU"].Count);
        Assert.Equal(2, ctrl.ReservationStations["MUL"].Count);
    }

    [Fact]
    public void NoWar_SourcesCapturedAtIssue()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R2, R4, R5\nHALT");
        ctrl.RegisterFile[2].Value = 10;
        ctrl.RegisterFile[3].Value = 5;
        for (int i = 0; i < 20; i++) ctrl.Tick();
        Assert.Equal(15, ctrl.RegisterFile[1].Value);
    }
}
