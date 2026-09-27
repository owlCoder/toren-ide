using System.Diagnostics.CodeAnalysis;

namespace Toren.Core.Results;

public sealed class Result<T>
{
    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = Error.None;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        Error = error;
    }

    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public Error Error { get; }

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value);
    }

    public static Result<T> Failure(Error error)
    {
        if (error.IsNone)
        {
            throw new ArgumentException("A failed result must contain an error.", nameof(error));
        }

        return new Result<T>(error);
    }
}
