using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Toren.App.Views;

internal sealed partial class ToolDialog : Window
{
    public ToolDialog(string title, Control content, double width, double height)
    {
        InitializeComponent();
        Title = title;
        DialogTitle.Text = title;
        var iconKey = title == "Packages" ? "TorenIconProject" : "TorenIconSettings";
        if (this.TryFindResource(iconKey, out var icon) && icon is Geometry geometry)
        {
            DialogIcon.Data = geometry;
        }
        DialogContent.Content = content;
        Width = width;
        Height = height;
        MinWidth = 640;
        MinHeight = 480;
        Closed += (_, _) => DialogContent.Content = null;
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(args);
        }
    }

    private void Close_OnClick(object? sender, RoutedEventArgs args) => Close();

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
