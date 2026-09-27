namespace Toren.Core.Execution;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}
