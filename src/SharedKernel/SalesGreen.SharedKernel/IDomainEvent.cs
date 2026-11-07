namespace SalesGreen.SharedKernel;

/// <summary>Fato relevante que aconteceu dentro de um bounded context (marcador).</summary>
public interface IDomainEvent;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
