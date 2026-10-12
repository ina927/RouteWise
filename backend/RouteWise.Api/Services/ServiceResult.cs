namespace RouteWise.Api.Services;

public enum ServiceError
{
    Validation,
    NotFound,
    Forbidden,
    Conflict
}

// Internal operation outcome; endpoints decide how to represent it over HTTP.
public sealed class ServiceResult<T> where T : notnull
{
    public T? Value { get; }
    public ServiceError? Error { get; }
    public string? Message { get; }
    public bool IsSuccess => Error is null;

    private ServiceResult(T value)
    {
        Value = value;
    }

    private ServiceResult(ServiceError error, string message)
    {
        Error = error;
        Message = message;
    }

    public static ServiceResult<T> Success(T value) => new(value);

    public static ServiceResult<T> Failure(ServiceError error, string message)
        => new(error, message);
}
