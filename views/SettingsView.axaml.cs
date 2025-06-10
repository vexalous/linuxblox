using Avalonia.ReactiveUI;
using LinuxBlox.ViewModels;

namespace LinuxBlox.Views
{
    public partial class SettingsView : ReactiveUserControl<MainWindowViewModel>
    {
        public SettingsView()
        {
            InitializeComponent();
        }
    }
}
