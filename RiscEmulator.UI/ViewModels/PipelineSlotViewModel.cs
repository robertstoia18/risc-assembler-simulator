namespace RiscEmulator.UI.ViewModels;

public class PipelineSlotViewModel : BaseViewModel
{
    private string _stageName = string.Empty;
    private string _instructionLabel = "NOP";
    private bool _isStall;
    private bool _hasForwarding;
    private int _a;
    private int _b;
    private int _c;
    private int _mar;

    public string StageName
    {
        get => _stageName;
        set => Set(ref _stageName, value);
    }

    public string InstructionLabel
    {
        get => _instructionLabel;
        set => Set(ref _instructionLabel, value);
    }

    public bool IsStall
    {
        get => _isStall;
        set => Set(ref _isStall, value);
    }

    public bool HasForwarding
    {
        get => _hasForwarding;
        set => Set(ref _hasForwarding, value);
    }

    public int A
    {
        get => _a;
        set { Set(ref _a, value); OnPropertyChanged(nameof(AHex)); }
    }

    public int B
    {
        get => _b;
        set { Set(ref _b, value); OnPropertyChanged(nameof(BHex)); }
    }

    public int C
    {
        get => _c;
        set { Set(ref _c, value); OnPropertyChanged(nameof(CHex)); }
    }

    public int MAR
    {
        get => _mar;
        set { Set(ref _mar, value); OnPropertyChanged(nameof(MARHex)); }
    }

    public string AHex => $"0x{A:X4}";
    public string BHex => $"0x{B:X4}";
    public string CHex => $"0x{C:X4}";
    public string MARHex => $"0x{MAR:X4}";
}