using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Containers.Contracts;
using Toren.Containers.Models;
using Toren.Core.Results;
using Toren.DotNet.EntityFramework.Contracts;
using Toren.DotNet.EntityFramework.Models;

namespace Toren.App.DataTools.ViewModels;

public sealed partial class DataToolsViewModel(
    IEfCoreToolService efCoreToolService,
    IDockerComposeService dockerComposeService,
    IDockerComposeFileLocator composeFileLocator) : ObservableObject
{
    private readonly IEfCoreToolService _efCoreToolService = efCoreToolService
        ?? throw new ArgumentNullException(nameof(efCoreToolService));
    private readonly IDockerComposeService _dockerComposeService = dockerComposeService
        ?? throw new ArgumentNullException(nameof(dockerComposeService));
    private readonly IDockerComposeFileLocator _composeFileLocator = composeFileLocator
        ?? throw new ArgumentNullException(nameof(composeFileLocator));
    private string? _projectPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMigration))]
    private int _selectedMigrationIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddMigration))]
    private string _newMigrationName = string.Empty;

    [ObservableProperty]
    private string _migrationOutputDirectory = string.Empty;

    [ObservableProperty]
    private string _databaseTarget = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseDocker))]
    private string _composeFilePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseEf))]
    [NotifyPropertyChangedFor(nameof(CanAddMigration))]
    [NotifyPropertyChangedFor(nameof(CanUseDocker))]
    private bool _isBusy;

    [ObservableProperty]
    private string _efStatusText = "Select a startup project to use EF Core tooling.";

    [ObservableProperty]
    private string _dockerStatusText = "Docker Compose has not been checked.";

    [ObservableProperty]
    private string _dockerLogs = string.Empty;

    public ObservableCollection<EfCoreMigrationInfo> Migrations { get; } = new();

    public EfCoreMigrationInfo? SelectedMigration =>
        SelectedMigrationIndex >= 0 && SelectedMigrationIndex < Migrations.Count
            ? Migrations[SelectedMigrationIndex]
            : null;

    public bool CanUseEf => !IsBusy && !string.IsNullOrWhiteSpace(_projectPath);

    public bool CanAddMigration => CanUseEf && !string.IsNullOrWhiteSpace(NewMigrationName);

    public bool CanUseDocker => !IsBusy && !string.IsNullOrWhiteSpace(ComposeFilePath);

    public void SetProject(string? projectPath)
    {
        var normalized = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        if (string.Equals(_projectPath, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _projectPath = normalized;
        Migrations.Clear();
        SelectedMigrationIndex = -1;
        EfStatusText = normalized is null
            ? "Select a startup project to use EF Core tooling."
            : $"EF Core tooling ready for {Path.GetFileNameWithoutExtension(normalized)}.";
        OnPropertyChanged(nameof(CanUseEf));
        OnPropertyChanged(nameof(CanAddMigration));
    }

    public void SetWorkspace(string? workspacePath)
    {
        DockerLogs = string.Empty;
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            ComposeFilePath = string.Empty;
            DockerStatusText = "Open a workspace to use Docker Compose.";
            return;
        }

        var result = _composeFileLocator.Find(workspacePath);
        ComposeFilePath = result.IsSuccess ? result.Value!.Path ?? string.Empty : string.Empty;
        DockerStatusText = result.IsFailure
            ? result.Error.Message
            : string.IsNullOrWhiteSpace(ComposeFilePath)
                ? "No Compose file was found at the workspace root."
                : $"Compose file: {Path.GetFileName(ComposeFilePath)}";
    }

    public async Task RefreshEfAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var detection = await _efCoreToolService.DetectAsync(_projectPath, cancellationToken)
                .ConfigureAwait(true);
            if (detection.IsFailure)
            {
                EfStatusText = detection.Error.Message;
                return;
            }

            if (!detection.Value!.IsAvailable)
            {
                EfStatusText = detection.Value.Details ?? "dotnet ef is not available.";
                Migrations.Clear();
                SelectedMigrationIndex = -1;
                return;
            }

            await RefreshMigrationsCoreAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AddMigrationAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || !CanAddMigration)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var name = NewMigrationName.Trim();
            var result = await _efCoreToolService.AddMigrationAsync(
                new EfCoreMigrationRequest(
                    _projectPath,
                    name,
                    OutputDirectory: string.IsNullOrWhiteSpace(MigrationOutputDirectory)
                        ? null
                        : MigrationOutputDirectory.Trim()),
                cancellationToken).ConfigureAwait(true);
            EfStatusText = result.IsSuccess ? $"Added migration '{name}'." : result.Error.Message;
            if (result.IsSuccess)
            {
                NewMigrationName = string.Empty;
                await RefreshMigrationsCoreAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RemoveMigrationAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _efCoreToolService.RemoveMigrationAsync(
                new EfCoreProjectRequest(_projectPath),
                cancellationToken).ConfigureAwait(true);
            EfStatusText = result.IsSuccess ? "Removed the latest migration." : result.Error.Message;
            if (result.IsSuccess)
            {
                await RefreshMigrationsCoreAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UpdateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var target = string.IsNullOrWhiteSpace(DatabaseTarget) ? null : DatabaseTarget.Trim();
            var result = await _efCoreToolService.UpdateDatabaseAsync(
                new EfCoreDatabaseUpdateRequest(_projectPath, target),
                cancellationToken).ConfigureAwait(true);
            EfStatusText = result.IsSuccess
                ? target is null
                    ? "Database updated to the latest migration."
                    : $"Database updated to '{target}'."
                : result.Error.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshDockerAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _dockerComposeService.DetectAsync(cancellationToken).ConfigureAwait(true);
            DockerStatusText = result.IsFailure
                ? result.Error.Message
                : result.Value!.IsAvailable
                    ? $"Docker Compose {result.Value.Version ?? "available"}."
                    : result.Value.Details ?? "Docker Compose is not available; Docker is optional.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task UpAsync(CancellationToken cancellationToken = default) =>
        RunDockerMutationAsync(
            "Compose services started.",
            request => _dockerComposeService.UpAsync(request, cancellationToken));

    public Task DownAsync(CancellationToken cancellationToken = default) =>
        RunDockerMutationAsync(
            "Compose services stopped.",
            request => _dockerComposeService.DownAsync(request, cancellationToken));

    public Task BuildAsync(CancellationToken cancellationToken = default) =>
        RunDockerMutationAsync(
            "Compose images built.",
            request => _dockerComposeService.BuildAsync(request, cancellationToken));

    public async Task RefreshLogsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUseDocker)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _dockerComposeService.GetLogsAsync(
                new DockerComposeLogsRequest(ComposeFilePath),
                cancellationToken).ConfigureAwait(true);
            if (result.IsSuccess)
            {
                DockerLogs = result.Value!;
                DockerStatusText = "Compose logs refreshed.";
            }
            else
            {
                DockerStatusText = result.Error.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshMigrationsCoreAsync(CancellationToken cancellationToken)
    {
        if (_projectPath is null)
        {
            return;
        }

        var result = await _efCoreToolService.ListMigrationsAsync(
            new EfCoreProjectRequest(_projectPath),
            cancellationToken).ConfigureAwait(true);
        Migrations.Clear();
        SelectedMigrationIndex = -1;
        if (result.IsFailure)
        {
            EfStatusText = result.Error.Message;
            return;
        }

        foreach (var migration in result.Value!)
        {
            Migrations.Add(migration);
        }

        EfStatusText = Migrations.Count == 0
            ? "EF Core is available; no migrations were found."
            : $"Loaded {Migrations.Count} migration(s).";
    }

    private async Task RunDockerMutationAsync(
        string successMessage,
        Func<DockerComposeRequest, Task<Result<bool>>> action)
    {
        if (!CanUseDocker)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await action(new DockerComposeRequest(ComposeFilePath)).ConfigureAwait(true);
            DockerStatusText = result.IsSuccess ? successMessage : result.Error.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
