using Avalonia.ReactiveUI;
using linuxblox.viewmodels;
using ReactiveUI;

namespace linuxblox;

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
