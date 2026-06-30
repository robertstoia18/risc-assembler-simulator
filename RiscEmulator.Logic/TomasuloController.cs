namespace RiscEmulator.Logic;

public class RSEntry
{
    public string Tag { get; set; } = "";
    public bool Busy { get; set; }
    public Opcode? Op { get; set; }
    public int? Vj { get; set; }
    public int? Vk { get; set; }
    public string? Qj { get; set; }
    public string? Qk { get; set; }
    public int Dest { get; set; }
    public int ImmOrOffset { get; set; }
    public int CyclesLeft { get; set; }
    public int Result { get; set; }
    public bool Executing { get; set; }
    public bool Done { get; set; }
}

public class RegTagEntry
{
    public int Value { get; set; }
    public string? Qi { get; set; }
}

public class TomasuloController
{
    public int CycleCount { get; private set; }
    public bool Halted { get; private set; }

    public Dictionary<string, List<RSEntry>> ReservationStations { get; }
    public RegTagEntry[] RegisterFile { get; } = new RegTagEntry[32];
    public Memory Memory { get; } = new Memory();

    public string? CdbTag { get; private set; }
    public int CdbValue { get; private set; }

    private List<(int Address, Instruction Instr)> _program = new();
    private int _programIndex;
    private int _rsCounter;

    private static readonly Dictionary<string, int> FuSlots = new()
    {
        ["ALU"]  = 4,
        ["MUL"]  = 2,
        ["LDST"] = 2,
        ["JMP"]  = 2
    };

    public TomasuloController()
    {
        ReservationStations = new Dictionary<string, List<RSEntry>>();
        foreach (var kv in FuSlots)
        {
            var list = new List<RSEntry>();
            for (int i = 0; i < kv.Value; i++)
                list.Add(new RSEntry { Tag = $"{kv.Key}.{i}" });
            ReservationStations[kv.Key] = list;
        }

        for (int i = 0; i < 32; i++)
            RegisterFile[i] = new RegTagEntry();
    }

    public void LoadProgram(AssemblerResult result)
    {
        _program = result.Instructions;
        Memory.LoadProgram(result.Words);
        _programIndex = 0;
        _rsCounter = 0;
        CycleCount = 0;
        Halted = false;
        CdbTag = null;
        CdbValue = 0;

        for (int i = 0; i < 32; i++)
        {
            RegisterFile[i].Value = 0;
            RegisterFile[i].Qi = null;
        }

        foreach (var list in ReservationStations.Values)
            foreach (var rs in list)
            {
                rs.Busy = false;
                rs.Op = null;
                rs.Vj = null; rs.Vk = null;
                rs.Qj = null; rs.Qk = null;
                rs.Dest = 0; rs.CyclesLeft = 0;
                rs.Executing = false; rs.Done = false;
            }
    }

    public void Tick()
    {
        if (Halted) return;

        StageWriteResult();
        StageExecute();
        StageIssue();
        CycleCount++;
    }

    private void StageWriteResult()
    {
        CdbTag = null;

        RSEntry? winner = null;
        string? winnerFu = null;

        foreach (var kv in ReservationStations)
            foreach (var rs in kv.Value)
                if (rs.Busy && rs.Done)
                {
                    winner = rs;
                    winnerFu = kv.Key;
                    break;
                }

        if (winner == null) return;

        CdbTag = winner.Tag;
        CdbValue = winner.Result;

        if (winner.Op == Opcode.LD || winner.Op == Opcode.POP)
            CdbValue = Memory.Read(winner.Result);
        if (winner.Op == Opcode.ST || winner.Op == Opcode.PUSH)
        {
            Memory.Write(winner.Result, winner.Vj ?? 0);
            winner.Busy = false;
            winner.Done = false;
            return;
        }

        bool isBranchOrJmp = winner.Op is Opcode.BEQ or Opcode.BNE or Opcode.BL or Opcode.BGE
            or Opcode.JMP or Opcode.JAL or Opcode.RET or Opcode.RETI;

        foreach (var list in ReservationStations.Values)
            foreach (var rs in list)
            {
                if (!rs.Busy || rs.Tag == winner.Tag) continue;
                if (rs.Qj == winner.Tag) { rs.Vj = CdbValue; rs.Qj = null; }
                if (rs.Qk == winner.Tag) { rs.Vk = CdbValue; rs.Qk = null; }
            }

        if (!isBranchOrJmp && winner.Dest != 0)
        {
            var reg = RegisterFile[winner.Dest];
            reg.Value = CdbValue;
            if (reg.Qi == winner.Tag) reg.Qi = null;
        }

        if (winner.Op is Opcode.BEQ or Opcode.BNE or Opcode.BL or Opcode.BGE)
        {
            bool taken = winner.Op switch
            {
                Opcode.BEQ => (winner.Vj ?? 0) == (winner.Vk ?? 0),
                Opcode.BNE => (winner.Vj ?? 0) != (winner.Vk ?? 0),
                Opcode.BL  => (winner.Vj ?? 0) <  (winner.Vk ?? 0),
                Opcode.BGE => (winner.Vj ?? 0) >= (winner.Vk ?? 0),
                _ => false
            };
            if (taken)
            {
                int src = winner.ImmOrOffset;
                int newIdx = _program.FindIndex(p => p.Address == src);
                if (newIdx >= 0) _programIndex = newIdx;
                foreach (var list in ReservationStations.Values)
                    foreach (var rs in list)
                    {
                        if (rs.Busy && rs.Tag != winner.Tag)
                        {
                            rs.Busy = false;
                            rs.Op = null;
                            rs.Vj = null; rs.Vk = null;
                            rs.Qj = null; rs.Qk = null;
                            rs.Dest = 0;
                            rs.Executing = false;
                            rs.Done = false;
                            rs.CyclesLeft = 0;
                            for (int i = 0; i < 32; i++)
                                if (RegisterFile[i].Qi == rs.Tag)
                                    RegisterFile[i].Qi = null;
                        }
                    }
            }
        }

        if (winner.Op == Opcode.HALT) Halted = true;
        winner.Busy = false;
        winner.Done = false;
    }

    private void StageExecute()
    {
        foreach (var list in ReservationStations.Values)
            foreach (var rs in list)
            {
                if (!rs.Busy || rs.Done) continue;
                if (rs.Vj == null || rs.Vk == null) continue;

                if (!rs.Executing)
                {
                    rs.Executing = true;
                    rs.CyclesLeft = GetExecCycles(rs.Op) - 1;
                }

                if (rs.CyclesLeft > 0) { rs.CyclesLeft--; continue; }

                rs.Done = true;
                rs.Executing = false;
                rs.Result = ComputeResult(rs);
            }
    }

    private static int GetExecCycles(Opcode? op) => op == Opcode.MUL ? 3 : 1;

    private static int ComputeResult(RSEntry rs)
    {
        int a = rs.Vj ?? 0;
        int b = rs.Vk ?? 0;
        return rs.Op switch
        {
            Opcode.ADD  => a + b,
            Opcode.SUB  => a - b,
            Opcode.AND  => a & b,
            Opcode.OR   => a | b,
            Opcode.XOR  => a ^ b,
            Opcode.MUL  => a * b,
            Opcode.INC  => a + 1,
            Opcode.DEC  => a - 1,
            Opcode.NEG  => -a,
            Opcode.CLR  => 0,
            Opcode.LD   => a + rs.ImmOrOffset,
            Opcode.ST   => a + rs.ImmOrOffset,
            Opcode.POP  => a,
            Opcode.PUSH => a,
            _ => 0
        };
    }

    private void StageIssue()
    {
        if (_programIndex >= _program.Count) return;
        var (addr, instr) = _program[_programIndex];

        if (instr.IsNop) { _programIndex++; return; }

        if (instr.IsHalt)
        {
            bool anyBusy = ReservationStations.Values.Any(list => list.Any(rs => rs.Busy));
            if (anyBusy) return;
            Halted = true;
            _programIndex++;
            return;
        }

        string fuGroup = InstructionSet.GetUnitType(instr.Op) switch
        {
            FunctionalUnitType.MUL  => "MUL",
            FunctionalUnitType.LDST => "LDST",
            FunctionalUnitType.JMP  => "JMP",
            _                       => "ALU"
        };

        var freeRs = ReservationStations[fuGroup].FirstOrDefault(rs => !rs.Busy);
        if (freeRs == null) return;

        int rs1 = GetSrc1(instr);
        int rs2 = GetSrc2(instr);
        int rd  = GetDest(instr);

        freeRs.Busy = true;
        freeRs.Op = instr.Op;
        freeRs.Dest = rd;
        freeRs.ImmOrOffset = instr.Op == Opcode.LD || instr.Op == Opcode.ST
            ? instr.Immediate
            : instr.Op is Opcode.BEQ or Opcode.BNE or Opcode.BL or Opcode.BGE
                ? instr.OriginAddress + InstructionSet.GetWordCount(instr) + instr.Offset
                : 0;
        freeRs.Executing = false;
        freeRs.Done = false;

        var regJ = rs1 >= 0 && rs1 < 32 ? RegisterFile[rs1] : null;
        var regK = rs2 >= 0 && rs2 < 32 ? RegisterFile[rs2] : null;

        if (regJ == null || regJ.Qi == null)
            freeRs.Vj = rs1 == 0 ? 0 : (regJ?.Value ?? 0);
        else
        {
            freeRs.Vj = null;
            freeRs.Qj = regJ.Qi;
        }

        if (regK == null || regK.Qi == null)
            freeRs.Vk = rs2 == -1 ? instr.Immediate : (rs2 == 0 ? 0 : (regK?.Value ?? 0));
        else
        {
            freeRs.Vk = null;
            freeRs.Qk = regK.Qi;
        }

        if (rd != 0)
            RegisterFile[rd].Qi = freeRs.Tag;

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
        return -1;
    }
}
