using Toren.DotNet.Execution.Models;

namespace Toren.App.Execution.Contracts;

public interface IAspNetLaunchUriResolver
{
    Uri? Resolve(DotNetLaunchProfile profile, string outputLine);
}
