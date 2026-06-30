namespace RiscEmulator.Logic;

public class FunctionalUnit
{
    public string Name { get; }
    public PipelineSlot ExSlot { get; set; }
    public PipelineSlot MemSlot { get; set; }
    public PipelineSlot WbSlot { get; set; }
    public int ExCyclesLeft { get; set; }

    public bool IsExBusy => ExSlot.Instruction is { IsNop: false };

    public FunctionalUnit(string name)
    {
        Name = name;
        ExSlot = new PipelineSlot { Instruction = Instruction.MakeNop() };
        MemSlot = new PipelineSlot { Instruction = Instruction.MakeNop() };
        WbSlot = new PipelineSlot { Instruction = Instruction.MakeNop() };
    }

    public void Reset()
    {
        ExSlot = new PipelineSlot { Instruction = Instruction.MakeNop() };
        MemSlot = new PipelineSlot { Instruction = Instruction.MakeNop() };
        WbSlot = new PipelineSlot { Instruction = Instruction.MakeNop() };
        ExCyclesLeft = 0;
    }
}
