using Toren.Core.Results;

namespace Toren.Containers.Contracts;

public interface IDockerComposeFileLocator
{
    Result<string?> Find(string workspacePath);
}
