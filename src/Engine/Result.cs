namespace DataMorph.Engine;

/// <summary>
/// Represents the result of an operation that can fail with an error.
/// This type enables zero-allocation error handling for hot paths.
/// </summary>
public readonly struct Result
{
    private readonly string? _error;

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public string Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error on a successful result.");
            return _error!;
        }
    }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, null);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static Result Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(false, error);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
            action();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }
}

/// <summary>
/// Represents the result of an operation that can succeed with a value or fail with an error.
/// This type enables zero-allocation error handling for hot paths.
/// </summary>
/// <typeparam name="T">The type of the value returned on success.</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly string? _error;

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the value if the operation succeeded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result.</exception>
    public T Value
    {
        get
        {
            if (IsFailure)
                throw new InvalidOperationException("Cannot access Value on a failed result.");
            return _value!;
        }
    }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public string Error
    {
        get
        {
            if (IsSuccess)
                throw new InvalidOperationException("Cannot access Error on a successful result.");
            return _error!;
        }
    }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static Result<T> Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(false, default, error);
    }

    /// <summary>
    /// Transforms the value if the result is successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess
            ? Result<TNew>.Success(mapper(Value))
            : Result<TNew>.Failure(Error);
    }

    /// <summary>
    /// Binds the result to another operation if successful.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return IsSuccess
            ? binder(Value)
            : Result<TNew>.Failure(Error);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(Value);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure)
            action(Error);
        return this;
    }

    /// <summary>
    /// Gets the value if successful, otherwise returns the default value.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? Value : defaultValue;
}
