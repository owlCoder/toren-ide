namespace Toren.App.Debugging.ViewModels;

public sealed partial class DebugSessionViewModel
{
    public async Task<bool> ToggleBreakpointAsync(
        string sourcePath,
        int line,
        string? condition = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(line);

        var fullPath = Path.GetFullPath(sourcePath);
        if (_breakpointsBySource.TryGetValue(fullPath, out var definitions)
            && definitions.RemoveAll(definition => definition.Line == line) > 0)
        {
            if (definitions.Count == 0)
            {
                _breakpointsBySource.Remove(fullPath);
            }

            await ApplySourceBreakpointsAsync(fullPath, cancellationToken).ConfigureAwait(true);
            return false;
        }

        await AddBreakpointAsync(fullPath, line, condition, cancellationToken).ConfigureAwait(true);
        return true;
    }
}
