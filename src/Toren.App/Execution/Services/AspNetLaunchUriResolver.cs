using Toren.DotNet.Execution.Models;

namespace Toren.App.Execution.Services;

public sealed class AspNetLaunchUriResolver
{
    private const string ListeningMarker = "Now listening on:";

    public Uri? Resolve(DotNetLaunchProfile profile, string outputLine)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(outputLine);

        var markerIndex = outputLine.IndexOf(ListeningMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var candidate = outputLine[(markerIndex + ListeningMarker.Length)..].Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.LaunchUrl))
        {
            return baseUri;
        }

        var relativeLaunchUrl = profile.LaunchUrl.Trim().TrimStart('/');
        return relativeLaunchUrl.Length == 0
            ? baseUri
            : new Uri(baseUri, relativeLaunchUrl);
    }
}
