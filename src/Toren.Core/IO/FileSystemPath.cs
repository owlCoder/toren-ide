namespace Toren.Core.IO;

/// <summary>Path comparison matching the default file-system case sensitivity of the host OS.</summary>
public static class FileSystemPath
{
    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static StringComparer Comparer { get; } = StringComparer.FromComparison(Comparison);
}
