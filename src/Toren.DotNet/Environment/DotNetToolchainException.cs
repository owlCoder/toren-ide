namespace Toren.DotNet.Environment;

public sealed class DotNetToolchainException : Exception
{
    public DotNetToolchainException(string message)
        : base(message)
    {
    }

    public DotNetToolchainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
