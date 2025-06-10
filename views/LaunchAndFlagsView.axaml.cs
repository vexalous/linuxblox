using Avalonia.Controls;
using Avalonia.ReactiveUI;
using LinuxBlox.ViewModels;

namespace LinuxBlox.Views
{
    public partial class LaunchAndFlagsView : ReactiveUserControl<MainWindowViewModel>
    {
        public LaunchAndFlagsView()
        {
            InitializeComponent();
        }
    }
}
