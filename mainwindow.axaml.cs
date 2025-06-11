using Avalonia.ReactiveUI;
using LinuxBlox.ViewModels;
using ReactiveUI;

namespace LinuxBlox;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainWindowViewModel();

        this.WhenActivated(disposables => 
        {
        });
    }
}
