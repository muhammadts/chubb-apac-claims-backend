namespace Chubb.Claims.Application.Abstractions;

public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

public sealed class ValidationException(string message) : Exception(message);
public sealed class AccessDeniedException(string message) : Exception(message);
public sealed class ResourceNotFoundException(string message) : Exception(message);
public sealed class ConcurrencyConflictException(string message) : Exception(message);
