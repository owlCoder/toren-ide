using Toren.Core.Results;
using Toren.DotNet.AspNetCore.Contracts;
using Toren.DotNet.AspNetCore.Errors;
using Toren.DotNet.AspNetCore.Models;

namespace Toren.DotNet.AspNetCore.Services;

public sealed class AspNetApiShortcutResolver : IAspNetApiShortcutResolver
{
    public Result<IReadOnlyList<AspNetApiShortcut>> Resolve(Uri applicationBaseUri)
    {
        ArgumentNullException.ThrowIfNull(applicationBaseUri);
        if (!applicationBaseUri.IsAbsoluteUri
            || (!applicationBaseUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !applicationBaseUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<IReadOnlyList<AspNetApiShortcut>>(
                AspNetApiShortcutErrors.UnsupportedBaseUri(applicationBaseUri));
        }

        var origin = new UriBuilder(applicationBaseUri.Scheme, applicationBaseUri.Host, applicationBaseUri.Port).Uri;
        return Result.Success<IReadOnlyList<AspNetApiShortcut>>(
        [
            new AspNetApiShortcut(
                AspNetApiShortcutKind.SwaggerUi,
                "Swagger UI",
                new Uri(origin, "/swagger")),
            new AspNetApiShortcut(
                AspNetApiShortcutKind.OpenApiDocument,
                "OpenAPI document",
                new Uri(origin, "/openapi/v1.json")),
        ]);
    }
}
