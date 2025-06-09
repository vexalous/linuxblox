using ReactiveUI;

namespace linuxblox.viewmodels
{
    public abstract class FlagViewModel : ReactiveObject
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        public abstract string GetValue();
    }

    public class ToggleFlagViewModel : FlagViewModel
    {
        private bool _isOn;
        public bool IsOn
        {
            get => _isOn;
            set => this.RaiseAndSetIfChanged(ref _isOn, value);
        }

        public override string GetValue() => IsOn ? "True" : "False";
    }

    public class InputFlagViewModel : FlagViewModel
    {
        private string _value = "";
        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public override string GetValue() => Value;
    }
}
