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
    private readonly IDotNetSdkResolver _dotNetSdkResolver;
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
    [NotifyPropertyChangedFor(nameof(IsWelcomeContentVisible))]
    private bool _isWelcomeOpen = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcomeContentVisible))]
    private bool _isWelcomeSelected = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecentEmpty))]
    private bool _hasRecentWorkspaces;

    public MainWindowViewModel(
        IDotNetEnvironmentService dotNetEnvironmentService,
        IDotNetSdkResolver dotNetSdkResolver,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceTreeService workspaceTreeService,
        IRecentWorkspaceStore recentWorkspaceStore,
        DocumentHostViewModel documents)
    {
        _dotNetEnvironmentService = dotNetEnvironmentService ?? throw new ArgumentNullException(nameof(dotNetEnvironmentService));
        _dotNetSdkResolver = dotNetSdkResolver ?? throw new ArgumentNullException(nameof(dotNetSdkResolver));
        _workspaceClassifier = workspaceClassifier ?? throw new ArgumentNullException(nameof(workspaceClassifier));
        _recentWorkspaceStore = recentWorkspaceStore ?? throw new ArgumentNullException(nameof(recentWorkspaceStore));
        Documents = documents ?? throw new ArgumentNullException(nameof(documents));
        Explorer = new ExplorerViewModel(workspaceTreeService);
    }

    public ObservableCollection<DotNetSdkInfo> InstalledSdks { get; } = new();

    public ObservableCollection<WorkspaceDescriptor> RecentWorkspaces { get; } = new();

    public ExplorerViewModel Explorer { get; }

    public DocumentHostViewModel Documents { get; }

    public bool IsWelcomeClosed => !IsWelcomeOpen;

    public bool IsWelcomeContentVisible => IsWelcomeOpen && IsWelcomeSelected;

    public bool IsRecentEmpty => !HasRecentWorkspaces;

    public string PlatformSummary => _platformSummary;

    public void CloseWelcome()
    {
        IsWelcomeOpen = false;
        IsWelcomeSelected = false;
        if (Documents.ActiveDocument is null && Documents.OpenDocuments.Count > 0)
        {
            Documents.Activate(Documents.OpenDocuments[^1]);
        }
    }

    public void ActivateWelcome()
    {
        if (!IsWelcomeOpen)
        {
            return;
        }

        Documents.Deactivate();
        IsWelcomeSelected = true;
    }

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
                await ExpandRootAsync(cancellationToken).ConfigureAwait(true);
                await RefreshWorkspaceSdkAsync(workspace, cancellationToken).ConfigureAwait(true);
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

    public async Task OpenDocumentAsync(
        WorkspaceNodeViewModel node,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Node.Kind is not WorkspaceNodeKind.File and not WorkspaceNodeKind.SymbolicLink)
        {
            return;
        }

        var opened = await Documents.OpenAsync(node.Node.Path, cancellationToken).ConfigureAwait(true);
        if (!opened.IsSuccess)
        {
            StatusText = opened.Error.Message;
            return;
        }

        IsWelcomeSelected = false;
        StatusText = $"Opened {opened.Value.Title}";
    }

    public void ActivateDocument(OpenDocumentViewModel document)
    {
        Documents.Activate(document);
        IsWelcomeSelected = false;
    }

    public void CloseDocument(OpenDocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Documents.TryClose(document))
        {
            StatusText = document.IsDirty
                ? $"Save changes to {document.Title} before closing it."
                : $"Could not close {document.Title}.";
            return;
        }

        StatusText = $"Closed {document.Title}";
        if (Documents.ActiveDocument is null && IsWelcomeOpen)
        {
            IsWelcomeSelected = true;
        }
    }

    public async Task SaveActiveDocumentAsync(CancellationToken cancellationToken = default)
    {
        var saved = await Documents.SaveActiveAsync(cancellationToken).ConfigureAwait(true);
        StatusText = saved.IsSuccess
            ? $"Saved {saved.Value.Title}"
            : saved.Error.Message;
    }

    private async Task<bool> OpenWorkspaceAsync(WorkspaceDescriptor workspace, CancellationToken cancellationToken)
    {
        if (!ActivateWorkspace(workspace, closeWelcome: true))
        {
            return false;
        }

        await ExpandRootAsync(cancellationToken).ConfigureAwait(true);
        await RefreshWorkspaceSdkAsync(workspace, cancellationToken).ConfigureAwait(true);

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
            IsWelcomeSelected = false;
        }
        StatusText = $"Opened {workspace.Kind}: {workspace.Path}";
        return true;
    }

    private async Task RefreshWorkspaceSdkAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken)
    {
        var resolved = await _dotNetSdkResolver
            .ResolveVersionAsync(GetWorkspaceDirectory(workspace), cancellationToken)
            .ConfigureAwait(true);
        if (resolved.IsSuccess)
        {
            SdkSummary = $".NET SDK: {resolved.Value}";
        }
        else
        {
            SdkSummary = ".NET SDK: unresolved";
            StatusText = resolved.Error.Message;
        }
    }

    private static string GetWorkspaceDirectory(WorkspaceDescriptor workspace)
    {
        var fullPath = Path.GetFullPath(workspace.Path);
        return workspace.Kind == WorkspaceKind.Folder
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
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

    private async Task ExpandRootAsync(CancellationToken cancellationToken)
    {
        var root = Explorer.Roots[0];
        // Start loading before expansion so the routed UI event cannot start a duplicate request.
        var load = ExpandNodeAsync(root, cancellationToken);
        root.IsExpanded = true;
        await load.ConfigureAwait(true);
    }
}
