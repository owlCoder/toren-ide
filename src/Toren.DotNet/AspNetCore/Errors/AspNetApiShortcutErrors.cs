using Toren.Core.Results;

namespace Toren.DotNet.AspNetCore.Errors;

internal static class AspNetApiShortcutErrors
{
    public static OperationError UnsupportedBaseUri(Uri uri) =>
        OperationError.Create(
            "dotnet.aspnet-api-shortcuts.base-uri.unsupported",
            $"ASP.NET API shortcuts require an absolute HTTP or HTTPS application URL: {uri}");
}
