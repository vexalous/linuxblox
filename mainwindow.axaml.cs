using Avalonia.ReactiveUI; // 1. Use the ReactiveUI namespace for Avalonia
using linuxblox.viewmodels;
using System.Reactive.Disposables;

namespace linuxblox;

// 2. Inherit from ReactiveWindow<MainWindowViewModel> instead of just Window
public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    public MainWindow()
    {
        InitializeComponent();

        // 3. Assign an instance of your ViewModel to the 'ViewModel' property
        ViewModel = new MainWindowViewModel();

        // 4. Add the WhenActivated block to trigger the ViewModel's activation
        //    This is the glue that connects the View's lifecycle to the ViewModel.
        this.WhenActivated(disposables => 
        {
            // This block can be used for view-specific logic that should run
            // when the ViewModel is activated. For now, it can be empty.
            // Its presence is what matters.
        });
    }
}
