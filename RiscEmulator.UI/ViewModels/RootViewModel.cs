namespace RiscEmulator.UI.ViewModels;

public class RootViewModel : BaseViewModel
{
    public MainViewModel Pipeline { get; } = new();
    public ScoreboardViewModel Scoreboard { get; } = new();
    public TomasuloViewModel Tomasulo { get; } = new();
    public OooViewModel Ooo { get; } = new();
}
