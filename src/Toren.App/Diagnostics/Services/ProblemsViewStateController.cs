using System.ComponentModel;
using Avalonia.Controls;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Models;
using Toren.App.Diagnostics.ViewModels;

namespace Toren.App.Diagnostics.Services;

internal sealed class ProblemsViewStateController
{
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(150);

    private readonly Window _window;
    private readonly ProblemsViewModel _problems;
    private readonly IProblemsViewStateStore _store;
    private readonly Action<string> _setStatus;
    private CancellationTokenSource? _saveCancellation;
    private bool _initialized;
    private bool _detached;

    private ProblemsViewStateController(
        Window window,
        ProblemsViewModel problems,
        IProblemsViewStateStore store,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _problems = problems ?? throw new ArgumentNullException(nameof(problems));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ProblemsViewModel problems,
        IProblemsViewStateStore store,
        Action<string> setStatus)
    {
        var controller = new ProblemsViewStateController(window, problems, store, setStatus);
        _ = controller.InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var loaded = await _store.LoadAsync().ConfigureAwait(true);
        if (_detached)
        {
            return;
        }

        if (loaded.IsSuccess)
        {
            _problems.ShowErrors = loaded.Value.ShowErrors;
            _problems.ShowWarnings = loaded.Value.ShowWarnings;
            _problems.ShowInfo = loaded.Value.ShowInfo;
        }
        else
        {
            _setStatus(loaded.Error.Message);
        }

        _problems.PropertyChanged += Problems_OnPropertyChanged;
        _initialized = true;
    }

    private void Problems_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!_initialized
            || eventArgs.PropertyName is not nameof(ProblemsViewModel.ShowErrors)
                and not nameof(ProblemsViewModel.ShowWarnings)
                and not nameof(ProblemsViewModel.ShowInfo))
        {
            return;
        }

        QueueSave();
    }

    private void QueueSave()
    {
        _saveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _ = SaveLatestAsync(cancellation);
    }

    private async Task SaveLatestAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(SaveDebounce, cancellation.Token).ConfigureAwait(true);
            var state = new ProblemsViewState(
                _problems.ShowErrors,
                _problems.ShowWarnings,
                _problems.ShowInfo);
            var saved = await _store.SaveAsync(state, cancellation.Token).ConfigureAwait(true);
            if (!saved.IsSuccess && !_detached)
            {
                _setStatus(saved.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_saveCancellation, cancellation))
            {
                _saveCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _initialized = false;
        _problems.PropertyChanged -= Problems_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
    }
}
