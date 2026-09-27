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

    public MainWindowViewModel(
        IDotNetEnvironmentService dotNetEnvironmentService,
        IWorkspaceClassifier workspaceClassifier)
    {
        _dotNetEnvironmentService = dotNetEnvironmentService ?? throw new ArgumentNullException(nameof(dotNetEnvironmentService));
        _workspaceClassifier = workspaceClassifier ?? throw new ArgumentNullException(nameof(workspaceClassifier));
    }

    public ObservableCollection<DotNetSdkInfo> InstalledSdks { get; } = new();

    public bool IsWelcomeClosed => !IsWelcomeOpen;

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
            return;
        }

        InstalledSdks.Clear();
        foreach (var sdk in sdkResult.Value)
        {
            InstalledSdks.Add(sdk);
        }

        SdkSummary = sdkResult.Value.Count == 0
            ? ".NET SDK: not found"
            : $".NET SDK: {sdkResult.Value[^1].Version}";
    }

    public void OpenDirectory(string path)
    {
        OpenWorkspace(_workspaceClassifier.ClassifyDirectory(path));
    }

    public bool TryOpenWorkspaceFile(string path)
    {
        if (!_workspaceClassifier.TryClassifyFile(path, out var descriptor) || descriptor is null)
        {
            return false;
        }

        OpenWorkspace(descriptor);
        return true;
    }

    public void SetStatus(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        StatusText = message;
    }

    private void OpenWorkspace(WorkspaceDescriptor workspace)
    {
        WorkspaceTitle = workspace.DisplayName;
        WorkspacePath = workspace.Path;
        StatusText = $"Opened {workspace.Kind}: {workspace.Path}";
    }
}
