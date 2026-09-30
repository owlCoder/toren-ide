using Toren.DotNet.Http.Models;

namespace Toren.DotNet.Http.Contracts;

public interface IHttpResponseFormatter
{
    string FormatBody(HttpResponseSnapshot response);
}
