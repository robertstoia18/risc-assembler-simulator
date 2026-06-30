using System.Collections.ObjectModel;
using System.Windows.Input;
using RiscEmulator.Logic;

namespace RiscEmulator.UI.ViewModels;

public class RSRowVm
{
    public string Tag { get; set; } = "";
    public string Busy { get; set; } = "";
    public string Op { get; set; } = "";
    public string Vj { get; set; } = "";
    public string Vk { get; set; } = "";
    public string Qj { get; set; } = "";
    public string Qk { get; set; } = "";
    public string State { get; set; } = "";
}

public class RegTagRowVm
{
    public string Reg { get; set; } = "";
    public string Value { get; set; } = "";
    public string Qi { get; set; } = "";
}

public class TomasuloViewModel : BaseViewModel
{
    private readonly TomasuloController _ctrl;
    private readonly Assembler _asm = new();

    private ObservableCollection<RSRowVm> _rsTable = new();
    public ObservableCollection<RSRowVm> RSTable
    {
        get => _rsTable;
        set => Set(ref _rsTable, value);
    }

    private ObservableCollection<RegTagRowVm> _regTable = new();
    public ObservableCollection<RegTagRowVm> RegTable
    {
        get => _regTable;
        set => Set(ref _regTable, value);
    }

    private string _cdbInfo = "CDB: —";
    public string CdbInfo { get => _cdbInfo; set => Set(ref _cdbInfo, value); }

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

    public TomasuloViewModel()
    {
        _ctrl = new TomasuloController();
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
        RSTable = new ObservableCollection<RSRowVm>();
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
        CdbInfo = "CDB: —";
    }

    private void Refresh()
    {
        var newRs = new ObservableCollection<RSRowVm>();
        foreach (var kv in _ctrl.ReservationStations)
            foreach (var rs in kv.Value)
            {
                newRs.Add(new RSRowVm
                {
                    Tag = rs.Tag,
                    Busy = rs.Busy ? "Yes" : "No",
                    Op = rs.Op?.ToString() ?? "",
                    Vj = rs.Vj.HasValue ? rs.Vj.Value.ToString() : "",
                    Vk = rs.Vk.HasValue ? rs.Vk.Value.ToString() : "",
                    Qj = rs.Qj ?? "",
                    Qk = rs.Qk ?? "",
                    State = rs.Done ? "Done" : rs.Executing ? "Exec" : rs.Busy ? "Wait" : ""
                });
            }
        RSTable = newRs;

        var newReg = new ObservableCollection<RegTagRowVm>();
        for (int i = 1; i < 32; i++)
        {
            var rf = _ctrl.RegisterFile[i];
            newReg.Add(new RegTagRowVm
            {
                Reg = $"R{i}",
                Value = rf.Value.ToString(),
                Qi = rf.Qi ?? ""
            });
        }
        RegTable = newReg;

        CdbInfo = _ctrl.CdbTag != null
            ? $"CDB: {_ctrl.CdbTag} = {_ctrl.CdbValue}"
            : "CDB: —";
        OnPropertyChanged(nameof(CdbInfo));
    }

    private static int ParseAddr(string s)
    {
        s = s.Trim().TrimStart('0', 'x').TrimEnd('h', 'H');
        return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : 0;
    }
}