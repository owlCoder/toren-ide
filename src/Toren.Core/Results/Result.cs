using System.Diagnostics.CodeAnalysis;

namespace Toren.Core.Results;

public sealed class Result<T>
{
    internal Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = OperationError.None;
    }

    internal Result(OperationError error)
    {
        IsSuccess = false;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public OperationError Error { get; }
}

public static class Result
{
    public static Result<T> Success<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value);
    }

    public static Result<T> Failure<T>(OperationError error)
    {
        if (error.IsNone)
        {
            throw new ArgumentException("A failed result must contain an error.", nameof(error));
        }

        return new Result<T>(error);
    }
}
