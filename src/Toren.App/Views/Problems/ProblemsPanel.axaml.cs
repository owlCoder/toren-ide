using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Toren.App.Diagnostics.ViewModels;

namespace Toren.App.Views.Problems;

internal sealed partial class ProblemsPanel : UserControl
{
    public ProblemsPanel()
    {
        InitializeComponent();
    }

    public event Action<ProblemItemViewModel>? ProblemActivated;

    private void ProblemsList_OnTapped(object? sender, TappedEventArgs eventArgs)
    {
        var source = eventArgs.Source as Control;
        var row = source as ListBoxItem ?? source?.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (row?.DataContext is ProblemItemViewModel problem)
        {
            ProblemActivated?.Invoke(problem);
            eventArgs.Handled = true;
        }
    }
    private void ProblemsList_OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && ProblemsList.SelectedItem is ProblemItemViewModel problem)
        {
            ProblemActivated?.Invoke(problem);
            args.Handled = true;
        }
    }

}
