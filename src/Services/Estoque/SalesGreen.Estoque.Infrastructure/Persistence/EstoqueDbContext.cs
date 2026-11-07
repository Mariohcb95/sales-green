using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Estoque.Domain;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Estoque.Infrastructure.Persistence;

public sealed class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options, IDomainEventDispatcher? dispatcher = null)
    : AppDbContext(options, dispatcher), IUnitOfWork
{
    public DbSet<ItemEstoque> Itens => Set<ItemEstoque>();
    public DbSet<Reserva> Reservas => Set<Reserva>();

    public Task SalvarAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void ConfigurarModelo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemEstoque>(b =>
        {
            b.ToTable("itens_estoque");
            b.HasKey(i => i.Id);
            b.Property(i => i.ProdutoCodigo).HasMaxLength(CodigoProduto.TamanhoMaximo);
            b.HasIndex(i => i.ProdutoCodigo).IsUnique();
            b.Property(i => i.Descricao).HasMaxLength(120);
            // Concorrência otimista: UPDATE ... WHERE "Versao" = <versão lida>. Duas reservas simultâneas do
            // mesmo produto → a segunda afeta 0 linhas, gera DbUpdateConcurrencyException e o retry do consumidor
            // reprocessa a mensagem com o saldo atualizado. Sem lock no banco enquanto a mensagem é processada.
            b.Property(i => i.Versao).IsConcurrencyToken();
            b.HasMany(i => i.Lotes).WithOne().HasForeignKey("ItemEstoqueId").OnDelete(DeleteBehavior.Cascade);
            b.Navigation(i => i.Lotes).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        });

        modelBuilder.Entity<Lote>(b =>
        {
            b.ToTable("lotes");
            b.HasKey(l => l.Id);
            b.Property(l => l.Codigo).HasMaxLength(40);
            b.Property(l => l.Disponivel).HasConversion(q => q.Valor, v => Quantidade.De(v)).HasPrecision(18, 3);
        });

        modelBuilder.Entity<Reserva>(b =>
        {
            b.ToTable("reservas");
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.PedidoId).IsUnique();
            b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(r => r.MotivoLiberacao).HasMaxLength(300);
            b.HasMany(r => r.Itens).WithOne().HasForeignKey("ReservaId").OnDelete(DeleteBehavior.Cascade);
            b.Navigation(r => r.Itens).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        });

        modelBuilder.Entity<ItemReserva>(b =>
        {
            b.ToTable("reserva_itens");
            b.HasKey(i => i.Id);
            b.Property(i => i.ProdutoCodigo).HasMaxLength(CodigoProduto.TamanhoMaximo);
            b.Property(i => i.Quantidade).HasConversion(q => q.Valor, v => Quantidade.De(v)).HasPrecision(18, 3);
        });
    }
}

public sealed class EstoqueDbContextFactory : IDesignTimeDbContextFactory<EstoqueDbContext>
{
    public EstoqueDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<EstoqueDbContext>().UseNpgsql("Host=localhost;Database=estoque").Options);
}

public sealed class EstoqueRepository(EstoqueDbContext db) : IEstoqueRepository
{
    public Task<ItemEstoque?> ObterAsync(string produtoCodigo, CancellationToken ct) =>
        db.Itens.FirstOrDefaultAsync(i => i.ProdutoCodigo == produtoCodigo, ct);

    public async Task<IReadOnlyDictionary<string, ItemEstoque>> ObterVariosAsync(IReadOnlyCollection<string> produtoCodigos, CancellationToken ct) =>
        await db.Itens.Where(i => produtoCodigos.Contains(i.ProdutoCodigo)).ToDictionaryAsync(i => i.ProdutoCodigo, ct);

    public void Adicionar(ItemEstoque item) => db.Itens.Add(item);

    public Task<Reserva?> ObterReservaAsync(Guid pedidoId, CancellationToken ct) =>
        db.Reservas.FirstOrDefaultAsync(r => r.PedidoId == pedidoId, ct);

    public void AdicionarReserva(Reserva reserva) => db.Reservas.Add(reserva);
}
