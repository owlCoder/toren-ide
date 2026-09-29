namespace Toren.Debugging.Protocol;

public sealed class DapSequenceGenerator
{
    private int _current;

    public int Next() => Interlocked.Increment(ref _current);
}
