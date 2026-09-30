using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Core.Navigation.Contracts;
using Toren.DotNet.AspNetCore.Contracts;
using Toren.DotNet.AspNetCore.Models;
using Toren.DotNet.UserSecrets.Contracts;
using Toren.DotNet.UserSecrets.Models;

namespace Toren.App.AspNetCore.ViewModels;

public sealed partial class AspNetCoreToolsViewModel(
    IDotNetUserSecretsService userSecretsService,
    IHttpsDevelopmentCertificateService certificateService,
    IAspNetApiShortcutResolver apiShortcutResolver,
    IExternalUriLauncher uriLauncher) : ObservableObject
{
    private readonly IDotNetUserSecretsService _userSecretsService = userSecretsService
        ?? throw new ArgumentNullException(nameof(userSecretsService));
    private readonly IHttpsDevelopmentCertificateService _certificateService = certificateService
        ?? throw new ArgumentNullException(nameof(certificateService));
    private readonly IAspNetApiShortcutResolver _apiShortcutResolver = apiShortcutResolver
        ?? throw new ArgumentNullException(nameof(apiShortcutResolver));
    private readonly IExternalUriLauncher _uriLauncher = uriLauncher
        ?? throw new ArgumentNullException(nameof(uriLauncher));
    private string? _projectPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSecret))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSecret))]
    private int _selectedSecretIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveSecret))]
    private string _newSecretKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveSecret))]
    private string _newSecretValue = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanManageSecrets))]
    [NotifyPropertyChangedFor(nameof(CanSaveSecret))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSecret))]
    [NotifyPropertyChangedFor(nameof(CanTrustHttps))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTrustHttps))]
    private HttpsDevelopmentCertificateState _httpsCertificateState = HttpsDevelopmentCertificateState.Missing;

    [ObservableProperty]
    private string _httpsStatusText = "HTTPS development certificate not checked.";

    [ObservableProperty]
    private string _statusText = "Select a startup project to manage ASP.NET Core tooling.";

    public ObservableCollection<UserSecretEntry> Secrets { get; } = new();

    public ObservableCollection<AspNetApiShortcut> ApiShortcuts { get; } = new();

    public UserSecretEntry? SelectedSecret =>
        SelectedSecretIndex >= 0 && SelectedSecretIndex < Secrets.Count
            ? Secrets[SelectedSecretIndex]
            : null;

    public bool CanManageSecrets => !IsBusy && !string.IsNullOrWhiteSpace(_projectPath);

    public bool CanSaveSecret =>
        CanManageSecrets
        && !string.IsNullOrWhiteSpace(NewSecretKey)
        && !string.IsNullOrEmpty(NewSecretValue);

    public bool CanRemoveSecret => CanManageSecrets && SelectedSecret is not null;

    public bool CanTrustHttps => !IsBusy && HttpsCertificateState != HttpsDevelopmentCertificateState.Trusted;

    public void SetProject(string? projectPath)
    {
        var normalized = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        if (string.Equals(_projectPath, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _projectPath = normalized;
        Secrets.Clear();
        SelectedSecretIndex = -1;
        NewSecretKey = string.Empty;
        NewSecretValue = string.Empty;
        StatusText = normalized is null
            ? "Select a startup project to manage ASP.NET Core tooling."
            : $"ASP.NET Core tooling ready for {Path.GetFileNameWithoutExtension(normalized)}.";
        OnPropertyChanged(nameof(CanManageSecrets));
        OnPropertyChanged(nameof(CanSaveSecret));
        OnPropertyChanged(nameof(CanRemoveSecret));
    }

    public void SetApplicationUri(Uri? applicationUri)
    {
        ApiShortcuts.Clear();
        if (applicationUri is null)
        {
            return;
        }

        var result = _apiShortcutResolver.Resolve(applicationUri);
        if (result.IsFailure)
        {
            StatusText = result.Error.Message;
            return;
        }

        foreach (var shortcut in result.Value!)
        {
            ApiShortcuts.Add(shortcut);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await RefreshSecretsCoreAsync(cancellationToken).ConfigureAwait(true);
            await RefreshHttpsCoreAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task InitializeSecretsAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _userSecretsService.InitializeAsync(_projectPath, cancellationToken)
                .ConfigureAwait(true);
            StatusText = result.IsSuccess ? "User Secrets initialized." : result.Error.Message;
            if (result.IsSuccess)
            {
                await RefreshSecretsCoreAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveSecretAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || !CanSaveSecret)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _userSecretsService
                .SetAsync(_projectPath, NewSecretKey.Trim(), NewSecretValue, cancellationToken)
                .ConfigureAwait(true);
            StatusText = result.IsSuccess ? $"Saved secret '{NewSecretKey.Trim()}'." : result.Error.Message;
            if (result.IsSuccess)
            {
                NewSecretKey = string.Empty;
                NewSecretValue = string.Empty;
                await RefreshSecretsCoreAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RemoveSelectedSecretAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || SelectedSecret is not { } secret || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _userSecretsService.RemoveAsync(_projectPath, secret.Key, cancellationToken)
                .ConfigureAwait(true);
            StatusText = result.IsSuccess ? $"Removed secret '{secret.Key}'." : result.Error.Message;
            if (result.IsSuccess)
            {
                await RefreshSecretsCoreAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ClearSecretsAsync(CancellationToken cancellationToken = default)
    {
        if (_projectPath is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _userSecretsService.ClearAsync(_projectPath, cancellationToken)
                .ConfigureAwait(true);
            StatusText = result.IsSuccess ? "Cleared all User Secrets." : result.Error.Message;
            if (result.IsSuccess)
            {
                Secrets.Clear();
                SelectedSecretIndex = -1;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task TrustHttpsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanTrustHttps)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _certificateService.TrustAsync(cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                HttpsStatusText = result.Error.Message;
                return;
            }

            await RefreshHttpsCoreAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void OpenShortcut(AspNetApiShortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        var result = _uriLauncher.Launch(shortcut.Uri);
        StatusText = result.IsSuccess
            ? $"Opened {shortcut.DisplayName}."
            : result.Error.Message;
    }

    private async Task RefreshSecretsCoreAsync(CancellationToken cancellationToken)
    {
        if (_projectPath is null)
        {
            return;
        }

        var result = await _userSecretsService.ListAsync(_projectPath, cancellationToken).ConfigureAwait(true);
        Secrets.Clear();
        SelectedSecretIndex = -1;
        if (result.IsFailure)
        {
            StatusText = result.Error.Message;
            return;
        }

        foreach (var secret in result.Value!)
        {
            Secrets.Add(secret);
        }

        StatusText = Secrets.Count == 0
            ? "No User Secrets are currently defined."
            : $"Loaded {Secrets.Count} User Secret key(s).";
    }

    private async Task RefreshHttpsCoreAsync(CancellationToken cancellationToken)
    {
        var result = await _certificateService.CheckAsync(cancellationToken).ConfigureAwait(true);
        if (result.IsFailure)
        {
            HttpsStatusText = result.Error.Message;
            return;
        }

        HttpsCertificateState = result.Value!;
        HttpsStatusText = HttpsCertificateState switch
        {
            HttpsDevelopmentCertificateState.Trusted => "HTTPS development certificate is valid and trusted.",
            HttpsDevelopmentCertificateState.ValidUntrusted => "HTTPS development certificate is valid but not trusted.",
            _ => "No valid HTTPS development certificate was found.",
        };
    }
}
