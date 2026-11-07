using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Pedidos.Application.Saga;
using SalesGreen.Pedidos.Domain;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Pedidos.Infrastructure.Persistence;

public sealed class PedidosDbContext(DbContextOptions<PedidosDbContext> options, IDomainEventDispatcher? dispatcher = null)
    : AppDbContext(options, dispatcher), IUnitOfWork
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public Task SalvarAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void ConfigurarModelo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>(b =>
        {
            b.ToTable("pedidos");
            b.HasKey(p => p.Id);
            b.Property(p => p.Cliente).HasMaxLength(120);
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(p => p.MotivoCancelamento).HasMaxLength(300);
            b.Ignore(p => p.ValorTotal);
            b.HasMany(p => p.Itens).WithOne().HasForeignKey("PedidoId").OnDelete(DeleteBehavior.Cascade);
            b.Navigation(p => p.Itens).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
            b.HasIndex(p => p.Status);
        });

        modelBuilder.Entity<ItemPedido>(b =>
        {
            b.ToTable("pedido_itens");
            b.HasKey(i => i.Id);
            b.Property(i => i.ProdutoCodigo).HasMaxLength(CodigoProduto.TamanhoMaximo);
            b.Property(i => i.Descricao).HasMaxLength(120);
            b.Property(i => i.Quantidade).HasConversion(q => q.Valor, v => Quantidade.De(v)).HasPrecision(18, 3);
            b.Property(i => i.PrecoUnitario).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
            b.Ignore(i => i.Subtotal);
        });

        new PedidoSagaStateMap().Configure(modelBuilder);
    }
}

/// <summary>Mapeamento da tabela de estado da Saga.</summary>
public sealed class PedidoSagaStateMap : SagaClassMap<PedidoSagaState>
{
    protected override void Configure(EntityTypeBuilder<PedidoSagaState> entity, ModelBuilder model)
    {
        entity.ToTable("pedido_saga");
        entity.Property(x => x.CurrentState).HasMaxLength(64);
        entity.Property(x => x.Cliente).HasMaxLength(120);
        entity.Property(x => x.ItensJson).HasColumnType("jsonb");
        entity.Property(x => x.ValorTotal).HasPrecision(18, 2);
        entity.Property(x => x.MotivoCancelamento).HasMaxLength(300);
    }
}

public sealed class PedidosDbContextFactory : IDesignTimeDbContextFactory<PedidosDbContext>
{
    public PedidosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PedidosDbContext>().UseNpgsql("Host=localhost;Database=pedidos").Options);
}
