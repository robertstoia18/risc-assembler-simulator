namespace RiscEmulator.Logic;

public class FuScoreboardEntry
{
    public bool Busy;
    public Opcode? Op;
    public int Fi;
    public int Fj;
    public int Fk;
    public string? Qj;
    public string? Qk;
    public bool Rj;
    public bool Rk;
    public int Vj;
    public int Vk;
    public int Imm;
    public int Result;
    public int CyclesLeft;
    public bool ExecDone;
}

public class InstrScoreboardEntry
{
    public Instruction Instr { get; set; } = null!;
    public string FuName { get; set; } = "";
    public int IssueCycle { get; set; } = -1;
    public int ReadCycle { get; set; } = -1;
    public int ExecDoneCycle { get; set; } = -1;
    public int WriteCycle { get; set; } = -1;
}

public class ScoreboardController
{
    public int CycleCount { get; private set; }
    public bool Halted { get; private set; }

    public Dictionary<string, FuScoreboardEntry> FuStatus { get; } = new()
    {
        ["ALU"]  = new FuScoreboardEntry(),
        ["MUL"]  = new FuScoreboardEntry(),
        ["LDST"] = new FuScoreboardEntry(),
        ["JMP"]  = new FuScoreboardEntry()
    };

    public string?[] RegisterResult { get; } = new string?[32];
    public List<InstrScoreboardEntry> InstrStatus { get; } = new();
    public int[] Registers { get; } = new int[32];
    public Memory Memory { get; } = new Memory();

    private List<(int Address, Instruction Instr)> _program = new();
    private int _programIndex;

    public void LoadProgram(AssemblerResult result)
    {
        _program = result.Instructions;
        Memory.LoadProgram(result.Words);
        _programIndex = 0;
        CycleCount = 0;
        Halted = false;
        Array.Clear(Registers, 0, 32);
        Array.Clear(RegisterResult, 0, 32);
        InstrStatus.Clear();
        foreach (var fu in FuStatus.Values)
        {
            fu.Busy = false;
            fu.Op = null;
            fu.Fi = 0; fu.Fj = 0; fu.Fk = 0;
            fu.Qj = null; fu.Qk = null;
            fu.Rj = false; fu.Rk = false;
            fu.Vj = 0; fu.Vk = 0; fu.Imm = 0;
            fu.Result = 0; fu.CyclesLeft = 0; fu.ExecDone = false;
        }
    }

    public void Tick()
    {
        if (Halted) return;
        StageWriteResult();
        StageExecute();
        StageReadOperands();
        StageIssue();
        CycleCount++;
    }

    private void StageWriteResult()
    {
        foreach (var kv in FuStatus)
        {
            var fu = kv.Value;
            if (!fu.Busy || !fu.ExecDone) continue;

            if (WarCheckPasses(kv.Key, fu.Fi))
            {
                var entry = InstrStatus.LastOrDefault(e => e.FuName == kv.Key && e.WriteCycle < 0);
                if (entry != null)
                {
                    entry.WriteCycle = CycleCount;
                    if (entry.Instr.IsHalt) Halted = true;
                }

                if (!entry!.Instr.IsStore && !entry.Instr.IsBranch && !entry.Instr.IsJump && fu.Fi != 0)
                    Registers[fu.Fi] = fu.Result;

                if (fu.Op == Opcode.ST)
                    Memory.Write(fu.Result, fu.Vk);
                else if (fu.Op == Opcode.PUSH)
                    Memory.Write(fu.Result, fu.Vj);
                if (fu.Op == Opcode.LD || fu.Op == Opcode.POP)
                    Registers[fu.Fi] = Memory.Read(fu.Result);
                if (fu.Op is Opcode.BEQ or Opcode.BNE or Opcode.BL or Opcode.BGE)
                    EvalBranch(entry.Instr, fu);

                RegisterResult[fu.Fi] = null;

                foreach (var kv2 in FuStatus)
                {
                    var fu2 = kv2.Value;
                    if (!fu2.Busy) continue;
                    bool rjSet = false, rkSet = false;
                    if (fu2.Qj == kv.Key) { fu2.Vj = fu.Result; fu2.Rj = true; fu2.Qj = null; rjSet = true; }
                    if (fu2.Qk == kv.Key) { fu2.Vk = fu.Result; fu2.Rk = true; fu2.Qk = null; rkSet = true; }
                    if ((rjSet || rkSet) && fu2.Rj && fu2.Rk && !fu2.ExecDone)
                    {
                        fu2.CyclesLeft = GetExecCycles(fu2.Op) - 1;
                        var rdEntry = InstrStatus.LastOrDefault(e => e.FuName == kv2.Key && e.ReadCycle < 0);
                        if (rdEntry != null) rdEntry.ReadCycle = CycleCount;
                    }
                }

                fu.Busy = false;
                fu.Op = null;
                fu.ExecDone = false;
            }
        }
    }

    private bool WarCheckPasses(string fuName, int fi)
    {
        foreach (var kv in FuStatus)
        {
            if (kv.Key == fuName) continue;
            var fu = kv.Value;
            if (!fu.Busy) continue;
            if (fu.Fj == fi && !fu.Rj && fu.Qj != fuName) return false;
            if (fu.Fk == fi && !fu.Rk && fu.Qk != fuName) return false;
        }
        return true;
    }

    private void EvalBranch(Instruction instr, FuScoreboardEntry fu)
    {
        bool taken = instr.Op switch
        {
            Opcode.BEQ => fu.Vj == fu.Vk,
            Opcode.BNE => fu.Vj != fu.Vk,
            Opcode.BL  => fu.Vj < fu.Vk,
            Opcode.BGE => fu.Vj >= fu.Vk,
            _ => false
        };
        if (taken)
        {
            int target = instr.OriginAddress + InstructionSet.GetWordCount(instr) + instr.Offset;
            int newIdx = _program.FindIndex(p => p.Address == target);
            if (newIdx >= 0) _programIndex = newIdx;
            else _programIndex = _program.Count;
        }
    }

    private void StageExecute()
    {
        foreach (var fu in FuStatus.Values)
        {
            if (!fu.Busy || fu.ExecDone || !fu.Rj || !fu.Rk) continue;
            if (fu.CyclesLeft > 0) { fu.CyclesLeft--; continue; }
            fu.ExecDone = true;

            var entry = InstrStatus.LastOrDefault(e => e.FuName == GetFuName(fu) && e.ExecDoneCycle < 0);
            if (entry != null) entry.ExecDoneCycle = CycleCount;

            fu.Result = ComputeResult(fu);
        }
    }

    private string GetFuName(FuScoreboardEntry target)
    {
        foreach (var kv in FuStatus)
            if (kv.Value == target) return kv.Key;
        return "";
    }

    private int ComputeResult(FuScoreboardEntry fu)
    {
        if (fu.Op == null) return 0;
        return fu.Op.Value switch
        {
            Opcode.ADD => fu.Vj + fu.Vk,
            Opcode.SUB => fu.Vj - fu.Vk,
            Opcode.AND => fu.Vj & fu.Vk,
            Opcode.OR  => fu.Vj | fu.Vk,
            Opcode.XOR => fu.Vj ^ fu.Vk,
            Opcode.MUL => fu.Vj * fu.Vk,
            Opcode.INC => fu.Vj + 1,
            Opcode.DEC => fu.Vj - 1,
            Opcode.NEG => -fu.Vj,
            Opcode.CLR => 0,
            Opcode.LD  => fu.Vj + fu.Vk,
            Opcode.ST  => fu.Vj + fu.Imm,
            Opcode.POP => fu.Vj,
            Opcode.PUSH => fu.Vj,
            _ => 0
        };
    }

    private void StageReadOperands()
    {
        foreach (var kv in FuStatus)
        {
            var fu = kv.Value;
            if (!fu.Busy || fu.ExecDone || fu.Rj && fu.Rk) continue;
            if (!fu.Rj && fu.Qj == null) fu.Rj = true;
            if (!fu.Rk && fu.Qk == null) fu.Rk = true;

            if (fu.Rj && fu.Rk)
            {
                var entry = InstrStatus.LastOrDefault(e => e.FuName == kv.Key && e.ReadCycle < 0);
                if (entry != null) entry.ReadCycle = CycleCount;
                fu.CyclesLeft = GetExecCycles(fu.Op) - 1;
            }
        }
    }

    private static int GetExecCycles(Opcode? op) => op == Opcode.MUL ? 3 : 1;

    private void StageIssue()
    {
        if (_programIndex >= _program.Count) return;
        var (addr, instr) = _program[_programIndex];

        if (instr.IsNop) { _programIndex++; return; }
        if (instr.IsHalt)
        {
            if (FuStatus.Values.Any(f => f.Busy)) return;
            InstrStatus.Add(new InstrScoreboardEntry
            {
                Instr = instr,
                FuName = "ALU",
                IssueCycle = CycleCount,
                ReadCycle = CycleCount,
                ExecDoneCycle = CycleCount,
                WriteCycle = CycleCount
            });
            _programIndex++;
            Halted = true;
            return;
        }

        string fuName = InstructionSet.GetUnitType(instr.Op) switch
        {
            FunctionalUnitType.MUL  => "MUL",
            FunctionalUnitType.LDST => "LDST",
            FunctionalUnitType.JMP  => "JMP",
            _                       => "ALU"
        };

        var fu = FuStatus[fuName];
        if (fu.Busy) return;

        int rd = GetDest(instr);
        if (rd != 0 && RegisterResult[rd] != null) return;

        int rs1 = GetSrc1(instr);
        int rs2 = GetSrc2(instr);

        fu.Busy = true;
        fu.Op = instr.Op;
        fu.Fi = rd;
        fu.Fj = rs1;
        fu.Fk = rs2;
        fu.Imm = instr.Immediate;
        fu.ExecDone = false;

        if (rs1 == 0 || RegisterResult[rs1] == null)
        {
            fu.Rj = true;
            fu.Vj = rs1 == 0 ? 0 : Registers[rs1];
            fu.Qj = null;
        }
        else
        {
            fu.Rj = false;
            fu.Qj = RegisterResult[rs1];
            fu.Vj = 0;
        }

        if (rs2 == 0 || RegisterResult[rs2] == null)
        {
            fu.Rk = true;
            fu.Vk = rs2 == 0 ? instr.Immediate : Registers[rs2];
            fu.Qk = null;
        }
        else
        {
            fu.Rk = false;
            fu.Qk = RegisterResult[rs2];
            fu.Vk = 0;
        }

        if (rd != 0)
            RegisterResult[rd] = fuName;

        InstrStatus.Add(new InstrScoreboardEntry
        {
            Instr = instr,
            FuName = fuName,
            IssueCycle = CycleCount
        });

        if (fu.Rj && fu.Rk)
        {
            var last = InstrStatus.Last();
            last.ReadCycle = CycleCount;
            fu.CyclesLeft = GetExecCycles(instr.Op) - 1;
        }

        _programIndex++;
    }

    private int GetDest(Instruction i)
    {
        if (i.IsStore || i.Op == Opcode.PUSH || i.IsBranch || i.IsJump) return 0;
        return i.Rd;
    }

    private int GetSrc1(Instruction i)
    {
        if (i.IsStore) return i.Rd;
        if (i.Op == Opcode.PUSH) return i.Rd;
        if (InstructionSet.ReadsRs1(i.Op)) return i.Rs1;
        return 0;
    }

    private int GetSrc2(Instruction i)
    {
        if (i.IsStore) return i.Rs1;
        if (InstructionSet.ReadsRs2(i.Op) && i.SourceMode == AddressingMode.AD) return i.Rs2;
        return 0;
    }
}
