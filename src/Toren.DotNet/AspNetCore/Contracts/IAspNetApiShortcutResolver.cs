using Toren.Core.Results;
using Toren.DotNet.AspNetCore.Models;

namespace Toren.DotNet.AspNetCore.Contracts;

public interface IAspNetApiShortcutResolver
{
    Result<IReadOnlyList<AspNetApiShortcut>> Resolve(Uri applicationBaseUri);
}
