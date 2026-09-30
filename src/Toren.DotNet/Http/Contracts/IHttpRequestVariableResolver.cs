using Toren.Core.Results;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Contracts;

public interface IHttpRequestVariableResolver
{
    Result<HttpRequestDefinition> Resolve(
        HttpRequestDefinition request,
        IReadOnlyDictionary<string, string> variables);
}
