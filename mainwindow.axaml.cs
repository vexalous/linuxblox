using Avalonia.Controls;
using linuxblox.viewmodels;

namespace linuxblox;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainWindowViewModel();
    }
}
