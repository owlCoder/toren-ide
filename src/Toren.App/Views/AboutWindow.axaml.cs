using Avalonia.Controls;
using Toren.App.ViewModels;

namespace Toren.App.Views;

public sealed partial class AboutWindow : Window
{
    private AboutWindow(AboutWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        DataContext = viewModel;
        InitializeComponent();
    }

    public static Task ShowAsync(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var window = new AboutWindow(new AboutWindowViewModel());
        return window.ShowDialog(owner);
    }
}
