namespace RiscEmulator.Logic;

public class PipelineSlot
{
    public Instruction? Instruction { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }
    public int MAR { get; set; }
    public bool IsStall { get; set; }
    public bool BranchTaken { get; set; }
    public int BranchTarget { get; set; }
}

public class ForwardingEvent
{
    public int FromStage { get; set; }
    public int ToStage { get; set; }
    public int Register { get; set; }
    public int Value { get; set; }
}

public class ProcessorState
{
    public int PC { get; set; }
    public int MAR { get; set; }
    public int MDR { get; set; }
    public int IR { get; set; }
    public int A { get; set; }
    public int B { get; set; }
    public int C { get; set; }

    public bool FlagZ { get; set; }
    public bool FlagC { get; set; }
    public bool FlagS { get; set; }
    public bool FlagO { get; set; }

    public RegisterFile Registers { get; } = new RegisterFile();
    public Memory Memory { get; } = new Memory();

    public PipelineSlot[] Slots { get; } = new PipelineSlot[5];

    public ProcessorState()
    {
        for (int i = 0; i < 5; i++)
            Slots[i] = new PipelineSlot { Instruction = Instruction.MakeNop() };
    }

    public PipelineSlot IF  => Slots[(int)PipelineStage.IF];
    public PipelineSlot OF  => Slots[(int)PipelineStage.OF];
    public PipelineSlot EX  => Slots[(int)PipelineStage.EX];
    public PipelineSlot MEM => Slots[(int)PipelineStage.MEM];
    public PipelineSlot WB  => Slots[(int)PipelineStage.WB];

    public void Reset()
    {
        PC = 0; MAR = 0; MDR = 0; IR = 0;
        A = 0; B = 0; C = 0;
        FlagZ = false; FlagC = false; FlagS = false; FlagO = false;
        Registers.Reset();
        Memory.Reset();
        for (int i = 0; i < 5; i++)
            Slots[i] = new PipelineSlot { Instruction = Instruction.MakeNop() };
    }
}
