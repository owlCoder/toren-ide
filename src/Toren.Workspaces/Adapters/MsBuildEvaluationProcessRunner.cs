using System.Threading.Channels;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Workspaces.Adapters;

/// <summary>Shares a process budget across background MSBuild evaluations.</summary>
public sealed class MsBuildEvaluationProcessRunner : IProcessRunner
{
    private readonly IProcessRunner _inner;
    private readonly Channel<byte> _slots;

    public MsBuildEvaluationProcessRunner(IProcessRunner inner, int maxConcurrency = 4)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        _inner = inner;
        _slots = Channel.CreateBounded<byte>(maxConcurrency);
        for (var index = 0; index < maxConcurrency; index++)
        {
            _slots.Writer.TryWrite(0);
        }
    }

    public async Task<Result<ProcessResult>> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Arguments.Count == 0
            || !request.Arguments[0].Equals("msbuild", StringComparison.OrdinalIgnoreCase)
            || !request.Arguments.Any(static argument =>
                argument.StartsWith("-getItem:", StringComparison.OrdinalIgnoreCase)
                || argument.StartsWith("-getProperty:", StringComparison.OrdinalIgnoreCase)))
        {
            return await _inner.RunAsync(request, cancellationToken).ConfigureAwait(false);
        }

        // These are background reads. Each evaluation gets one node and leaves no reusable
        // workers behind; foreground build/run commands retain their normal parallelism.
        var evaluation = request with
        {
            Arguments = [.. request.Arguments, "-maxcpucount:1", "-nodeReuse:false"],
        };
        var slot = await _slots.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _inner.RunAsync(evaluation, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _slots.Writer.TryWrite(slot);
        }
    }
}
