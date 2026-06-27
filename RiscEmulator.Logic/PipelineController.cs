namespace RiscEmulator.Logic;

public class PipelineController
{
    public ProcessorState State { get; }
    public int CycleCount { get; private set; }
    public bool Halted { get; private set; }

    public List<ForwardingEvent> LastForwardings { get; private set; } = new();
    public bool LastCycleHadStall { get; private set; }

    public PipelineSlot[] DisplaySlots { get; } = new PipelineSlot[2];
    public FunctionalUnit[] DisplayUnits { get; } = new FunctionalUnit[4];

    private List<(int Address, Instruction Instr)> _program = new();
    private Dictionary<int, Instruction> _instrByAddress = new();
    private List<(int Address, int Word)> _programWords = new();

    private FunctionalUnit? _dispatchTarget;
    private PipelineSlot? _dispatchSlot;
    private readonly List<(int Reg, int Val, int Stage)> _fwdSources = new();

    public PipelineController(ProcessorState state)
    {
        State = state;
        for (int i = 0; i < 2; i++)
            DisplaySlots[i] = new PipelineSlot { Instruction = Instruction.MakeNop() };
        for (int i = 0; i < 4; i++)
            DisplayUnits[i] = new FunctionalUnit(state.FunctionalUnits[i].Name);
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
            State.IF.A = 0; State.IF.B = 0; State.IF.C = 0;
            State.IF.IsStall = false;
            State.IF.BranchTaken = false;
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
        _dispatchTarget = null;
        _dispatchSlot = null;

        StageWB_AllUnits();
        StageMEM_LdSt();
        StageEX_AllUnits();
        StageForwardingSetup();

        bool stall = StageOF_CheckHazard();
        LastCycleHadStall = stall;

        if (!stall)
            StageIF();

        SaveDisplayState();
        AdvanceUnits(stall);
        CycleCount++;
    }

    private void SaveDisplayState()
    {
        for (int i = 0; i < 2; i++)
        {
            var src = State.Slots[i];
            DisplaySlots[i] = new PipelineSlot
            {
                Instruction = src.Instruction,
                A = src.A, B = src.B, C = src.C,
                MAR = src.MAR,
                IsStall = src.IsStall,
                BranchTaken = src.BranchTaken,
                BranchTarget = src.BranchTarget
            };
        }
        for (int i = 0; i < 4; i++)
        {
            var src = State.FunctionalUnits[i];
            var dst = DisplayUnits[i];
            dst.ExSlot = CloneSlot(src.ExSlot);
            dst.MemSlot = CloneSlot(src.MemSlot);
            dst.WbSlot = CloneSlot(src.WbSlot);
            dst.ExCyclesLeft = src.ExCyclesLeft;
        }
    }

    private static PipelineSlot CloneSlot(PipelineSlot s) => new PipelineSlot
    {
        Instruction = s.Instruction,
        A = s.A, B = s.B, C = s.C,
        MAR = s.MAR,
        IsStall = s.IsStall,
        BranchTaken = s.BranchTaken,
        BranchTarget = s.BranchTarget
    };

    private static PipelineSlot MakeNopSlot() =>
        new PipelineSlot { Instruction = Instruction.MakeNop() };

    private void StageWB_AllUnits()
    {
        foreach (var unit in State.FunctionalUnits)
        {
            var slot = unit.WbSlot;
            var instr = slot.Instruction;
            if (instr == null || instr.IsNop) continue;

            if (instr.IsHalt)
            {
                Halted = true;
                continue;
            }

            if (instr.IsBranch && slot.BranchTaken)
                State.PC = slot.BranchTarget;

            if (InstructionSet.WritesRd(instr.Op) && !instr.IsStore && instr.Rd != 0)
                State.Registers.Write(instr.Rd, slot.C);
        }
    }

    private void StageMEM_LdSt()
    {
        var slot = State.LdStUnit.MemSlot;
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
        else if (instr.Op == Opcode.POP)
        {
            slot.C = State.Memory.Read(slot.MAR);
            State.MDR = slot.C;
        }
        else if (instr.Op == Opcode.PUSH)
        {
            State.Memory.Write(slot.MAR, slot.C);
            State.MDR = slot.C;
        }
    }

    private void StageEX_AllUnits()
    {
        foreach (var unit in State.FunctionalUnits)
            StageEX_Unit(unit);
    }

    private void StageEX_Unit(FunctionalUnit unit)
    {
        var slot = unit.ExSlot;
        var instr = slot.Instruction;
        if (instr == null || instr.IsNop || instr.IsHalt) return;

        int a = slot.A;
        int b = slot.B;

        if (instr.IsMul)
        {
            slot.C = a * b;
            if (unit.ExCyclesLeft > 0) unit.ExCyclesLeft--;
            State.C = slot.C;
            return;
        }

        if (instr.IsLoad || instr.IsStore)
        {
            slot.MAR = a + instr.Immediate;
            State.MAR = slot.MAR;
            return;
        }

        if (instr.Op == Opcode.PUSH)
        {
            int sp = State.Registers.Read(30) - 1;
            State.Registers.Write(30, sp);
            slot.MAR = sp;
            State.MAR = sp;
            return;
        }

        if (instr.Op == Opcode.POP)
        {
            int sp = State.Registers.Read(30);
            slot.MAR = sp;
            State.MAR = sp;
            State.Registers.Write(30, sp + 1);
            return;
        }

        if (instr.Op == Opcode.RET)
        {
            State.PC = State.Registers.Read(31);
            FlushIF();
            FlushOF();
            return;
        }

        if (instr.Op == Opcode.RETI)
        {
            State.PC = State.Registers.Read(31);
            FlushIF();
            FlushOF();
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

    private void StageForwardingSetup()
    {
        _fwdSources.Clear();

        foreach (var unit in State.FunctionalUnits)
        {
            var exInstr = unit.ExSlot.Instruction;
            if (exInstr != null && !exInstr.IsNop && !exInstr.IsHalt &&
                !exInstr.IsLoad && !exInstr.IsStore &&
                InstructionSet.WritesRd(exInstr.Op) && exInstr.Rd != 0)
            {
                _fwdSources.Add((exInstr.Rd, unit.ExSlot.C, (int)PipelineStage.EX));
            }
        }

        foreach (var unit in State.FunctionalUnits)
        {
            var memInstr = unit.MemSlot.Instruction;
            if (memInstr != null && !memInstr.IsNop && !memInstr.IsHalt &&
                !memInstr.IsStore &&
                InstructionSet.WritesRd(memInstr.Op) && memInstr.Rd != 0)
            {
                _fwdSources.Add((memInstr.Rd, unit.MemSlot.C, (int)PipelineStage.MEM));
            }
        }
    }

    private (int Reg, int Val, int Stage)? FindForward(int reg)
    {
        foreach (var f in _fwdSources)
            if (f.Reg == reg) return f;
        return null;
    }

    private bool StageOF_CheckHazard()
    {
        var slot = State.OF;
        var instr = slot.Instruction;

        if (instr == null || instr.IsNop || instr.IsHalt)
        {
            _dispatchTarget = State.AluUnit;
            _dispatchSlot = slot;
            return false;
        }

        bool rs1Needed = InstructionSet.ReadsRs1(instr.Op);
        bool rs2Needed = InstructionSet.ReadsRs2(instr.Op) && instr.SourceMode != AddressingMode.AM;

        bool rs1Valid = !rs1Needed || State.Registers.IsValid(instr.Rs1);
        bool rs2Valid = !rs2Needed || State.Registers.IsValid(instr.Rs2);

        if (!rs1Valid || !rs2Valid)
        {
            bool rs1ForwardOk = rs1Needed && !rs1Valid && CanForward(instr.Rs1);
            bool rs2ForwardOk = rs2Needed && !rs2Valid && CanForward(instr.Rs2);

            bool needStall = (!rs1Valid && !rs1ForwardOk) || (!rs2Valid && !rs2ForwardOk);
            if (needStall) return true;
        }

        var unitType = InstructionSet.GetUnitType(instr.Op);
        var targetUnit = GetUnit(unitType);

        if (unitType == FunctionalUnitType.MUL && targetUnit.IsExBusy)
            return true;

        int a = rs1Needed ? ResolveOperand(instr.Rs1, instr) : 0;
        int b = rs2Needed ? ResolveOperand(instr.Rs2, instr) : 0;
        int c = 0;

        if (instr.IsStore || instr.Op == Opcode.PUSH)
        {
            a = ResolveOperand(instr.Rd, instr);
            c = ResolveOperand(instr.Rs1, instr);
        }

        slot.A = a;
        slot.B = b;
        slot.C = c;

        if (InstructionSet.WritesRd(instr.Op) && !instr.IsStore && instr.Rd != 0)
            State.Registers.Invalidate(instr.Rd);

        _dispatchTarget = targetUnit;
        _dispatchSlot = slot;
        return false;
    }

    private bool CanForward(int reg)
    {
        if (reg == 0) return true;
        return FindForward(reg).HasValue;
    }

    private int ResolveOperand(int reg, Instruction instr)
    {
        if (reg == 0) return 0;
        var fwd = FindForward(reg);
        if (fwd.HasValue)
        {
            LastForwardings.Add(new ForwardingEvent
            {
                FromStage = fwd.Value.Stage,
                ToStage = (int)PipelineStage.OF,
                Register = reg,
                Value = fwd.Value.Val
            });
            return fwd.Value.Val;
        }
        return State.Registers.Read(reg);
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

    private void AdvanceUnits(bool stall)
    {
        if (!stall)
        {
            State.Slots[(int)PipelineStage.OF] = State.Slots[(int)PipelineStage.IF];
            State.Slots[(int)PipelineStage.IF] = MakeNopSlot();
        }

        foreach (var unit in State.FunctionalUnits)
        {
            unit.WbSlot = unit.MemSlot;

            bool isMulMultiCycle = unit == State.MulUnit && unit.ExCyclesLeft > 0;
            if (!isMulMultiCycle)
            {
                unit.MemSlot = unit.ExSlot;

                bool isDispatchTarget = !stall && unit == _dispatchTarget;
                unit.ExSlot = isDispatchTarget
                    ? _dispatchSlot!
                    : MakeNopSlot();
            }
            else
            {
                unit.MemSlot = MakeNopSlot();
            }
        }

        if (!stall && _dispatchTarget == State.MulUnit && _dispatchSlot != null &&
            _dispatchSlot.Instruction != null && !_dispatchSlot.Instruction.IsNop)
        {
            State.MulUnit.ExCyclesLeft = 3;
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

    private FunctionalUnit GetUnit(FunctionalUnitType type)
    {
        return type switch
        {
            FunctionalUnitType.MUL => State.MulUnit,
            FunctionalUnitType.LDST => State.LdStUnit,
            FunctionalUnitType.JMP => State.JmpUnit,
            _ => State.AluUnit
        };
    }

    private int AluOp(Opcode op, int a, int b, int imm, AddressingMode srcMode)
    {
        int operandB = (srcMode == AddressingMode.AM) ? imm : b;
        return op switch
        {
            Opcode.ADD => a + operandB,
            Opcode.SUB => a - operandB,
            Opcode.AND => a & operandB,
            Opcode.OR => a | operandB,
            Opcode.XOR => a ^ operandB,
            Opcode.CLR => 0,
            Opcode.NEG => -a,
            Opcode.INC => a + 1,
            Opcode.DEC => a - 1,
            Opcode.ASL => a << 1,
            Opcode.ASR => a >> 1,
            Opcode.LSR => (int)((uint)a >> 1),
            Opcode.ROL => (a << 1) | (int)((uint)a >> 31),
            Opcode.ROR => (int)((uint)a >> 1) | (a << 31),
            Opcode.RLC => (a << 1) | (int)((uint)a >> 31),
            Opcode.RRC => (int)((uint)a >> 1) | (a << 31),
            _ => 0
        };
    }

    private bool EvalBranchCondition(Opcode op, int a, int b)
    {
        return op switch
        {
            Opcode.BEQ => a == b,
            Opcode.BNE => a != b,
            Opcode.BL => a < b,
            Opcode.BGE => a >= b,
            _ => false
        };
    }
}
