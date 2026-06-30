using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiscEmulator.UI.ViewModels;

public class CacheBlockViewModel : BaseViewModel
{
    private int _index;
    private bool _valid;
    private int _tag;
    private string _dataPreview = string.Empty;

    public int Index { get => _index; set => Set(ref _index, value); }

    public bool Valid
    {
        get => _valid;
        set
        {
            if (Set(ref _valid, value))
                OnPropertyChanged(nameof(ValidLabel));
        }
    }

    public int Tag
    {
        get => _tag;
        set
        {
            if (Set(ref _tag, value))
                OnPropertyChanged(nameof(TagHex));
        }
    }

    public string DataPreview { get => _dataPreview; set => Set(ref _dataPreview, value); }

    public string TagHex => $"0x{Tag:X4}";
    public string ValidLabel => Valid ? "V" : "-";
}
