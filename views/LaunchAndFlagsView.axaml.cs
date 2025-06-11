using Avalonia.ReactiveUI;
using LinuxBlox.ViewModels;
using Avalonia.Markup.Xaml;

namespace LinuxBlox.Views
{
    public partial class LaunchAndFlagsView : ReactiveUserControl<MainWindowViewModel>
    {
        public LaunchAndFlagsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
