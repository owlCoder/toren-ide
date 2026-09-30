using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.EnvironmentDoctor.Contracts;
using Toren.App.EnvironmentDoctor.Models;

namespace Toren.App.EnvironmentDoctor.ViewModels;

public sealed partial class EnvironmentDoctorViewModel(IEnvironmentDoctorService doctorService) : ObservableObject
{
    private readonly IEnvironmentDoctorService _doctorService = doctorService
        ?? throw new ArgumentNullException(nameof(doctorService));
    private string? _workspacePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRefresh))]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Open a workspace to check the development environment.";

    public ObservableCollection<EnvironmentDoctorCheck> Checks { get; } = new();

    public bool CanRefresh => !IsRunning && !string.IsNullOrWhiteSpace(_workspacePath);

    public void SetWorkspace(string? workspacePath)
    {
        var normalized = string.IsNullOrWhiteSpace(workspacePath) ? null : Path.GetFullPath(workspacePath);
        if (string.Equals(_workspacePath, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _workspacePath = normalized;
        Checks.Clear();
        StatusText = normalized is null
            ? "Open a workspace to check the development environment."
            : $"Environment Doctor ready for {Path.GetFileName(normalized)}.";
        OnPropertyChanged(nameof(CanRefresh));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_workspacePath is null || IsRunning)
        {
            return;
        }

        IsRunning = true;
        try
        {
            var result = await _doctorService.CheckAsync(_workspacePath, cancellationToken).ConfigureAwait(true);
            Checks.Clear();
            if (result.IsFailure)
            {
                StatusText = result.Error.Message;
                return;
            }

            foreach (var check in result.Value!.Checks)
            {
                Checks.Add(check);
            }

            StatusText = result.Value.IsHealthy
                ? "Environment is healthy."
                : $"Environment check complete: {result.Value.ErrorCount} error(s), {result.Value.WarningCount} warning(s).";
        }
        finally
        {
            IsRunning = false;
        }
    }
}
