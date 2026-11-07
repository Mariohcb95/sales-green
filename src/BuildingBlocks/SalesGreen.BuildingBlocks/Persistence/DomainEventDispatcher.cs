using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SalesGreen.SharedKernel;

namespace SalesGreen.BuildingBlocks.Persistence;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Despachante in-process de domain events. Feito à mão (poucas linhas) para não depender de MediatR,
/// que passou a ter licença comercial.
/// </summary>
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
        foreach (var handler in serviceProvider.GetServices(handlerType))
            await (Task)handle.Invoke(handler, [domainEvent, cancellationToken])!;
    }
}

public static class DomainEventsServiceCollectionExtensions
{
    /// <summary>Registra o despachante e todos os IDomainEventHandler&lt;T&gt; do assembly informado.</summary>
    public static IServiceCollection AddDomainEvents(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        var handlers = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(i => (Service: i, Implementation: t)));
        foreach (var (service, implementation) in handlers)
            services.AddScoped(service, implementation);
        return services;
    }
}
