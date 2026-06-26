using System.Collections.ObjectModel;
using System.Windows.Input;
using RiscEmulator.Logic;

namespace RiscEmulator.UI.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly ProcessorState _state = new();
    private readonly PipelineController _ctrl;
    private readonly Assembler _asm = new();

    private string _programSource = string.Empty;
    private string _statusMessage = "Introduceți programul și apăsați 'Load Program'.";
    private bool _programLoaded;
    private int _cycleCount;
    private int _pc, _mar, _mdr, _ir, _regA, _regB, _regC;

    public ObservableCollection<PipelineSlotViewModel> PipelineSlots { get; } = new();
    public ObservableCollection<RegisterViewModel> Registers { get; } = new();
    public ObservableCollection<MemoryRowViewModel> MemoryRows { get; } = new();

    public string ProgramSource
    {
        get => _programSource;
        set => Set(ref _programSource, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public int CycleCount
    {
        get => _cycleCount;
        set => Set(ref _cycleCount, value);
    }

    public int PC  { get => _pc;   set => Set(ref _pc,   value); }
    public int MAR { get => _mar;  set => Set(ref _mar,  value); }
    public int MDR { get => _mdr;  set => Set(ref _mdr,  value); }
    public int IR  { get => _ir;   set => Set(ref _ir,   value); }
    public int RegA { get => _regA; set => Set(ref _regA, value); }
    public int RegB { get => _regB; set => Set(ref _regB, value); }
    public int RegC { get => _regC; set => Set(ref _regC, value); }

    public ICommand NextClockCommand { get; }
    public ICommand LoadProgramCommand { get; }
    public ICommand ResetCommand { get; }

    private static readonly string[] StageNames = { "IF", "DEC/OF", "EX", "MEM", "WB" };

    public MainViewModel()
    {
        _ctrl = new PipelineController(_state);

        for (int i = 0; i < 5; i++)
            PipelineSlots.Add(new PipelineSlotViewModel { StageName = StageNames[i] });

        for (int i = 0; i < 32; i++)
            Registers.Add(new RegisterViewModel(i));

        NextClockCommand  = new RelayCommand(OnNextClock,  () => _programLoaded && !_ctrl.Halted);
        LoadProgramCommand = new RelayCommand(OnLoadProgram);
        ResetCommand      = new RelayCommand(OnReset);
    }

    private void OnLoadProgram()
    {
        try
        {
            var result = _asm.Assemble(ProgramSource);
            _ctrl.LoadProgram(result);
            _programLoaded = true;
            StatusMessage = $"Program incarcat: {result.Instructions.Count} instructiuni.";
            RefreshUI();
            RefreshMemoryWindow();
        }
        catch (AssemblerException ex)
        {
            StatusMessage = $"Eroare assembler: {ex.Message}";
            _programLoaded = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Eroare: {ex.Message}";
        }
    }

    private void OnNextClock()
    {
        _ctrl.Tick();
        RefreshUI();
        RefreshMemoryWindow();

        var fwds = _ctrl.LastForwardings;
        string fwdMsg = fwds.Count > 0
            ? " | Forwarding: " + string.Join(", ", fwds.Select(f => $"R{f.Register}:{f.Value} (stagiu {f.FromStage}→{f.ToStage})"))
            : "";
        string stallMsg = _ctrl.LastCycleHadStall ? " | STALL (NOP inserat)" : "";

        StatusMessage = $"Tact {_ctrl.CycleCount}{stallMsg}{fwdMsg}";
        if (_ctrl.Halted) StatusMessage += " | HALT";
    }

    private void OnReset()
    {
        _ctrl.LoadProgram(new AssemblerResult());
        _programLoaded = false;
        StatusMessage = "Reset. Reincarcati programul.";
        RefreshUI();
        MemoryRows.Clear();
    }

    private void RefreshUI()
    {
        PC  = _state.PC;
        MAR = _state.MAR;
        MDR = _state.MDR;
        IR  = _state.IR;
        RegA = _state.A;
        RegB = _state.B;
        RegC = _state.C;
        CycleCount = _ctrl.CycleCount;

        var fwdRegs = _ctrl.LastForwardings.Select(f => f.Register).ToHashSet();

        for (int i = 0; i < 5; i++)
        {
            var slot = _state.Slots[i];
            var vm = PipelineSlots[i];
            vm.InstructionLabel = slot.Instruction?.ToString() ?? "NOP";
            vm.IsStall = slot.IsStall;
            vm.HasForwarding = false;
        }

        foreach (var fwd in _ctrl.LastForwardings)
            PipelineSlots[fwd.ToStage].HasForwarding = true;

        for (int i = 0; i < 32; i++)
        {
            Registers[i].Value   = _state.Registers.GetValue(i);
            Registers[i].IsValid = _state.Registers.GetValid(i);
        }
    }

    private void RefreshMemoryWindow()
    {
        MemoryRows.Clear();
        int start = _state.PC;
        for (int i = 0; i < 32; i++)
        {
            int addr = start + i;
            int val  = _state.Memory.Read(addr);
            if (val != 0)
                MemoryRows.Add(new MemoryRowViewModel { Address = addr, Value = val });
        }
    }
}

public class MemoryRowViewModel
{
    public int Address { get; set; }
    public int Value   { get; set; }
    public string AddressHex => $"0x{Address:X4}";
    public string ValueHex   => $"0x{Value:X4}";
}
