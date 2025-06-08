using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI; // <-- This is the missing line that fixes the error.
using linuxblox.viewmodels;
using ReactiveUI;
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
        // This line registers the activation helper for Avalonia.
        Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
