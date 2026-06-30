using System.Collections.ObjectModel;
using System.Windows.Input;
using RiscEmulator.Logic;

namespace RiscEmulator.UI.ViewModels;

public class FuStatusRowVm : BaseViewModel
{
    public string FuName { get; set; } = "";
    public string Busy { get; set; } = "";
    public string Op { get; set; } = "";
    public string Fi { get; set; } = "";
    public string Fj { get; set; } = "";
    public string Fk { get; set; } = "";
    public string Qj { get; set; } = "";
    public string Qk { get; set; } = "";
    public string Rj { get; set; } = "";
    public string Rk { get; set; } = "";
}

public class InstrStatusRowVm
{
    public string Instr { get; set; } = "";
    public string Fu { get; set; } = "";
    public string Issue { get; set; } = "";
    public string Read { get; set; } = "";
    public string ExecDone { get; set; } = "";
    public string Write { get; set; } = "";
}

public class RegResultRowVm
{
    public string Reg { get; set; } = "";
    public string Value { get; set; } = "";
    public string Fu { get; set; } = "";
}

public class ScoreboardViewModel : BaseViewModel
{
    private readonly ScoreboardController _ctrl;
    private readonly Assembler _asm = new();

    private ObservableCollection<FuStatusRowVm> _fuTable = new();
    public ObservableCollection<FuStatusRowVm> FuTable
    {
        get => _fuTable;
        set => Set(ref _fuTable, value);
    }

    private ObservableCollection<InstrStatusRowVm> _instrTable = new();
    public ObservableCollection<InstrStatusRowVm> InstrTable
    {
        get => _instrTable;
        set => Set(ref _instrTable, value);
    }

    private ObservableCollection<RegResultRowVm> _regResultTable = new();
    public ObservableCollection<RegResultRowVm> RegResultTable
    {
        get => _regResultTable;
        set => Set(ref _regResultTable, value);
    }

    private string _status = "Încarcă un program și apasă Tick.";
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

    public ScoreboardViewModel()
    {
        _ctrl = new ScoreboardController();
        TickCommand = new RelayCommand(OnTick, () => !_ctrl.Halted);
        LoadCommand = new RelayCommand(OnLoad);
        ResetCommand = new RelayCommand(OnReset);
        InitTables();
    }

    private void InitTables()
    {
        FuTable = new ObservableCollection<FuStatusRowVm>();
        foreach (var name in new[] { "ALU", "MUL", "LDST", "JMP" })
            FuTable.Add(new FuStatusRowVm { FuName = name });

        RegResultTable = new ObservableCollection<RegResultRowVm>();
        for (int i = 1; i < 32; i++)
            RegResultTable.Add(new RegResultRowVm { Reg = $"R{i}", Fu = "" });

        InstrTable = new ObservableCollection<InstrStatusRowVm>();
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
        InitTables();
        InstrTable = new ObservableCollection<InstrStatusRowVm>();
    }

    private void Refresh()
    {
        var newFu = new ObservableCollection<FuStatusRowVm>();
        int i = 0;
        foreach (var kv in _ctrl.FuStatus)
        {
            var fu = kv.Value;
            var row = new FuStatusRowVm
            {
                FuName = kv.Key,
                Busy = fu.Busy ? "Yes" : "No",
                Op = fu.Op?.ToString() ?? "",
                Fi = fu.Fi != 0 ? $"R{fu.Fi}" : "",
                Fj = fu.Fj != 0 ? $"R{fu.Fj}" : "",
                Fk = fu.Fk != 0 ? $"R{fu.Fk}" : "",
                Qj = fu.Qj ?? "",
                Qk = fu.Qk ?? "",
                Rj = fu.Rj ? "Yes" : "No",
                Rk = fu.Rk ? "Yes" : "No"
            };
            newFu.Add(row);
        }
        FuTable = newFu;

        var newInstr = new ObservableCollection<InstrStatusRowVm>();
        foreach (var e in _ctrl.InstrStatus)
        {
            newInstr.Add(new InstrStatusRowVm
            {
                Instr = e.Instr?.ToString() ?? "",
                Fu = e.FuName,
                Issue = e.IssueCycle >= 0 ? e.IssueCycle.ToString() : "",
                Read = e.ReadCycle >= 0 ? e.ReadCycle.ToString() : "",
                ExecDone = e.ExecDoneCycle >= 0 ? e.ExecDoneCycle.ToString() : "",
                Write = e.WriteCycle >= 0 ? e.WriteCycle.ToString() : ""
            });
        }
        InstrTable = newInstr;

        var newReg = new ObservableCollection<RegResultRowVm>();
        for (int r = 0; r < 32; r++)
        {
            if (r == 0) continue; 
            newReg.Add(new RegResultRowVm
            {
                Reg = $"R{r}",
                Value = _ctrl.Registers[r].ToString(),
                Fu = _ctrl.RegisterResult[r] ?? ""
            });
        }
        RegResultTable = newReg;
    }

    private static int ParseAddr(string s)
    {
        s = s.Trim().TrimStart('0', 'x').TrimEnd('h', 'H');
        return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : 0;
    }
}