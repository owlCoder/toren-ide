using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Toren.App.Terminal.ViewModels;

public sealed partial class TerminalHostViewModel : ObservableObject, IAsyncDisposable
{
    private string _workingDirectory;
    private int _nextSessionNumber = 2;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSession))]
    [NotifyPropertyChangedFor(nameof(CanCloseSession))]
    private int _selectedSessionIndex;

    public TerminalHostViewModel(TerminalViewModel initialSession)
    {
        ArgumentNullException.ThrowIfNull(initialSession);
        _workingDirectory = initialSession.WorkingDirectory;
        Sessions.Add(initialSession);
    }

    public ObservableCollection<TerminalViewModel> Sessions { get; } = new();

    public TerminalViewModel? SelectedSession =>
        SelectedSessionIndex >= 0 && SelectedSessionIndex < Sessions.Count
            ? Sessions[SelectedSessionIndex]
            : null;

    public bool CanCloseSession => Sessions.Count > 1 && SelectedSession is not null;

    public void SetWorkingDirectory(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        _workingDirectory = Path.GetFullPath(workingDirectory);
        foreach (var session in Sessions)
        {
            session.SetWorkingDirectory(_workingDirectory);
        }
    }

    public TerminalViewModel AddSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var template = Sessions[0];
        var session = template.CreateSibling($"Terminal {_nextSessionNumber++}");
        session.SetWorkingDirectory(_workingDirectory);
        Sessions.Add(session);
        SelectedSessionIndex = Sessions.Count - 1;
        OnPropertyChanged(nameof(CanCloseSession));
        return session;
    }

    public async Task CloseSelectedSessionAsync()
    {
        if (!CanCloseSession || SelectedSession is not { } session)
        {
            return;
        }

        var index = SelectedSessionIndex;
        Sessions.RemoveAt(index);
        await session.DisposeAsync().ConfigureAwait(true);
        SelectedSessionIndex = Math.Min(index, Sessions.Count - 1);
        OnPropertyChanged(nameof(CanCloseSession));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var sessions = Sessions.ToArray();
        Sessions.Clear();
        SelectedSessionIndex = -1;
        foreach (var session in sessions)
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }
    }
}
