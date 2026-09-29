using Toren.Core.Results;

namespace Toren.Core.Navigation.Contracts;

public interface IExternalUriLauncher
{
    Result<bool> Launch(Uri uri);
}
