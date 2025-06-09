using ReactiveUI;

namespace linuxblox.viewmodels;

public abstract class FlagViewModel : ReactiveObject
{
    public string Name { get; init; }
    public string Description { get; init; }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    protected FlagViewModel(string name, string description)
    {
        Name = name;
        Description = description;
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

    public ToggleFlagViewModel(string name, string description) : base(name, description)
    {
    }

    public override string GetValue() => IsOn.ToString();
}

public class InputFlagViewModel : FlagViewModel
{
    private string _value = "";
    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public InputFlagViewModel(string name, string description) : base(name, description)
    {
    }

    public override string GetValue() => Value;
}
