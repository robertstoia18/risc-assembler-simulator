namespace RiscEmulator.UI.ViewModels;

public class FunctionalUnitViewModel : BaseViewModel
{
    private string _unitName = string.Empty;
    private PipelineSlotViewModel _ex = new() { StageName = "EX" };
    private PipelineSlotViewModel _mem = new() { StageName = "MEM" };
    private PipelineSlotViewModel _wb = new() { StageName = "WB" };

    public string UnitName
    {
        get => _unitName;
        set => Set(ref _unitName, value);
    }

    public PipelineSlotViewModel EX
    {
        get => _ex;
        set => Set(ref _ex, value);
    }

    public PipelineSlotViewModel MEM
    {
        get => _mem;
        set => Set(ref _mem, value);
    }

    public PipelineSlotViewModel WB
    {
        get => _wb;
        set => Set(ref _wb, value);
    }
}
