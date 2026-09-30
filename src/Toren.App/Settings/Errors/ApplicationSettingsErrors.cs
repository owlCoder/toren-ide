using Toren.Core.Results;

namespace Toren.App.Settings.Errors;

public static class ApplicationSettingsErrors
{
    public static OperationError InvalidFormat() => OperationError.Create(
        "app.settings.invalid-format",
        "Application settings could not be read because the stored format is invalid.");

    public static OperationError ReadFailed(string details) => OperationError.Create(
        "app.settings.read-failed",
        $"Application settings could not be read. {details}");

    public static OperationError WriteFailed(string details) => OperationError.Create(
        "app.settings.write-failed",
        $"Application settings could not be saved. {details}");
}
