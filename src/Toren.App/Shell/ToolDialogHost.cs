using Avalonia.Controls;
using Toren.App.Views;

namespace Toren.App.Shell;

internal sealed class ToolDialogHost
{
    private readonly Window _owner;
    private readonly string _title;
    private readonly Control _content;
    private readonly double _width;
    private readonly double _height;
    private ToolDialog? _dialog;

    public ToolDialogHost(Window owner, string title, Control content, double width, double height)
    {
        _owner = owner;
        _title = title;
        _content = content;
        _width = width;
        _height = height;
        owner.Closed += (_, _) => _dialog?.Close();
        owner.ActualThemeVariantChanged += (_, _) =>
        {
            if (_dialog is not null)
            {
                _dialog.RequestedThemeVariant = owner.ActualThemeVariant;
            }
        };
    }

    public void Open()
    {
        if (_dialog is not null)
        {
            _dialog.Activate();
            return;
        }

        _dialog = new ToolDialog(_title, _content, _width, _height)
        {
            RequestedThemeVariant = _owner.ActualThemeVariant,
        };
        _dialog.Closed += (_, _) => _dialog = null;
        if (_owner.IsVisible)
        {
            _dialog.Show(_owner);
        }
        else
        {
            _dialog.Show();
        }
    }
}
