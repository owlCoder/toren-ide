using CommunityToolkit.Mvvm.ComponentModel;

namespace Toren.App.Debugging.ViewModels;

public sealed partial class DebugWatchItemViewModel(string expression) : ObservableObject
{
    public string Expression { get; } = expression;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string? _type;

    [ObservableProperty]
    private string? _errorMessage;
}
