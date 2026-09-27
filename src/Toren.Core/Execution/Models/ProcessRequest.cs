namespace Toren.Core.Execution.Models;

public sealed record ProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null)
{
    public static ProcessRequest Create(string fileName, params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        return new ProcessRequest(fileName, arguments);
    }
}
