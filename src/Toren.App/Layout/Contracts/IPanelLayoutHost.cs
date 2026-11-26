using Toren.App.Layout.Models;

namespace Toren.App.Layout.Contracts;

public interface IPanelLayoutHost
{
    PanelLayout CapturePanelLayout();

    void ApplyPanelLayout(PanelLayout layout);
}
