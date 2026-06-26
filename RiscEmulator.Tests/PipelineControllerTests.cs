using RiscEmulator.Logic;
using Xunit;

namespace RiscEmulator.Tests;

public class PipelineControllerTests
{
    private static (PipelineController ctrl, ProcessorState state) Setup(string source, int startAddr = 0x100)
    {
        var asm = new Assembler();
        var result = asm.Assemble(source, startAddr);
        var state = new ProcessorState();
        var ctrl = new PipelineController(state);
        ctrl.LoadProgram(result);
        return (ctrl, state);
    }

    [Fact]
    public void Section12_AddR9R8R7_TraversesAllFiveStages()
    {
        var (ctrl, state) = Setup("ADD R9,R8,R7", 0x100);

        state.Registers.Write(8, 10);
        state.Registers.Write(7, 5);

        Assert.Equal(0x100, state.PC);

        ctrl.Tick();
        Assert.Equal(Opcode.ADD, state.OF.Instruction!.Op);
        Assert.Equal(Opcode.NOP, state.EX.Instruction!.Op);
        Assert.Equal(Opcode.NOP, state.MEM.Instruction!.Op);
        Assert.Equal(Opcode.NOP, state.WB.Instruction!.Op);

        ctrl.Tick();
        Assert.Equal(Opcode.ADD, state.EX.Instruction!.Op);
        Assert.Equal(10, state.EX.A);
        Assert.Equal(5, state.EX.B);

        ctrl.Tick();
        Assert.Equal(Opcode.ADD, state.MEM.Instruction!.Op);
        Assert.Equal(15, state.MEM.C);

        ctrl.Tick();
        Assert.Equal(Opcode.ADD, state.WB.Instruction!.Op);

        ctrl.Tick();
        Assert.Equal(15, state.Registers.Read(9));
        Assert.True(state.Registers.IsValid(9));
    }

    [Fact]
    public void Section12_ValidityBit_ResetAtOF_SetAtWB()
    {
        var (ctrl, state) = Setup("ADD R9,R8,R7", 0x100);
        state.Registers.Write(8, 1);
        state.Registers.Write(7, 2);

        Assert.True(state.Registers.IsValid(9));

        ctrl.Tick();
        Assert.True(state.Registers.IsValid(9), "Tick 1: ADD in OF dar inca neprocesat");

        ctrl.Tick();
        Assert.False(state.Registers.IsValid(9), "Tick 2: OF proceseaza ADD, R9 trebuie invalidat");

        ctrl.Tick();
        ctrl.Tick();
        ctrl.Tick();
        Assert.True(state.Registers.IsValid(9), "Tick 5: WB scrie R9, bit revalidat");
        Assert.Equal(3, state.Registers.Read(9));
    }

    [Fact]
    public void HazardDetection_StallsOnLoadUseHazard()
    {
        string src = "LD R1,(R8)\nADD R4,R1,R5";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(8, 50);
        state.Memory.Write(50, 100);
        state.Registers.Write(5, 3);

        ctrl.Tick();
        Assert.True(state.Registers.IsValid(1));

        ctrl.Tick();
        Assert.False(state.Registers.IsValid(1), "R1 invalidat dupa OF pentru LOAD");

        ctrl.Tick();
        Assert.True(ctrl.LastCycleHadStall, "Load-use hazard trebuie sa produca stall");

        ctrl.Tick();
        Assert.False(ctrl.LastCycleHadStall, "Tick 4: forwarding din MEM elimina stallul");

        for (int i = 0; i < 8; i++) ctrl.Tick();
        Assert.Equal(103, state.Registers.Read(4));
    }

    [Fact]
    public void Forwarding_EliminatesStall()
    {
        string src = "ADD R1,R2,R3\nADD R4,R1,R5";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(2, 10);
        state.Registers.Write(3, 5);
        state.Registers.Write(5, 3);

        ctrl.Tick();
        ctrl.Tick();

        bool hadAnyForwarding = false;
        for (int i = 0; i < 6; i++)
        {
            ctrl.Tick();
            if (ctrl.LastForwardings.Count > 0)
                hadAnyForwarding = true;
        }

        Assert.True(hadAnyForwarding, "Forwarding-ul trebuie să fie activ cel puțin o dată");
        Assert.Equal(18, state.Registers.Read(4));
    }

    [Fact]
    public void LoadInstruction_AccessesMemory()
    {
        string src = "LD R1,(R8)";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(8, 10);
        state.Memory.Write(10, 42);

        for (int i = 0; i < 8; i++) ctrl.Tick();

        Assert.Equal(42, state.Registers.Read(1));
    }

    [Fact]
    public void StoreInstruction_WritesMemory()
    {
        string src = "ST (R8),R1";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(8, 20);
        state.Registers.Write(1, 99);

        for (int i = 0; i < 8; i++) ctrl.Tick();

        Assert.Equal(99, state.Memory.Read(20));
    }

    [Fact]
    public void JmpInstruction_UpdatesPC()
    {
        string src = "JMP 200h";
        var (ctrl, state) = Setup(src, 0);

        for (int i = 0; i < 5; i++) ctrl.Tick();

        Assert.Equal(0x200, state.PC);
    }

    [Fact]
    public void BranchNotTaken_ContinuesSequentially()
    {
        // BEQ R1,R2,loop: R1=1, R2=2 → nu sunt egale → branch nu se face → ADD R5 executa
        string src = "BEQ R1,R2,loop\nADD R5,R5,R5\nloop: NOP";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(1, 1);
        state.Registers.Write(2, 2);
        state.Registers.Write(5, 7);

        for (int i = 0; i < 20; i++) ctrl.Tick();

        Assert.Equal(14, state.Registers.Read(5));
    }

    [Fact]
    public void BranchTaken_JumpsToTarget()
    {
        // BEQ R1,R2,skip: R1=R2=5 → egale → branch luat → ADD nu executa
        string src = "BEQ R1,R2,skip\nADD R5,R5,R5\nskip: NOP";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(1, 5);
        state.Registers.Write(2, 5);
        state.Registers.Write(5, 7);

        for (int i = 0; i < 20; i++) ctrl.Tick();

        Assert.Equal(7, state.Registers.Read(5));
    }

    [Fact]
    public void HaltStopsExecution()
    {
        string src = "ADD R1,R2,R3\nHALT\nADD R1,R2,R3";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(2, 1);
        state.Registers.Write(3, 1);

        for (int i = 0; i < 20; i++) ctrl.Tick();

        Assert.True(ctrl.Halted);
        Assert.Equal(2, state.Registers.Read(1));
    }

    [Fact]
    public void R0AlwaysZero()
    {
        string src = "ADD R0,R1,R2";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(1, 5);
        state.Registers.Write(2, 3);

        for (int i = 0; i < 8; i++) ctrl.Tick();

        Assert.Equal(0, state.Registers.Read(0));
    }

    [Fact]
    public void AluImmediate_ComputesCorrectly()
    {
        string src = "ADD R1,R2,#10";
        var (ctrl, state) = Setup(src, 0);

        state.Registers.Write(2, 5);

        for (int i = 0; i < 8; i++) ctrl.Tick();

        Assert.Equal(15, state.Registers.Read(1));
    }

    [Fact]
    public void JalSavesReturnAddress()
    {
        // JAL 200h la addr 0 (2 cuvinte), return addr = 2
        string src = "JAL 200h";
        var (ctrl, state) = Setup(src, 0);

        for (int i = 0; i < 8; i++) ctrl.Tick();

        Assert.Equal(0x200, state.PC);
        Assert.Equal(2, state.Registers.Read(31));
    }
}
