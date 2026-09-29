using Toren.Core.Results;
using Toren.Debugging.Models;

namespace Toren.Debugging.Contracts;

public interface IDebugAdapterTransportFactory
{
    Result<IDebugAdapterTransport> Start(
        DebugAdapterDescriptor adapter,
        string? workingDirectory = null);
}
