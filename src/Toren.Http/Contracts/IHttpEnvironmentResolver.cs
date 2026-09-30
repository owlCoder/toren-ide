using Toren.Core.Results;
using Toren.Http.Models;

namespace Toren.Http.Contracts;

public interface IHttpEnvironmentResolver
{
    Result<HttpRequestDefinition> Resolve(
        HttpRequestDefinition request,
        IReadOnlyDictionary<string, string> fileVariables,
        IReadOnlyDictionary<string, string>? environmentVariables = null);
}
