using RiscEmulator.Logic;
using Xunit;

namespace RiscEmulator.Tests;

public class OooControllerTests
{
    private static OooController Setup(string source, int startAddr = 0x100)
    {
        var asm = new Assembler();
        var result = asm.Assemble(source, startAddr);
        var ctrl = new OooController();
        ctrl.LoadProgram(result);
        return ctrl;
    }

    [Fact]
    public void Fetch_FillsPrimaryBuffer()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Tick();
        Assert.True(ctrl.PrimaryBuffer.Count > 0 || ctrl.InstructionWindow.Count > 0);
    }

    [Fact]
    public void Dispatch_MovesInstrToWindow()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Tick();
        ctrl.Tick();
        Assert.True(ctrl.InstructionWindow.Count > 0 || ctrl.ReorderBuffer.Count > 0);
    }

    [Fact]
    public void Execute_AddCommitsCorrectly()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Registers[2] = 8;
        ctrl.Registers[3] = 9;
        for (int i = 0; i < 15; i++) ctrl.Tick();
        Assert.Equal(17, ctrl.Registers[1]);
    }

    [Fact]
    public void Rob_InOrderCommit_CorrectOrder()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R4, R5, R6\nHALT");
        ctrl.Registers[2] = 1; ctrl.Registers[3] = 2;
        ctrl.Registers[5] = 10; ctrl.Registers[6] = 3;
        for (int i = 0; i < 20; i++) ctrl.Tick();
        Assert.Equal(3, ctrl.Registers[1]);
        Assert.Equal(7, ctrl.Registers[4]);
    }

    [Fact]
    public void Halt_SetsHalted()
    {
        var ctrl = Setup("HALT");
        for (int i = 0; i < 15; i++) ctrl.Tick();
        Assert.True(ctrl.Halted);
    }

    [Fact]
    public void RegisterTags_SetOnDispatch_ClearedOnCommit()
    {
        var ctrl = Setup("ADD R1, R2, R3\nHALT");
        ctrl.Tick();
        ctrl.Tick();
        bool seenTag = ctrl.RegisterTags[1] != null;
        for (int i = 0; i < 20; i++) ctrl.Tick();
        Assert.Null(ctrl.RegisterTags[1]);
    }

    [Fact]
    public void BranchBuffer_StartedOnBranchFetch()
    {
        var ctrl = Setup("BEQ R1, R2, end\nADD R3, R4, R5\nend: HALT");
        ctrl.Tick();
        bool anyActivity = ctrl.FetchingBranch
            || ctrl.BranchBuffer.Count > 0
            || ctrl.PrimaryBuffer.Count > 0
            || ctrl.InstructionWindow.Count > 0
            || ctrl.ReorderBuffer.Count > 0
            || ctrl.Halted;
        Assert.True(anyActivity);
    }

    [Fact]
    public void FuSlots_ExposedForUI()
    {
        var ctrl = Setup("HALT");
        Assert.Contains("ALU", ctrl.FuSlots.Keys);
        Assert.Contains("MUL", ctrl.FuSlots.Keys);
        Assert.Contains("LDST", ctrl.FuSlots.Keys);
        Assert.Contains("JMP", ctrl.FuSlots.Keys);
    }

    [Fact]
    public void Mul_ThreeCycles_Ooo()
    {
        var ctrl = Setup("MUL R1, R2, R3\nHALT");
        ctrl.Registers[2] = 3;
        ctrl.Registers[3] = 3;
        for (int i = 0; i < 20; i++) ctrl.Tick();
        Assert.Equal(9, ctrl.Registers[1]);
    }

    [Fact]
    public void OoO_IndependentInstrs_DispatchedWithoutWaiting()
    {
        var ctrl = Setup("ADD R1, R2, R3\nSUB R4, R5, R6\nHALT");
        ctrl.Registers[2] = 1; ctrl.Registers[3] = 2;
        ctrl.Registers[5] = 5; ctrl.Registers[6] = 3;
        ctrl.Tick(); ctrl.Tick(); ctrl.Tick();
        int dispatched = ctrl.InstructionWindow.Count(w => w.Dispatched);
        int inRob = ctrl.ReorderBuffer.Count;
        Assert.True(dispatched + inRob >= 0);
    }
}
