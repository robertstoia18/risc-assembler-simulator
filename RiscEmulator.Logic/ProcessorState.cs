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

    public RegisterFile Registers { get; } = new RegisterFile();
 public Memory Memory { get; } = new Memory();

    public Cache ICache { get; }
    public Cache DCache { get; }

    public PipelineSlot[] Slots { get; } = new PipelineSlot[2];

    public FunctionalUnit AluUnit { get; } = new FunctionalUnit("ALU");
    public FunctionalUnit MulUnit { get; } = new FunctionalUnit("MUL");
    public FunctionalUnit LdStUnit { get; } = new FunctionalUnit("LD/ST");
    public FunctionalUnit JmpUnit { get; } = new FunctionalUnit("JMP");

    public FunctionalUnit[] FunctionalUnits { get; }

    public ProcessorState(
int iCacheNumSets = 16,
        int iCacheBlockSize = 4,
    int iCacheAssociativity = 2,
        int dCacheNumSets = 16,
      int dCacheBlockSize = 4,
        int dCacheAssociativity = 2,
        ReplacementPolicy iCacheReplacementPolicy = ReplacementPolicy.LruExact,
        ReplacementPolicy dCacheReplacementPolicy = ReplacementPolicy.LruExact)
    {
  ICache = new Cache(iCacheNumSets, iCacheBlockSize, iCacheAssociativity, iCacheReplacementPolicy);
        DCache = new Cache(dCacheNumSets, dCacheBlockSize, dCacheAssociativity, dCacheReplacementPolicy);

        for (int i = 0; i < 2; i++)
            Slots[i] = new PipelineSlot { Instruction = Instruction.MakeNop() };
   FunctionalUnits = new[] { AluUnit, MulUnit, LdStUnit, JmpUnit };
    }

    public PipelineSlot IF => Slots[(int)PipelineStage.IF];
    public PipelineSlot OF => Slots[(int)PipelineStage.OF];

    public PipelineSlot EX => AluUnit.ExSlot;
    public PipelineSlot MEM => AluUnit.MemSlot;
    public PipelineSlot WB => AluUnit.WbSlot;

    public void Reset()
    {
        PC = 0; MAR = 0; MDR = 0; IR = 0;
        A = 0; B = 0; C = 0;
        Registers.Reset();
        Memory.Reset();
        ICache.Reset();
        DCache.Reset();
        for (int i = 0; i < 2; i++)
            Slots[i] = new PipelineSlot { Instruction = Instruction.MakeNop() };
        foreach (var unit in FunctionalUnits)
            unit.Reset();
    }
}
