namespace Toren.Debugging.Contracts;

public interface IDebugAdapterClientFactory
{
    IDebugAdapterClient Create(IDebugAdapterTransport transport);
}
