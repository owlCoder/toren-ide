using Avalonia.Controls;
using Avalonia.Input;
using Toren.App.Diagnostics.ViewModels;

namespace Toren.App.Views.Problems;

internal sealed partial class ProblemsPanel : UserControl
{
    public ProblemsPanel()
    {
        InitializeComponent();
    }

    public event Action<ProblemItemViewModel>? ProblemActivated;

    private void ProblemsList_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (ProblemsList.SelectedItem is ProblemItemViewModel problem)
        {
            ProblemActivated?.Invoke(problem);
            eventArgs.Handled = true;
        }
    }
}
