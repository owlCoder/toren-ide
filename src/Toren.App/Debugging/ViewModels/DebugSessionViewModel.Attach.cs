using CommunityToolkit.Mvvm.ComponentModel;

namespace Toren.App.Debugging.ViewModels;

public sealed partial class DebugSessionViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAttach))]
    private string _attachProcessId = string.Empty;

    public bool CanAttach =>
        int.TryParse(AttachProcessId, out var processId) && processId > 0;

    public async Task AttachAsync(CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(AttachProcessId, out var processId) || processId <= 0)
        {
            StatusText = "Enter a valid process id.";
            return;
        }

        StatusText = $"Attaching to process {processId}…";
        var attached = await _coordinator.AttachAsync(processId, cancellationToken).ConfigureAwait(true);
        if (attached.IsFailure)
        {
            StatusText = attached.Error.Message;
            return;
        }

        AttachProcessId = string.Empty;
        RefreshSessionState();
        StatusText = $"Attached to process {processId}.";
    }
}
