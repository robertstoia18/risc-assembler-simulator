namespace RiscEmulator.Logic;

public class PipelineController
{
    public ProcessorState State { get; }
    public int CycleCount { get; private set; }
    public bool Halted { get; private set; }

    public List<ForwardingEvent> LastForwardings { get; private set; } = new();
    public bool LastCycleHadStall { get; private set; }

    public PipelineSlot[] DisplaySlots { get; } = new PipelineSlot[5];

    private List<(int Address, Instruction Instr)> _program = new();
    private Dictionary<int, Instruction> _instrByAddress = new();
    private List<(int Address, int Word)> _programWords = new();

    public PipelineController(ProcessorState state)
    {
        State = state;
        for (int i = 0; i < 5; i++)
            DisplaySlots[i] = new PipelineSlot { Instruction = Instruction.MakeNop() };
    }

    public void LoadProgram(AssemblerResult result)
    {
        State.Reset();
        CycleCount = 0;
        Halted = false;
        _program = result.Instructions;
        _programWords = result.Words;
        _instrByAddress = _program.ToDictionary(p => p.Address, p => p.Instr);
        State.Memory.LoadProgram(result.Words);

        if (_program.Count > 0)
        {
            State.PC = _program[0].Address;

            State.IF.Instruction = _program[0].Instr;
            State.IF.A = 0;
            State.IF.B = 0;
            State.IF.C = 0;
            State.IF.IsStall = false;
            State.IF.BranchTaken = false;
            State.IF.BranchTarget = 0;

            State.IR = State.Memory.Read(State.PC);
            State.MDR = State.IR;
            State.MAR = State.PC;

            SaveDisplayState();
        }
    }

    public void Tick()
    {
        if (Halted) return;

        LastForwardings = new List<ForwardingEvent>();
        LastCycleHadStall = false;

        StageWB();
        StageMEM();
        StageEX();

        StageForwardingSetup();

        bool stall = StageOF_CheckHazard();
        LastCycleHadStall = stall;

        if (!stall)
            StageIF();

        SaveDisplayState();

        AdvancePipeline(stall);

        CycleCount++;
    }

    private void SaveDisplayState()
    {
        for (int i = 0; i < 5; i++)
        {
            var src = State.Slots[i];
            DisplaySlots[i] = new PipelineSlot
            {
                Instruction = src.Instruction,
                A = src.A,
                B = src.B,
                C = src.C,
                MAR = src.MAR,
                IsStall = src.IsStall,
                BranchTaken = src.BranchTaken,
                BranchTarget = src.BranchTarget
            };
        }
    }

    private void StageWB()
    {
        var slot = State.WB;
        var instr = slot.Instruction;
        if (instr == null || instr.IsNop) return;

        if (instr.IsHalt)
        {
            Halted = true;
            return;
        }

        if (InstructionSet.WritesRd(instr.Op) && !instr.IsStore)
        {
            State.Registers.Write(instr.Rd, slot.C);
        }

        if (instr.IsBranch && slot.BranchTaken)
        {
            State.PC = slot.BranchTarget;
        }
    }

    private void StageForwardingSetup()
    {
        _fwdFromEx = null;
        _fwdFromMem = null;

        var exInstr = State.EX.Instruction;
        if (exInstr != null && !exInstr.IsNop && !exInstr.IsHalt && !exInstr.IsLoad &&
            InstructionSet.WritesRd(exInstr.Op) && !exInstr.IsStore && exInstr.Rd != 0)
        {
            _fwdFromEx = (exInstr.Rd, State.EX.C);
        }

        var memInstr = State.MEM.Instruction;
        if (memInstr != null && !memInstr.IsNop && !memInstr.IsHalt &&
            InstructionSet.WritesRd(memInstr.Op) && !memInstr.IsStore && memInstr.Rd != 0)
        {
            _fwdFromMem = (memInstr.Rd, State.MEM.C);
        }
    }

    private (int Reg, int Val)? _fwdFromEx;
    private (int Reg, int Val)? _fwdFromMem;

    private bool StageOF_CheckHazard()
    {
        var slot = State.OF;
        var instr = slot.Instruction;
        if (instr == null || instr.IsNop || instr.IsHalt) return false;

        bool rs1Needed = InstructionSet.ReadsRs1(instr.Op);
        bool rs2Needed = InstructionSet.ReadsRs2(instr.Op) && instr.SourceMode != AddressingMode.AM;

        bool rs1Valid = !rs1Needed || State.Registers.IsValid(instr.Rs1);
        bool rs2Valid = !rs2Needed || State.Registers.IsValid(instr.Rs2);

        if (!rs1Valid || !rs2Valid)
        {
            bool rs1ForwardOk = rs1Needed && !State.Registers.IsValid(instr.Rs1) && CanForward(instr.Rs1);
            bool rs2ForwardOk = rs2Needed && !State.Registers.IsValid(instr.Rs2) && CanForward(instr.Rs2);

            bool needStall = (!rs1Valid && !rs1ForwardOk) || (!rs2Valid && !rs2ForwardOk);
            if (needStall)
                return true;
        }

        int a = rs1Needed ? ResolveOperand(instr.Rs1, instr) : 0;
        int b = rs2Needed ? ResolveOperand(instr.Rs2, instr) : 0;
        int c = 0;

        if (instr.IsStore)
        {
            a = ResolveOperand(instr.Rd, instr);
            c = ResolveOperand(instr.Rs1, instr);
        }

        slot.A = a;
        slot.B = b;
        slot.C = c;

        if (InstructionSet.WritesRd(instr.Op) && !instr.IsStore && instr.Rd != 0)
            State.Registers.Invalidate(instr.Rd);

        return false;
    }

    private bool CanForward(int reg)
    {
        if (reg == 0) return true;
        if (_fwdFromEx.HasValue && _fwdFromEx.Value.Reg == reg) return true;
        if (_fwdFromMem.HasValue && _fwdFromMem.Value.Reg == reg) return true;
        return false;
    }

    private int ResolveOperand(int reg, Instruction instr)
    {
        if (reg == 0) return 0;

        if (_fwdFromEx.HasValue && _fwdFromEx.Value.Reg == reg)
        {
            LastForwardings.Add(new ForwardingEvent
            {
                FromStage = (int)PipelineStage.EX,
                ToStage = (int)PipelineStage.OF,
                Register = reg,
                Value = _fwdFromEx.Value.Val
            });
            return _fwdFromEx.Value.Val;
        }

        if (_fwdFromMem.HasValue && _fwdFromMem.Value.Reg == reg)
        {
            LastForwardings.Add(new ForwardingEvent
            {
                FromStage = (int)PipelineStage.MEM,
                ToStage = (int)PipelineStage.OF,
                Register = reg,
                Value = _fwdFromMem.Value.Val
            });
            return _fwdFromMem.Value.Val;
        }

        return State.Registers.Read(reg);
    }

    private void StageMEM()
    {
        var slot = State.MEM;
        var instr = slot.Instruction;
        if (instr == null || instr.IsNop) return;

        if (instr.IsLoad)
        {
            slot.C = State.Memory.Read(slot.MAR);
            State.MDR = slot.C;
        }
        else if (instr.IsStore)
        {
            State.Memory.Write(slot.MAR, slot.C);
            State.MDR = slot.C;
        }
    }

    private void StageEX()
    {
        var slot = State.EX;
        var instr = slot.Instruction;
        if (instr == null || instr.IsNop) return;

        int a = slot.A;
        int b = slot.B;

        if (instr.IsLoad)
        {
            slot.MAR = a + instr.Immediate;
            State.MAR = slot.MAR;
            return;
        }

        if (instr.IsStore)
        {
            slot.MAR = a + instr.Immediate;
            State.MAR = slot.MAR;
            return;
        }

        if (instr.IsJump)
        {
            if (instr.SourceMode == AddressingMode.AM)
                State.PC = instr.Immediate;
            else if (instr.SourceMode == AddressingMode.AI)
                State.PC = slot.A;
            else if (instr.SourceMode == AddressingMode.AX)
                State.PC = slot.A + instr.Immediate;

            if (instr.Op == Opcode.JAL)
            {
                int returnAddr = instr.OriginAddress + InstructionSet.GetWordCount(instr);
                slot.C = returnAddr;
            }

            FlushIF();
            FlushOF();
            return;
        }

        if (instr.IsBranch)
        {
            bool condition = EvalBranchCondition(instr.Op, slot.A, slot.B);
            int target = instr.OriginAddress + InstructionSet.GetWordCount(instr) + instr.Offset;
            slot.BranchTaken = condition;
            slot.BranchTarget = target;

            if (condition)
            {
                State.PC = target;
                FlushIF();
                FlushOF();
            }
            return;
        }

        int result = AluOp(instr.Op, a, b, instr.Immediate, instr.SourceMode);
        slot.C = result;
        State.C = result;
    }

    private void StageIF()
    {
        if (Halted) return;

        int pc = State.PC;
        State.MAR = pc;

        if (!_instrByAddress.TryGetValue(pc, out var instr))
        {
            State.IF.Instruction = Instruction.MakeNop();
            State.IF.A = 0; State.IF.B = 0; State.IF.C = 0;
            State.IF.IsStall = false;
            return;
        }

        State.IR = State.Memory.Read(pc);
        State.MDR = State.IR;

        int wordsUsed = InstructionSet.GetWordCount(instr);
        State.PC = pc + wordsUsed;

        State.IF.Instruction = instr;
        State.IF.A = 0; State.IF.B = 0; State.IF.C = 0;
        State.IF.IsStall = false;
        State.IF.BranchTaken = false;
    }

    private void AdvancePipeline(bool stall)
    {
        if (stall)
        {
            State.Slots[(int)PipelineStage.WB] = State.Slots[(int)PipelineStage.MEM];
            State.Slots[(int)PipelineStage.MEM] = State.Slots[(int)PipelineStage.EX];

            var nopSlot = new PipelineSlot { Instruction = Instruction.MakeNop(), IsStall = true };
            State.Slots[(int)PipelineStage.EX] = nopSlot;
        }
        else
        {
            State.Slots[(int)PipelineStage.WB] = State.Slots[(int)PipelineStage.MEM];
            State.Slots[(int)PipelineStage.MEM] = State.Slots[(int)PipelineStage.EX];
            State.Slots[(int)PipelineStage.EX] = State.Slots[(int)PipelineStage.OF];
            State.Slots[(int)PipelineStage.OF] = State.Slots[(int)PipelineStage.IF];
            State.Slots[(int)PipelineStage.IF] = new PipelineSlot { Instruction = Instruction.MakeNop() };
        }
    }

    private void FlushIF()
    {
        State.Slots[(int)PipelineStage.IF] = new PipelineSlot
        {
            Instruction = Instruction.MakeNop(),
            IsStall = true
        };
    }

    private void FlushOF()
    {
        State.Slots[(int)PipelineStage.OF] = new PipelineSlot
        {
            Instruction = Instruction.MakeNop(),
            IsStall = true
        };
    }

    private int AluOp(Opcode op, int a, int b, int imm, AddressingMode srcMode)
    {
        int operandB = (srcMode == AddressingMode.AM) ? imm : b;
        switch (op)
        {
            case Opcode.ADD: return a + operandB;
            case Opcode.SUB: return a - operandB;
            case Opcode.AND: return a & operandB;
            case Opcode.OR: return a | operandB;
            case Opcode.XOR: return a ^ operandB;
            default: return 0;
        }
    }

    private bool EvalBranchCondition(Opcode op, int a, int b)
    {
        switch (op)
        {
            case Opcode.BEQ: return a == b;
            case Opcode.BNE: return a != b;
            case Opcode.BL: return a < b;
            case Opcode.BGE: return a >= b;
            default: return false;
        }
    }
}