namespace Toren.Core.Results;

public readonly record struct Error(string Code, string Message)
{
    public static Error None { get; } = new(string.Empty, string.Empty);

    public bool IsNone => string.IsNullOrEmpty(Code);

    public static Error Create(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new Error(code, message);
    }
}
