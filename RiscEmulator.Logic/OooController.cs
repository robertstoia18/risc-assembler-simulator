namespace RiscEmulator.Logic;

public class PrefetchEntry
{
    public Instruction Instr { get; set; } = null!;
    public bool IsFromBranchTarget { get; set; }
}

public class RobEntry
{
    public string Tag { get; set; } = "";
    public Instruction Instr { get; set; } = null!;
    public int Dest { get; set; }
    public int? Value { get; set; }
    public bool Ready { get; set; }
    public bool IsStore { get; set; }
    public int StoreAddress { get; set; }
    public int StoreValue { get; set; }
}

public class IssueWindowEntry
{
    public string Tag { get; set; } = "";
    public Instruction Instr { get; set; } = null!;
    public int Dest { get; set; }
    public int? Vj { get; set; }
    public int? Vk { get; set; }
    public string? Qj { get; set; }
    public string? Qk { get; set; }
    public bool Dispatched { get; set; }
}

public class OooFuSlot
{
    public bool Busy { get; set; }
    public string Tag { get; set; } = "";
    public Instruction? Instr { get; set; }
    public int Vj { get; set; }
    public int Vk { get; set; }
    public int CyclesLeft { get; set; }
    public int Result { get; set; }
    public bool Done { get; set; }
}

public class OooController
{
    private const int PrefetchSize = 8;
    private const int WindowSize = 8;
    private const int RobSize = 8;

    public int CycleCount { get; private set; }
    public bool Halted { get; private set; }

    public Queue<PrefetchEntry> PrimaryBuffer { get; } = new();
    public Queue<PrefetchEntry> BranchBuffer { get; } = new();
    public bool FetchingBranch { get; private set; }
    public List<IssueWindowEntry> InstructionWindow { get; } = new();
    public Queue<RobEntry> ReorderBuffer { get; } = new();

    public int[] Registers { get; } = new int[32];
    public string?[] RegisterTags { get; } = new string?[32];
    public Memory Memory { get; } = new Memory();

    private readonly Dictionary<string, OooFuSlot> _fuSlots = new()
    {
        ["ALU"]  = new OooFuSlot(),
        ["MUL"]  = new OooFuSlot(),
        ["LDST"] = new OooFuSlot(),
        ["JMP"]  = new OooFuSlot()
    };

    public IReadOnlyDictionary<string, OooFuSlot> FuSlots => _fuSlots;

    private List<(int Address, Instruction Instr)> _program = new();
    private int _fetchPc;
    private int _branchFetchPc;
    private int _tagCounter;

    public void LoadProgram(AssemblerResult result)
    {
        _program = result.Instructions;
        Memory.LoadProgram(result.Words);
        _fetchPc = _program.Count > 0 ? _program[0].Address : 0;
        _branchFetchPc = -1;
        FetchingBranch = false;
        _tagCounter = 0;
        CycleCount = 0;
        Halted = false;

        PrimaryBuffer.Clear();
        BranchBuffer.Clear();
        InstructionWindow.Clear();
        ReorderBuffer.Clear();
        Array.Clear(Registers, 0, 32);
        Array.Clear(RegisterTags, 0, 32);

        foreach (var fu in _fuSlots.Values)
        {
            fu.Busy = false;
            fu.Instr = null;
            fu.Done = false;
            fu.Tag = "";
        }
    }

    public void Tick()
    {
        if (Halted) return;

        StageCommit();
        StageExecute();
        StageIssueFu();
        StageDispatch();
        StageFetch();
        CycleCount++;
    }

    private void StageFetch()
    {
        if (PrimaryBuffer.Count < PrefetchSize)
        {
            var instr = FetchAt(ref _fetchPc);
            if (instr != null)
            {
                PrimaryBuffer.Enqueue(new PrefetchEntry { Instr = instr, IsFromBranchTarget = false });

                if (instr.IsBranch && !FetchingBranch)
                {
                    int target = instr.OriginAddress + InstructionSet.GetWordCount(instr) + instr.Offset;
                    _branchFetchPc = target;
                    FetchingBranch = true;
                    BranchBuffer.Clear();
                }
            }
        }

        if (FetchingBranch && BranchBuffer.Count < PrefetchSize / 2)
        {
            var instr = FetchAt(ref _branchFetchPc);
            if (instr != null)
                BranchBuffer.Enqueue(new PrefetchEntry { Instr = instr, IsFromBranchTarget = true });
        }
    }

    private Instruction? FetchAt(ref int pc)
    {
        int currentPc = pc;
        var pair = _program.FirstOrDefault(p => p.Address == currentPc);
        if (pair.Instr == null) return null;
        pc = currentPc + InstructionSet.GetWordCount(pair.Instr);
        return pair.Instr;
    }

    private void StageDispatch()
    {
        if (InstructionWindow.Count >= WindowSize || PrimaryBuffer.Count == 0) return;
        if (ReorderBuffer.Count >= RobSize) return;

        var entry = PrimaryBuffer.Dequeue();
        var instr = entry.Instr;

        if (instr.IsNop) return;

        string tag = $"ROB{_tagCounter++}";
        int rd  = GetDest(instr);
        int rs1 = GetSrc1(instr);
        int rs2 = GetSrc2(instr);

        var win = new IssueWindowEntry
        {
            Tag = tag,
            Instr = instr,
            Dest = rd,
            Dispatched = false
        };

        ResolveSource(rs1, instr.Immediate, true, win);
        ResolveSource(rs2, instr.Immediate, false, win);

        if (rd != 0)
            RegisterTags[rd] = tag;

        InstructionWindow.Add(win);

        ReorderBuffer.Enqueue(new RobEntry
        {
            Tag = tag,
            Instr = instr,
            Dest = rd,
            Ready = false,
            IsStore = instr.IsStore || instr.Op == Opcode.PUSH
        });
    }

    private void ResolveSource(int reg, int imm, bool isJ, IssueWindowEntry win)
    {
        int? val = null;
        string? qtag = null;

        if (reg < 0)
        {
            val = imm;
        }
        else if (reg == 0 || RegisterTags[reg] == null)
        {
            val = reg == 0 ? 0 : Registers[reg];
        }
        else
        {
            var producerRob = ReorderBuffer.FirstOrDefault(r => r.Tag == RegisterTags[reg]);
            if (producerRob != null && producerRob.Ready)
                val = producerRob.Value;
            else
                qtag = RegisterTags[reg];
        }

        if (isJ) { win.Vj = val; win.Qj = qtag; }
        else     { win.Vk = val; win.Qk = qtag; }
    }

    private void StageIssueFu()
    {
        foreach (var win in InstructionWindow)
        {
            if (win.Dispatched) continue;
            if (win.Vj == null || win.Vk == null) continue;

            string fuName = InstructionSet.GetUnitType(win.Instr.Op) switch
            {
                FunctionalUnitType.MUL  => "MUL",
                FunctionalUnitType.LDST => "LDST",
                FunctionalUnitType.JMP  => "JMP",
                _                       => "ALU"
            };

            var fu = _fuSlots[fuName];
            if (fu.Busy) continue;

            fu.Busy = true;
            fu.Tag = win.Tag;
            fu.Instr = win.Instr;
            fu.Vj = win.Vj.Value;
            fu.Vk = win.Vk.Value;
            fu.CyclesLeft = GetExecCycles(win.Instr.Op) - 1;
            fu.Done = false;
            win.Dispatched = true;
        }
    }

    private void StageExecute()
    {
        foreach (var fu in _fuSlots.Values)
        {
            if (!fu.Busy || fu.Done) continue;
            if (fu.CyclesLeft > 0) { fu.CyclesLeft--; continue; }

            fu.Done = true;
            fu.Result = ComputeResult(fu);

            var rob = ReorderBuffer.FirstOrDefault(r => r.Tag == fu.Tag);
            if (rob != null)
            {
                if (fu.Instr?.IsLoad == true)
                    rob.Value = Memory.Read(fu.Result);
                else
                    rob.Value = fu.Result;

                if (rob.IsStore)
                {
                    rob.StoreAddress = fu.Result;
                    rob.StoreValue = fu.Vk;
                }
                rob.Ready = true;
            }

            foreach (var w in InstructionWindow)
            {
                if (w.Dispatched) continue;
                if (w.Qj == fu.Tag) { w.Vj = rob?.Value ?? fu.Result; w.Qj = null; }
                if (w.Qk == fu.Tag) { w.Vk = rob?.Value ?? fu.Result; w.Qk = null; }
            }

            fu.Busy = false;
            fu.Done = false;
            fu.Instr = null;
        }
    }

    private void StageCommit()
    {
        if (ReorderBuffer.Count == 0) return;
        var head = ReorderBuffer.Peek();
        if (!head.Ready) return;

        ReorderBuffer.Dequeue();
        InstructionWindow.RemoveAll(w => w.Tag == head.Tag);

        if (FetchingBranch && head.Instr.IsBranch)
        {
            FetchingBranch = false;
            bool taken = head.Instr.Op switch
            {
                Opcode.BEQ => head.Value == 1,
                _ => head.Value == 1
            };
            if (taken)
            {
                PrimaryBuffer.Clear();
                foreach (var bi in BranchBuffer)
                    PrimaryBuffer.Enqueue(bi);
                BranchBuffer.Clear();
                var robList = ReorderBuffer.ToList();
                int branchRobIndex = robList.FindIndex(r => r.Tag == head.Tag);

                var toRemove = new List<IssueWindowEntry>();
                foreach (var w in InstructionWindow)
                {
                    var robEntry = robList.FirstOrDefault(r => r.Tag == w.Tag);
                    if (robEntry != null && robList.IndexOf(robEntry) > branchRobIndex)
                    {
                        toRemove.Add(w);
                        foreach (var fu in _fuSlots.Values)
                            if (fu.Tag == w.Tag) { fu.Busy = false; fu.Instr = null; fu.Done = false; }
                    }
                }
                foreach (var w in toRemove)
                    InstructionWindow.Remove(w);

                while (ReorderBuffer.Count > 0 && ReorderBuffer.Peek().Tag != head.Tag)
                {
                    var oldEntry = ReorderBuffer.Dequeue();
                    foreach (var fu in _fuSlots.Values)
                        if (fu.Tag == oldEntry.Tag) { fu.Busy = false; fu.Instr = null; fu.Done = false; }
                }
            }
            else
            {
                BranchBuffer.Clear();
            }
        }

        if (head.Instr.IsHalt) { Halted = true; return; }
        if (head.IsStore) { Memory.Write(head.StoreAddress, head.StoreValue); return; }

        if (head.Dest != 0)
        {
            Registers[head.Dest] = head.Value ?? 0;
            if (RegisterTags[head.Dest] == head.Tag)
                RegisterTags[head.Dest] = null;
        }
    }

    private static int GetExecCycles(Opcode op) => op == Opcode.MUL ? 3 : 1;

    private static int ComputeResult(OooFuSlot fu)
    {
        int a = fu.Vj, b = fu.Vk;
        int imm = fu.Instr?.Immediate ?? 0;
        if (fu.Instr == null) return 0;
        return fu.Instr.Op switch
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
            Opcode.LD   => a + imm,
            Opcode.ST   => a + imm,
            Opcode.BEQ => a == b ? 1 : 0,
            Opcode.BNE => a != b ? 1 : 0,
            Opcode.BL => a < b ? 1 : 0,
            Opcode.BGE => a >= b ? 1 : 0,
            Opcode.JMP => 1,
            Opcode.JAL => 1,
            _ => 0
        };
    }

    private static int GetDest(Instruction i)
    {
        if (i.IsStore || i.Op == Opcode.PUSH || i.IsBranch || i.IsJump) return 0;
        return i.Rd;
    }

    private static int GetSrc1(Instruction i)
    {
        if (i.IsStore) return i.Rd;
        if (i.Op == Opcode.PUSH) return i.Rd;
        if (InstructionSet.ReadsRs1(i.Op)) return i.Rs1;
        return 0;
    }

    private static int GetSrc2(Instruction i)
    {
        if (i.IsStore) return i.Rs1;
        if (InstructionSet.ReadsRs2(i.Op) && i.SourceMode == AddressingMode.AD) return i.Rs2;
        return -1;
    }
}
