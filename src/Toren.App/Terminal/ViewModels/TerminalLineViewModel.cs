namespace Toren.App.Terminal.ViewModels;

public sealed record TerminalLineViewModel(
    string Text,
    bool IsError = false,
    bool IsCommand = false);
