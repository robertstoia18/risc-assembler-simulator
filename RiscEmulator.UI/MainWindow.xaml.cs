using System.Windows;
using RiscEmulator.UI.ViewModels;

namespace RiscEmulator.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
