using MassTransit;
using Microsoft.EntityFrameworkCore;
using SalesGreen.SharedKernel;

namespace SalesGreen.BuildingBlocks.Persistence;

/// <summary>
/// DbContext base dos serviços:
/// 1) despacha os domain events ANTES do commit, para que handlers que publicam eventos de integração
///    gravem na tabela de Outbox na MESMA transação dos dados de negócio;
/// 2) registra as tabelas de Inbox/Outbox do MassTransit.
/// </summary>
public abstract class AppDbContext(DbContextOptions options, IDomainEventDispatcher? dispatcher) : DbContext(options)
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (dispatcher is not null)
        {
            // Laço: um handler pode alterar outro agregado e gerar novos eventos.
            while (true)
            {
                var agregados = ChangeTracker.Entries<AggregateRoot>()
                    .Select(e => e.Entity)
                    .Where(a => a.DomainEvents.Count > 0)
                    .ToList();
                if (agregados.Count == 0) break;

                var eventos = agregados.SelectMany(a => a.DomainEvents).ToList();
                agregados.ForEach(a => a.ClearDomainEvents());
                foreach (var evento in eventos)
                    await dispatcher.DispatchAsync(evento, cancellationToken);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigurarModelo(modelBuilder);

        // Ids são gerados no domínio (Guid.NewGuid no construtor). Sem isto, o EF trata uma entidade filha
        // nova (ex.: um novo Lote adicionado a um ItemEstoque existente) como "já existente" e faz UPDATE.
        foreach (var tipo in modelBuilder.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
            modelBuilder.Entity(tipo.ClrType).Property(nameof(Entity.Id)).ValueGeneratedNever();

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    /// <summary>Mapeamento específico de cada serviço.</summary>
    protected abstract void ConfigurarModelo(ModelBuilder modelBuilder);
}
