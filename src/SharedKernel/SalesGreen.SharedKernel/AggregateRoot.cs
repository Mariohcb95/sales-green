using System.ComponentModel.DataAnnotations.Schema;

namespace SalesGreen.SharedKernel;

/// <summary>
/// Raiz de agregado: única porta de entrada para alterar o agregado e fronteira de consistência transacional.
/// Acumula domain events que são despachados no momento do SaveChanges.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
