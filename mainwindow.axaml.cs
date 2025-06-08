using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using linuxblox.viewmodels;
using ReactiveUI; // 1. Add these two using statements
using Splat;

namespace linuxblox;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // 2. Add this line to register the activation helper.
        // This teaches ReactiveUI how to handle activation for Avalonia views.
        Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                // The DataContext is now set by the ReactiveWindow base class, so we can remove it from here.
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            // This part is for mobile; adjust if needed.
            singleViewPlatform.MainView = new MainWindow
            {
                // The DataContext is now set by the ReactiveWindow base class.
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
