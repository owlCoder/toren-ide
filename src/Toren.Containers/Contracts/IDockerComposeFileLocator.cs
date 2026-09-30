using Toren.Containers.Models;
using Toren.Core.Results;

namespace Toren.Containers.Contracts;

public interface IDockerComposeFileLocator
{
    Result<DockerComposeFileLocation> Find(string workspacePath);
}
