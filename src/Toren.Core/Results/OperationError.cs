namespace Toren.Core.Results;

public readonly record struct OperationError(string Code, string Message)
{
    public static OperationError None { get; } = new(string.Empty, string.Empty);

    public bool IsNone => string.IsNullOrEmpty(Code);

    public static OperationError Create(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new OperationError(code, message);
    }
}
