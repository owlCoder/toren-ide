using System.Globalization;
using System.Text.RegularExpressions;
using Toren.App.Diagnostics.Contracts;
using Toren.App.Diagnostics.Models;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.App.Diagnostics.Services;

public sealed partial class DotNetCommandDiagnosticParser : IDotNetCommandDiagnosticParser
{
    public IReadOnlyList<ProblemDiagnostic> Parse(
        WorkspaceDescriptor workspace,
        DotNetCommandResult result)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(result);

        var workingDirectory = GetWorkingDirectory(workspace);
        var fallbackProjectPath = workspace.Kind == WorkspaceKind.Project
            ? Path.GetFullPath(workspace.Path)
            : null;
        var diagnostics = new List<ProblemDiagnostic>();

        ParseOutput(result.StandardOutput, workingDirectory, fallbackProjectPath, diagnostics);
        ParseOutput(result.StandardError, workingDirectory, fallbackProjectPath, diagnostics);

        return diagnostics.Distinct().ToArray();
    }

    private static void ParseOutput(
        string output,
        string workingDirectory,
        string? fallbackProjectPath,
        List<ProblemDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        foreach (var line in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (TryParseLine(line, workingDirectory, fallbackProjectPath, out var diagnostic))
            {
                diagnostics.Add(diagnostic);
            }
        }
    }

    private static bool TryParseLine(
        string line,
        string workingDirectory,
        string? fallbackProjectPath,
        out ProblemDiagnostic diagnostic)
    {
        var match = DiagnosticLinePattern().Match(line);
        if (!match.Success)
        {
            diagnostic = null!;
            return false;
        }

        var severity = match.Groups["severity"].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
            ? ProblemSeverity.Error
            : ProblemSeverity.Warning;
        var code = match.Groups["code"].Value;
        var projectPath = ResolvePath(match.Groups["project"].Value, workingDirectory)
            ?? fallbackProjectPath;
        var filePath = ResolveDiagnosticFile(match.Groups["file"].Value, workingDirectory);
        var startLine = ParsePositiveInteger(match.Groups["line"].Value);
        var startColumn = ParsePositiveInteger(match.Groups["column"].Value);
        diagnostic = new ProblemDiagnostic(
            code,
            match.Groups["message"].Value.Trim(),
            severity,
            code.StartsWith("NU", StringComparison.OrdinalIgnoreCase) ? "NuGet" : "MSBuild",
            filePath,
            projectPath,
            startLine,
            startColumn);
        return true;
    }

    private static string GetWorkingDirectory(WorkspaceDescriptor workspace)
    {
        var fullPath = Path.GetFullPath(workspace.Path);
        return workspace.Kind == WorkspaceKind.Folder
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
    }

    private static string? ResolveDiagnosticFile(string value, string workingDirectory)
    {
        var candidate = value.Trim().Trim('"');
        if (candidate.Length == 0
            || candidate.Equals("CSC", StringComparison.OrdinalIgnoreCase)
            || candidate.Equals("MSBUILD", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ResolvePath(candidate, workingDirectory);
    }

    private static string? ResolvePath(string value, string workingDirectory)
    {
        var candidate = value.Trim().Trim('"');
        if (candidate.Length == 0)
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(
                candidate,
                Path.IsPathFullyQualified(candidate) ? Directory.GetCurrentDirectory() : workingDirectory);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static int ParsePositiveInteger(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : 0;

    [GeneratedRegex(
        @"^(?<file>.+?)(?:\((?<line>\d+),(?<column>\d+)\))?\s*:\s*(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<message>.*?)(?:\s+\[(?<project>[^\]]+)\])?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticLinePattern();
}
