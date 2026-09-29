namespace Toren.Core.Debugging.Protocol;

public sealed class DapSequenceGenerator
{
    private int _current;

    public int Next()
    {
        return Interlocked.Increment(ref _current);
    }
}
