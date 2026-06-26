using System.Globalization;
using System.Windows.Data;

namespace RiscEmulator.UI.ViewModels;

public class BoolToFlagConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        string name = parameter?.ToString() ?? "?";
        return flag ? name : "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
