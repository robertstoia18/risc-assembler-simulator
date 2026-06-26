namespace RiscEmulator.UI.ViewModels;

public class RegisterViewModel : BaseViewModel
{
    private int _value;
    private bool _isValid = true;

    public int Index { get; }
    public string Label => $"R{Index}";

    public int Value
    {
        get => _value;
        set { Set(ref _value, value); OnPropertyChanged(nameof(DisplayValue)); }
    }

    public bool IsValid
    {
        get => _isValid;
        set { Set(ref _isValid, value); OnPropertyChanged(nameof(StatusLabel)); }
    }

    public string DisplayValue => $"{_value} (0x{_value:X4})";
    public string StatusLabel => _isValid ? "V" : "B";

    public RegisterViewModel(int index)
    {
        Index = index;
    }
}
