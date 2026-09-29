using Toren.Core.Execution.Models;

namespace Toren.Core.Execution.Contracts;

public interface INativeShellProvider
{
    ProcessRequest CreateShellRequest(string workingDirectory);
}
