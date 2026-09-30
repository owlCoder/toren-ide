using Toren.Core.Results;
using Toren.Http.Models;

namespace Toren.Http.Contracts;

public interface IHttpRequestFileParser
{
    Result<HttpRequestFile> Parse(string text);
}
