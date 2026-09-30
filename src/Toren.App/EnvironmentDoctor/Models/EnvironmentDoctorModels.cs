namespace Toren.App.EnvironmentDoctor.Models;

public enum EnvironmentDoctorStatus
{
    Healthy = 0,
    Warning = 1,
    Error = 2,
}

public sealed record EnvironmentDoctorCheck(
    string Id,
    string DisplayName,
    EnvironmentDoctorStatus Status,
    string Summary,
    string? Remediation = null);

public sealed record EnvironmentDoctorReport(IReadOnlyList<EnvironmentDoctorCheck> Checks)
{
    public int WarningCount => Checks.Count(check => check.Status == EnvironmentDoctorStatus.Warning);

    public int ErrorCount => Checks.Count(check => check.Status == EnvironmentDoctorStatus.Error);

    public bool IsHealthy => ErrorCount == 0 && WarningCount == 0;
}
