using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.DotNet.Environment.Contracts;
using Toren.DotNet.Environment.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly IDotNetEnvironmentService _dotNetEnvironmentService;
    private readonly IWorkspaceClassifier _workspaceClassifier;
    private readonly IRecentWorkspaceStore _recentWorkspaceStore;
    private readonly string _platformSummary = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows"
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
        : RuntimeInformation.OSDescription;

    [ObservableProperty]
    private string _workspaceTitle = "No workspace open";

    [ObservableProperty]
    private string _workspacePath = "Open a folder, .sln, .slnx, or .csproj file to begin.";

    [ObservableProperty]
    private string _sdkSummary = ".NET SDK: detecting...";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomeClosed))]
    private bool _isWelcomeOpen = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecentEmpty))]
    private bool _hasRecentWorkspaces;

    public MainWindowViewModel(
        IDotNetEnvironmentService dotNetEnvironmentService,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceTreeService workspaceTreeService,
        IRecentWorkspaceStore recentWorkspaceStore)
    {
        _dotNetEnvironmentService = dotNetEnvironmentService ?? throw new ArgumentNullException(nameof(dotNetEnvironmentService));
        _workspaceClassifier = workspaceClassifier ?? throw new ArgumentNullException(nameof(workspaceClassifier));
        _recentWorkspaceStore = recentWorkspaceStore ?? throw new ArgumentNullException(nameof(recentWorkspaceStore));
        Explorer = new ExplorerViewModel(workspaceTreeService);
    }

    public ObservableCollection<DotNetSdkInfo> InstalledSdks { get; } = new();

    public ObservableCollection<WorkspaceDescriptor> RecentWorkspaces { get; } = new();

    public ExplorerViewModel Explorer { get; }

    public bool IsWelcomeClosed => !IsWelcomeOpen;

    public bool IsRecentEmpty => !HasRecentWorkspaces;

    public string PlatformSummary => _platformSummary;

    public void CloseWelcome() => IsWelcomeOpen = false;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var sdkResult = await _dotNetEnvironmentService
            .GetInstalledSdksAsync(cancellationToken)
            .ConfigureAwait(true);

        if (!sdkResult.IsSuccess)
        {
            SdkSummary = ".NET SDK: unavailable";
            StatusText = sdkResult.Error.Message;
        }
        else
        {
            InstalledSdks.Clear();
            foreach (var sdk in sdkResult.Value)
            {
                InstalledSdks.Add(sdk);
            }

            SdkSummary = sdkResult.Value.Count == 0
                ? ".NET SDK: not found"
                : $".NET SDK: {sdkResult.Value[^1].Version}";
        }

        var recent = await _recentWorkspaceStore.LoadAsync(cancellationToken).ConfigureAwait(true);
        if (!recent.IsSuccess)
        {
            StatusText = recent.Error.Message;
            return;
        }

        SetRecentWorkspaces(recent.Value);
        foreach (var workspace in recent.Value)
        {
            if (ActivateWorkspace(workspace, closeWelcome: false))
            {
                StatusText = $"Restored {workspace.DisplayName}";
                break;
            }
        }
    }

    public async Task OpenDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        await OpenWorkspaceAsync(_workspaceClassifier.ClassifyDirectory(path), cancellationToken).ConfigureAwait(true);
    }

    public async Task OpenWorkspaceFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!_workspaceClassifier.TryClassifyFile(path, out var descriptor) || descriptor is null)
        {
            StatusText = "The selected file is not a supported .NET workspace.";
            return;
        }

        await OpenWorkspaceAsync(descriptor, cancellationToken).ConfigureAwait(true);
    }

    public async Task OpenRecentAsync(WorkspaceDescriptor workspace, CancellationToken cancellationToken = default)
    {
        await OpenWorkspaceAsync(workspace, cancellationToken).ConfigureAwait(true);
    }

    public void SetStatus(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        StatusText = message;
    }

    public async Task ExpandNodeAsync(WorkspaceNodeViewModel node, CancellationToken cancellationToken = default)
    {
        var result = await Explorer.ExpandAsync(node, cancellationToken).ConfigureAwait(true);
        if (result.IsFailure)
        {
            StatusText = result.Error.Message;
        }
    }

    private async Task<bool> OpenWorkspaceAsync(WorkspaceDescriptor workspace, CancellationToken cancellationToken)
    {
        if (!ActivateWorkspace(workspace, closeWelcome: true))
        {
            return false;
        }

        var recorded = await _recentWorkspaceStore.RecordAsync(workspace, cancellationToken).ConfigureAwait(true);
        if (recorded.IsSuccess)
        {
            SetRecentWorkspaces(recorded.Value);
        }
        else
        {
            StatusText = recorded.Error.Message;
        }

        return true;
    }

    private bool ActivateWorkspace(WorkspaceDescriptor workspace, bool closeWelcome)
    {
        var result = Explorer.Open(workspace);
        if (!result.IsSuccess)
        {
            StatusText = result.Error.Message;
            return false;
        }

        WorkspaceTitle = workspace.DisplayName;
        WorkspacePath = workspace.Path;
        if (closeWelcome)
        {
            IsWelcomeOpen = false;
        }
        StatusText = $"Opened {workspace.Kind}: {workspace.Path}";
        return true;
    }

    private void SetRecentWorkspaces(IReadOnlyList<WorkspaceDescriptor> workspaces)
    {
        RecentWorkspaces.Clear();
        foreach (var workspace in workspaces)
        {
            RecentWorkspaces.Add(workspace);
        }

        HasRecentWorkspaces = RecentWorkspaces.Count > 0;
    }
}
