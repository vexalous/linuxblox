using Avalonia.ReactiveUI;
using linuxblox.viewmodels;
using ReactiveUI; // <-- THIS IS THE MISSING LINE THAT FIXES THE BUILD ERROR
using System.Reactive.Disposables;

namespace linuxblox;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainWindowViewModel();

        this.WhenActivated(disposables => 
        {
            // This block's presence is what activates the ViewModel.
            // It links the View's lifetime to the ViewModel's activation logic.
        });
    }
}
