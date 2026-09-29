using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Toren.App.Execution.Contracts;
using Toren.App.Execution.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Output;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Execution.Services;

internal sealed class WorkspaceExecutionController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly IWorkspaceClassifier _workspaceClassifier;
    private readonly IWorkspaceExecutionTargetService _targetService;
    private readonly IDotNetLaunchProfileProvider _launchProfileProvider;
    private readonly WorkspaceExecutionViewModel _execution;
    private CancellationTokenSource? _targetLoadCancellation;
    private CancellationTokenSource? _launchProfileLoadCancellation;
    private ComboBox? _configurationSelector;
    private ComboBox? _runTargetSelector;
    private Button? _runButton;
    private TextBlock? _commandStatus;
    private bool _updatingCommandBar;
    private bool _detached;

    private WorkspaceExecutionController(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceExecutionTargetService targetService,
        IDotNetLaunchProfileProvider launchProfileProvider,
        WorkspaceExecutionViewModel execution)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _workspaceClassifier = workspaceClassifier
            ?? throw new ArgumentNullException(nameof(workspaceClassifier));
        _targetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
        _launchProfileProvider = launchProfileProvider
            ?? throw new ArgumentNullException(nameof(launchProfileProvider));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));

        InstallOutputPanel();
        InstallCommandBar();
        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _execution.PropertyChanged += Execution_OnPropertyChanged;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
        SynchronizeWorkspace();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceExecutionTargetService targetService,
        IDotNetLaunchProfileProvider launchProfileProvider,
        WorkspaceExecutionViewModel execution)
    {
        _ = new WorkspaceExecutionController(
            window,
            shell,
            workspaceClassifier,
            targetService,
            launchProfileProvider,
            execution);
    }

    private void InstallOutputPanel()
    {
        var toolTabs = _window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        var outputTab = toolTabs?.Items.OfType<TabItem>().Skip(1).FirstOrDefault();
        if (outputTab is null)
        {
            return;
        }

        outputTab.Content = new OutputPanel
        {
            DataContext = _execution,
        };
    }

    private void InstallCommandBar()
    {
        var commandBar = _window.GetLogicalDescendants()
            .OfType<Border>()
            .FirstOrDefault(control => control.Classes.Contains("command-bar"));
        if (commandBar is null)
        {
            return;
        }

        var selectors = commandBar.GetLogicalDescendants()
            .OfType<ComboBox>()
            .Where(control => control.Classes.Contains("command-select"))
            .Take(2)
            .ToArray();
        if (selectors.Length == 2)
        {
            _configurationSelector = selectors[0];
            _runTargetSelector = selectors[1];
            _configurationSelector.SelectionChanged += ConfigurationSelector_OnSelectionChanged;
            _runTargetSelector.SelectionChanged += RunTargetSelector_OnSelectionChanged;
            ToolTip.SetTip(_configurationSelector, "Build configuration");
            ToolTip.SetTip(_runTargetSelector, "Startup project");
            AutomationProperties.SetName(_configurationSelector, "Build configuration");
            AutomationProperties.SetName(_runTargetSelector, "Startup project");
        }

        _runButton = commandBar.GetLogicalDescendants()
            .OfType<Button>()
            .FirstOrDefault(control => control.Classes.Contains("run"));
        if (_runButton is not null)
        {
            _runButton.Click += RunButton_OnClick;
            ToolTip.SetTip(_runButton, "Run selected startup project (F5)");
            AutomationProperties.SetName(_runButton, "Run selected startup project");
        }

        _commandStatus = commandBar.GetLogicalDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(control => string.Equals(control.Text, "Build / Run · M3", StringComparison.Ordinal));

        UpdateCommandBarState();
    }

    private void Shell_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.WorkspacePath))
        {
            SynchronizeWorkspace();
        }
    }

    private void Explorer_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ExplorerViewModel.IsWorkspaceOpen))
        {
            SynchronizeWorkspace();
        }
    }

    private void Execution_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(WorkspaceExecutionViewModel.SelectedRunTargetIndex))
        {
            SynchronizeLaunchProfiles();
        }

        if (eventArgs.PropertyName is nameof(WorkspaceExecutionViewModel.ConfigurationIndex)
            or nameof(WorkspaceExecutionViewModel.SelectedRunTargetIndex)
            or nameof(WorkspaceExecutionViewModel.IsRunning)
            or nameof(WorkspaceExecutionViewModel.StatusText)
            or nameof(WorkspaceExecutionViewModel.CanExecute)
            or nameof(WorkspaceExecutionViewModel.CanRun))
        {
            UpdateCommandBarState();
        }
    }

    private void UpdateCommandBarState()
    {
        _updatingCommandBar = true;
        try
        {
            if (_configurationSelector is not null)
            {
                _configurationSelector.IsEnabled = _execution.CanExecute;
                _configurationSelector.SelectedIndex = _execution.ConfigurationIndex;
            }

            if (_runTargetSelector is not null)
            {
                _runTargetSelector.IsEnabled = _execution.CanExecute && _execution.HasRunTargets;
                _runTargetSelector.ItemsSource = _execution.RunTargets
                    .Select(static target => target.DisplayName)
                    .ToArray();
                _runTargetSelector.SelectedIndex = _execution.SelectedRunTargetIndex;
            }

            if (_runButton is not null)
            {
                _runButton.IsEnabled = _execution.CanRun;
            }

            if (_commandStatus is not null)
            {
                _commandStatus.Text = _execution.StatusText;
            }
        }
        finally
        {
            _updatingCommandBar = false;
        }
    }

    private void ConfigurationSelector_OnSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (_updatingCommandBar || _configurationSelector is null)
        {
            return;
        }

        _execution.ConfigurationIndex = _configurationSelector.SelectedIndex == 1 ? 1 : 0;
    }

    private void RunTargetSelector_OnSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (_updatingCommandBar || _runTargetSelector is null)
        {
            return;
        }

        _execution.SelectedRunTargetIndex = _runTargetSelector.SelectedIndex;
    }

    private async void RunButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        if (_execution.CanRun)
        {
            await _execution.ExecuteAsync(DotNetCommandKind.Run).ConfigureAwait(true);
        }
    }

    private void SynchronizeWorkspace()
    {
        CancelTargetLoad();
        CancelLaunchProfileLoad();
        var workspace = ResolveWorkspace();
        _execution.SetWorkspace(workspace);
        if (workspace is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _targetLoadCancellation = cancellation;
        _ = LoadRunTargetsAsync(workspace, cancellation);
    }

    private void SynchronizeLaunchProfiles()
    {
        CancelLaunchProfileLoad();
        if (_execution.SelectedRunTarget is not { } target)
        {
            _execution.SetLaunchProfiles([]);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _launchProfileLoadCancellation = cancellation;
        _ = LoadLaunchProfilesAsync(target.ProjectPath, cancellation);
    }

    private WorkspaceDescriptor? ResolveWorkspace()
    {
        if (!_shell.Explorer.IsWorkspaceOpen)
        {
            return null;
        }

        var path = _shell.WorkspacePath;
        if (Directory.Exists(path))
        {
            return _workspaceClassifier.ClassifyDirectory(path);
        }

        return _workspaceClassifier.TryClassifyFile(path, out var workspace)
            ? workspace
            : null;
    }

    private async Task LoadRunTargetsAsync(
        WorkspaceDescriptor workspace,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _targetService
                .GetTargetsAsync(workspace, cancellation.Token)
                .ConfigureAwait(true);
            if (!ReferenceEquals(_targetLoadCancellation, cancellation))
            {
                return;
            }

            if (result.IsSuccess)
            {
                _execution.SetRunTargets(result.Value);
            }
            else
            {
                _execution.SetRunTargetLoadError(result.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer workspace selection superseded this load.
        }
        finally
        {
            if (ReferenceEquals(_targetLoadCancellation, cancellation))
            {
                _targetLoadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async Task LoadLaunchProfilesAsync(
        string projectPath,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _launchProfileProvider
                .GetProfilesAsync(projectPath, cancellation.Token)
                .ConfigureAwait(true);
            if (!ReferenceEquals(_launchProfileLoadCancellation, cancellation))
            {
                return;
            }

            if (result.IsSuccess)
            {
                _execution.SetLaunchProfiles(result.Value);
            }
            else
            {
                _execution.SetLaunchProfileLoadError(result.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer startup-project selection superseded this load.
        }
        finally
        {
            if (ReferenceEquals(_launchProfileLoadCancellation, cancellation))
            {
                _launchProfileLoadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.F5 && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (_execution.CanCancel)
            {
                eventArgs.Handled = true;
                _execution.Cancel();
            }

            return;
        }

        if (eventArgs.Key == Key.F5 && eventArgs.KeyModifiers == KeyModifiers.None)
        {
            if (_execution.CanRun)
            {
                eventArgs.Handled = true;
                await _execution.ExecuteAsync(DotNetCommandKind.Run).ConfigureAwait(true);
            }

            return;
        }

        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!commandModifier
            || !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift)
            || eventArgs.Key != Key.B
            || !_execution.CanExecute)
        {
            return;
        }

        eventArgs.Handled = true;
        await _execution.ExecuteAsync(DotNetCommandKind.Build).ConfigureAwait(true);
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private void CancelTargetLoad()
    {
        _targetLoadCancellation?.Cancel();
        _targetLoadCancellation = null;
    }

    private void CancelLaunchProfileLoad()
    {
        _launchProfileLoadCancellation?.Cancel();
        _launchProfileLoadCancellation = null;
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelTargetLoad();
        CancelLaunchProfileLoad();
        if (_configurationSelector is not null)
        {
            _configurationSelector.SelectionChanged -= ConfigurationSelector_OnSelectionChanged;
        }

        if (_runTargetSelector is not null)
        {
            _runTargetSelector.SelectionChanged -= RunTargetSelector_OnSelectionChanged;
        }

        if (_runButton is not null)
        {
            _runButton.Click -= RunButton_OnClick;
        }

        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _execution.PropertyChanged -= Execution_OnPropertyChanged;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _execution.Dispose();
    }
}
