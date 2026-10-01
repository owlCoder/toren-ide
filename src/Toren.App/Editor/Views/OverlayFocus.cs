using Avalonia.Controls;
using Avalonia.Threading;

namespace Toren.App.Editor.Views;

internal static class OverlayFocus
{
    public static void Schedule(Control overlay, Control target, bool selectAll = false)
    {
        // Hidden overlays need a layout pass before their input can accept focus.
        Dispatcher.UIThread.Post(() =>
        {
            var focusTarget = target is ListBox list && list.SelectedIndex >= 0
                ? list.ContainerFromIndex(list.SelectedIndex) ?? target
                : target;
            if (!overlay.IsEffectivelyVisible || !focusTarget.Focus())
            {
                return;
            }

            if (selectAll && target is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }, DispatcherPriority.Loaded);
    }
}
