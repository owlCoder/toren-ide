namespace Toren.Debugging.Contracts;

public interface IDebugAdapterTransport : IAsyncDisposable
{
    Stream ReadStream { get; }

    Stream WriteStream { get; }

    TextReader ErrorReader { get; }

    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);

    void Terminate();
}
