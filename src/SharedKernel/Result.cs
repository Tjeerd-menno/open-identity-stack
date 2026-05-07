using System.Diagnostics.CodeAnalysis;

using SharedKernel;
namespace SharedKernel;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// </summary>
/// <typeparam name="TValue">The type of the value on success.</typeparam>
public sealed class Result<TValue>
{
    private readonly TValue? value;
    private readonly DomainError? error;

    /// <summary>
    /// Gets whether the result represents a successful operation.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the result represents a failed operation.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !this.IsSuccess;

    /// <summary>
    /// Gets the value if the result is successful.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a failed result.</exception>
    public TValue Value => this.IsSuccess
        ? this.value!
        : throw new InvalidOperationException("Cannot access Value on a failed result. Check IsSuccess first.");

    /// <summary>
    /// Gets the error if the result is a failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a successful result.</exception>
    public DomainError Error => this.IsFailure
        ? this.error!
        : throw new InvalidOperationException("Cannot access Error on a successful result. Check IsFailure first.");

    private Result(TValue value)
    {
        this.IsSuccess = true;
        this.value = value;
        this.error = null;
    }

    private Result(DomainError error)
    {
        this.IsSuccess = false;
        this.value = default;
        this.error = error;
    }

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    public static implicit operator Result<TValue>(TValue value) => new(value);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result<TValue>(DomainError error) => new(error);

    /// <summary>
    /// Matches the result and executes the appropriate function.
    /// </summary>
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<DomainError, TResult> onFailure)
    {
        return this.IsSuccess ? onSuccess(this.Value) : onFailure(this.Error);
    }

    /// <summary>
    /// Maps the value if successful, otherwise returns the error.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper)
    {
        return this.IsSuccess
            ? mapper(this.Value)
            : this.Error;
    }

    /// <summary>
    /// Chains another result-producing operation if successful.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<TValue, Result<TNew>> binder)
    {
        return this.IsSuccess ? binder(this.Value) : this.Error;
    }
}

/// <summary>
/// Represents the result of an operation that can either succeed or fail with an error.
/// </summary>
public sealed class Result
{
    private readonly DomainError? error;

    /// <summary>
    /// Gets whether the result represents a successful operation.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets whether the result represents a failed operation.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !this.IsSuccess;

    /// <summary>
    /// Gets the error if the result is a failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessed on a successful result.</exception>
    public DomainError Error => this.IsFailure
        ? this.error!
        : throw new InvalidOperationException("Cannot access Error on a successful result. Check IsFailure first.");

    private Result()
    {
        this.IsSuccess = true;
        this.error = null;
    }

    private Result(DomainError error)
    {
        this.IsSuccess = false;
        this.error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new();

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result Failure(DomainError error) => new(error);

    /// <summary>
    /// Implicitly converts an error to a failed result.
    /// </summary>
    public static implicit operator Result(DomainError error) => Failure(error);

    /// <summary>
    /// Matches the result and executes the appropriate function.
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<DomainError, TResult> onFailure)
    {
        return this.IsSuccess ? onSuccess() : onFailure(this.Error);
    }
}

/// <summary>
/// Represents an error with a code and description.
/// </summary>
/// <param name="Code">A unique error code for identification.</param>
/// <param name="Description">A human-readable description of the error.</param>
public sealed record DomainError(string Code, string Description)
{
    /// <summary>
    /// A none/empty error for situations where no specific error occurred.
    /// </summary>
    public static readonly DomainError None = new(string.Empty, string.Empty);

    /// <summary>
    /// A null value error.
    /// </summary>
    public static readonly DomainError NullValue = new("Error.NullValue", "A null value was provided.");

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static DomainError Validation(string code, string description) =>
        new($"Validation.{code}", description);

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static DomainError NotFound(string code, string description) =>
        new($"NotFound.{code}", description);

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    public static DomainError Conflict(string code, string description) =>
        new($"Conflict.{code}", description);

    /// <summary>
    /// Creates an authorization error.
    /// </summary>
    public static DomainError Unauthorized(string code, string description) =>
        new($"Unauthorized.{code}", description);

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static DomainError Forbidden(string code, string description) =>
        new($"Forbidden.{code}", description);

    /// <summary>
    /// Creates a failure error.
    /// </summary>
    public static DomainError Failure(string code, string description) =>
        new($"Failure.{code}", description);
}
