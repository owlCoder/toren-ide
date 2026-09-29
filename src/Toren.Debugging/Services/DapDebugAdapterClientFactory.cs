using Toren.Debugging.Contracts;

namespace Toren.Debugging.Services;

public sealed class DapDebugAdapterClientFactory : IDebugAdapterClientFactory
{
    public IDebugAdapterClient Create(IDebugAdapterTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        return new DapDebugAdapterClient(transport);
    }
}
