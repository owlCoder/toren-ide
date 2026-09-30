using Toren.Core.Results;
using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Contracts;

public interface IHttpRequestDocumentParser
{
    Result<IReadOnlyList<HttpRequestDefinition>> Parse(string content);
}
