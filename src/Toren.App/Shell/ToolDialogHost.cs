using Avalonia.Controls;
using Avalonia.Threading;
using Toren.App.Views;

namespace Toren.App.Shell;

internal sealed class ToolDialogHost
{
    private readonly Window _owner;
    private readonly string _title;
    private readonly Control _content;
    private readonly string? _focusTarget;
    private double _width;
    private double _height;
    private ToolDialog? _dialog;

    public ToolDialogHost(Window owner, string title, Control content, double width, double height, string? focusTarget = null)
    {
        _owner = owner;
        _title = title;
        _content = content;
        _focusTarget = focusTarget;
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

    public bool IsOpen => _dialog is not null;

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
        _dialog.Closing += (_, _) =>
        {
            _width = _dialog!.Bounds.Width;
            _height = _dialog.Bounds.Height;
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

        var openedDialog = _dialog;
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(_dialog, openedDialog)) return;
            var search = _focusTarget is null ? null : _content.FindControl<TextBox>(_focusTarget);
            search?.Focus();
            search?.SelectAll();
        }, DispatcherPriority.Loaded);
    }
}
