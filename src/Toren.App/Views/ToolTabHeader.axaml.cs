using Avalonia.Controls;
using Avalonia.Media;

namespace Toren.App.Views;

internal sealed partial class ToolTabHeader : UserControl
{
    public ToolTabHeader(Window window, string label, string iconResource)
    {
        InitializeComponent();
        Label.Text = label;
        if (window.TryFindResource(iconResource, out var resource) && resource is Geometry geometry)
        {
            Icon.Data = geometry;
        }
    }
}
