using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Toren.App.Execution.ViewModels;
using Toren.Core.Navigation.Contracts;
using Toren.DotNet.Execution.Models;

namespace Toren.App.Execution.Services;

internal sealed class WorkspaceRunBrowserController
{
    private readonly Window _window;
    private readonly WorkspaceExecutionViewModel _execution;
    private readonly AspNetLaunchUriResolver _launchUriResolver;
    private readonly IExternalUriLauncher _uriLauncher;
    private readonly Action<string> _setStatus;
    private bool _launchAttempted;
    private bool _detached;

    private WorkspaceRunBrowserController(
        Window window,
        WorkspaceExecutionViewModel execution,
        AspNetLaunchUriResolver launchUriResolver,
        IExternalUriLauncher uriLauncher,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));
        _launchUriResolver = launchUriResolver ?? throw new ArgumentNullException(nameof(launchUriResolver));
        _uriLauncher = uriLauncher ?? throw new ArgumentNullException(nameof(uriLauncher));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));

        _execution.PropertyChanged += Execution_OnPropertyChanged;
        _execution.OutputLines.CollectionChanged += OutputLines_OnCollectionChanged;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        WorkspaceExecutionViewModel execution,
        AspNetLaunchUriResolver launchUriResolver,
        IExternalUriLauncher uriLauncher,
        Action<string> setStatus)
    {
        _ = new WorkspaceRunBrowserController(
            window,
            execution,
            launchUriResolver,
            uriLauncher,
            setStatus);
    }

    private void Execution_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(WorkspaceExecutionViewModel.CurrentCommandKind)
            && _execution.CurrentCommandKind == DotNetCommandKind.Run)
        {
            _launchAttempted = false;
        }
    }

    private void OutputLines_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (_launchAttempted
            || _execution.CurrentCommandKind != DotNetCommandKind.Run
            || _execution.SelectedLaunchProfile is not { LaunchBrowser: true } launchProfile
            || eventArgs.NewItems is null)
        {
            return;
        }

        foreach (var item in eventArgs.NewItems.OfType<ExecutionOutputLineViewModel>())
        {
            var launchUri = _launchUriResolver.Resolve(launchProfile, item.Text);
            if (launchUri is null)
            {
                continue;
            }

            _launchAttempted = true;
            var launch = _uriLauncher.Launch(launchUri);
            if (!launch.IsSuccess)
            {
                _setStatus(launch.Error.Message);
            }

            return;
        }
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _execution.PropertyChanged -= Execution_OnPropertyChanged;
        _execution.OutputLines.CollectionChanged -= OutputLines_OnCollectionChanged;
        _window.Closed -= Window_OnClosed;
    }
}
