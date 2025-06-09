using ReactiveUI;

namespace linuxblox.viewmodels
{
    public abstract class FlagViewModel : ReactiveObject
    {
        public required string Name { get; init; }
        public required string Description { get; init; }

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

        public override string GetValue() => IsOn.ToString();
    }

    public class InputFlagViewModel : FlagViewModel
    {
        private string _value = "";
        public string InputValue
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }

        public override string GetValue() => InputValue;
    }
}
