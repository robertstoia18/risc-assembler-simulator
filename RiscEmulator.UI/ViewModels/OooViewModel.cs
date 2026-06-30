using System.Collections.ObjectModel;
using System.Windows.Input;
using RiscEmulator.Logic;

namespace RiscEmulator.UI.ViewModels;

public class PrefetchRowVm
{
    public string InstrLabel { get; set; } = "";
    public string Source { get; set; } = "";
}

public class WindowRowVm
{
    public string Tag { get; set; } = "";
    public string Instr { get; set; } = "";
    public string Vj { get; set; } = "";
    public string Vk { get; set; } = "";
    public string Qj { get; set; } = "";
    public string Qk { get; set; } = "";
    public string State { get; set; } = "";
}

public class RobRowVm
{
    public string Tag { get; set; } = "";
    public string Instr { get; set; } = "";
    public string Dest { get; set; } = "";
    public string Value { get; set; } = "";
    public string Ready { get; set; } = "";
}

public class OooViewModel : BaseViewModel
{
    private readonly OooController _ctrl;
    private readonly Assembler _asm = new();

    private ObservableCollection<PrefetchRowVm> _primaryBufferRows = new();
    public ObservableCollection<PrefetchRowVm> PrimaryBufferRows
    {
        get => _primaryBufferRows;
        set => Set(ref _primaryBufferRows, value);
    }

    private ObservableCollection<PrefetchRowVm> _branchBufferRows = new();
    public ObservableCollection<PrefetchRowVm> BranchBufferRows
    {
        get => _branchBufferRows;
        set => Set(ref _branchBufferRows, value);
    }

    private ObservableCollection<WindowRowVm> _windowRows = new();
    public ObservableCollection<WindowRowVm> WindowRows
    {
        get => _windowRows;
        set => Set(ref _windowRows, value);
    }

    private ObservableCollection<RobRowVm> _robRows = new();
    public ObservableCollection<RobRowVm> RobRows
    {
        get => _robRows;
        set => Set(ref _robRows, value);
    }

    private ObservableCollection<RegTagRowVm> _regTable = new();
    public ObservableCollection<RegTagRowVm> RegTable
    {
        get => _regTable;
        set => Set(ref _regTable, value);
    }

    private string _fuStatus = "";
    public string FuStatus { get => _fuStatus; set => Set(ref _fuStatus, value); }

    private string _status = "Încarcă un program.";
    public string Status { get => _status; set => Set(ref _status, value); }

    private int _cycleCount;
    public int CycleCount { get => _cycleCount; set => Set(ref _cycleCount, value); }

    public ICommand TickCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand ResetCommand { get; }

    private string _programSource = "";
    public string ProgramSource { get => _programSource; set => Set(ref _programSource, value); }

    private string _startAddress = "0x0000";
    public string StartAddress { get => _startAddress; set => Set(ref _startAddress, value); }

    public OooViewModel()
    {
        _ctrl = new OooController();
        TickCommand = new RelayCommand(OnTick, () => !_ctrl.Halted);
        LoadCommand = new RelayCommand(OnLoad);
        ResetCommand = new RelayCommand(OnReset);
        InitRegTable();
    }

    private void InitRegTable()
    {
        var newReg = new ObservableCollection<RegTagRowVm>();
        for (int i = 1; i < 32; i++)
            newReg.Add(new RegTagRowVm { Reg = $"R{i}", Value = "0", Qi = "" });
        RegTable = newReg;

        PrimaryBufferRows = new ObservableCollection<PrefetchRowVm>();
        BranchBufferRows = new ObservableCollection<PrefetchRowVm>();
        WindowRows = new ObservableCollection<WindowRowVm>();
        RobRows = new ObservableCollection<RobRowVm>();
    }

    private void OnLoad()
    {
        try
        {
            int addr = ParseAddr(StartAddress);
            var result = _asm.Assemble(ProgramSource, addr);
            _ctrl.LoadProgram(result);
            Status = $"Program încărcat: {result.Instructions.Count} instrucțiuni.";
            Refresh();
        }
        catch (Exception ex) { Status = $"Eroare: {ex.Message}"; }
    }

    private void OnTick()
    {
        _ctrl.Tick();
        CycleCount = _ctrl.CycleCount;
        Refresh();
        if (_ctrl.Halted) Status = $"HALT la ciclul {_ctrl.CycleCount}.";
        else Status = $"Tact {_ctrl.CycleCount}";
    }

    private void OnReset()
    {
        _ctrl.LoadProgram(new AssemblerResult());
        CycleCount = 0;
        Status = "Reset.";
        InitRegTable();
        FuStatus = "";
    }

    private void Refresh()
    {
        // Primary buffer
        var newPrimary = new ObservableCollection<PrefetchRowVm>();
        foreach (var e in _ctrl.PrimaryBuffer)
            newPrimary.Add(new PrefetchRowVm { InstrLabel = e.Instr.ToString(), Source = "Secvențial" });
        PrimaryBufferRows = newPrimary;

        // Branch buffer
        var newBranch = new ObservableCollection<PrefetchRowVm>();
        foreach (var e in _ctrl.BranchBuffer)
            newBranch.Add(new PrefetchRowVm { InstrLabel = e.Instr.ToString(), Source = "Branch Target" });
        BranchBufferRows = newBranch;

        var newWindow = new ObservableCollection<WindowRowVm>();
        foreach (var w in _ctrl.InstructionWindow)
            newWindow.Add(new WindowRowVm
            {
                Tag = w.Tag,
                Instr = w.Instr.ToString(),
                Vj = w.Vj.HasValue ? w.Vj.Value.ToString() : "",
                Vk = w.Vk.HasValue ? w.Vk.Value.ToString() : "",
                Qj = w.Qj ?? "",
                Qk = w.Qk ?? "",
                State = w.Dispatched ? "Dispatched" : "Waiting"
            });
        WindowRows = newWindow;

        var newRob = new ObservableCollection<RobRowVm>();
        foreach (var r in _ctrl.ReorderBuffer)
            newRob.Add(new RobRowVm
            {
                Tag = r.Tag,
                Instr = r.Instr.ToString(),
                Dest = r.Dest != 0 ? $"R{r.Dest}" : "",
                Value = r.Value.HasValue ? r.Value.Value.ToString() : "—",
                Ready = r.Ready ? "Yes" : "No"
            });
        RobRows = newRob;

        var newReg = new ObservableCollection<RegTagRowVm>();
        for (int i = 1; i < 32; i++)
        {
            newReg.Add(new RegTagRowVm
            {
                Reg = $"R{i}",
                Value = _ctrl.Registers[i].ToString(),
                Qi = _ctrl.RegisterTags[i] ?? ""
            });
        }
        RegTable = newReg;

        var parts = new List<string>();
        foreach (var kv in _ctrl.FuSlots)
        {
            var fu = kv.Value;
            parts.Add($"{kv.Key}: {(fu.Busy ? (fu.Instr?.ToString() ?? "busy") + $" [{fu.CyclesLeft}]" : "free")}");
        }
        FuStatus = string.Join("  |  ", parts);
        OnPropertyChanged(nameof(FuStatus));
    }

    private static int ParseAddr(string s)
    {
        s = s.Trim().TrimStart('0', 'x').TrimEnd('h', 'H');
        return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : 0;
    }
}