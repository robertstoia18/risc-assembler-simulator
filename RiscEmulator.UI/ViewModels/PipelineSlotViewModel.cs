namespace RiscEmulator.UI.ViewModels;

public class PipelineSlotViewModel : BaseViewModel
{
    private string _stageName = string.Empty;
    private string _instructionLabel = "NOP";
    private bool _isStall;
    private bool _hasForwarding;

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
}
