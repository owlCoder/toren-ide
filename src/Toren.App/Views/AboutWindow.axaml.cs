using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Toren.App.ViewModels;

namespace Toren.App.Views;

internal sealed partial class AboutWindow : Window
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

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        BeginMoveDrag(eventArgs);
    }

    private void CloseWindow_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.Key == Key.Escape && !args.Handled)
        {
            Close();
            args.Handled = true;
        }
    }
}
