using Avalonia.ReactiveUI;
using LinuxBlox.ViewModels;
using Avalonia.Markup.Xaml; // Added

namespace LinuxBlox.Views
{
    public partial class SettingsView : ReactiveUserControl<MainWindowViewModel>
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
