using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Faturamento.Domain;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Faturamento.Infrastructure.Persistence;

public sealed class FaturamentoDbContext(DbContextOptions<FaturamentoDbContext> options, IDomainEventDispatcher? dispatcher = null)
    : AppDbContext(options, dispatcher), IUnitOfWork
{
    public const string SequenciaNumeroNota = "nota_fiscal_numero_seq";

    public DbSet<NotaFiscal> Notas => Set<NotaFiscal>();

    public Task SalvarAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void ConfigurarModelo(ModelBuilder modelBuilder)
    {
        // Sequence do PostgreSQL: numeração sem buracos por concorrência de leitura e sem "SELECT MAX + 1".
        modelBuilder.HasSequence<long>(SequenciaNumeroNota).StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<NotaFiscal>(b =>
        {
            b.ToTable("notas_fiscais");
            b.HasKey(n => n.Id);
            b.HasIndex(n => n.PedidoId).IsUnique(); // um pedido = uma nota (idempotência no banco)
            b.HasIndex(n => n.Numero).IsUnique();
            b.Property(n => n.ChaveAcesso).HasMaxLength(60);
            b.Property(n => n.Cliente).HasMaxLength(120);
            b.Property(n => n.Cenario).HasConversion<string>().HasMaxLength(30);
            b.Property(n => n.ValorProdutos).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
            b.Property(n => n.ValorTributos).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
            b.HasMany(n => n.Itens).WithOne().HasForeignKey("NotaFiscalId").OnDelete(DeleteBehavior.Cascade);
            b.HasMany(n => n.Tributos).WithOne().HasForeignKey("NotaFiscalId").OnDelete(DeleteBehavior.Cascade);
            b.Navigation(n => n.Itens).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
            b.Navigation(n => n.Tributos).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        });

        modelBuilder.Entity<ItemNota>(b =>
        {
            b.ToTable("nota_fiscal_itens");
            b.HasKey(i => i.Id);
            b.Property(i => i.ProdutoCodigo).HasMaxLength(CodigoProduto.TamanhoMaximo);
            b.Property(i => i.Descricao).HasMaxLength(120);
            b.Property(i => i.Quantidade).HasConversion(q => q.Valor, v => Quantidade.De(v)).HasPrecision(18, 3);
            b.Property(i => i.ValorUnitario).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
            b.Property(i => i.ValorTotal).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
        });

        modelBuilder.Entity<TributoNota>(b =>
        {
            b.ToTable("nota_fiscal_tributos");
            b.HasKey(t => t.Id);
            b.Property(t => t.Tributo).HasMaxLength(20);
            b.Property(t => t.BaseCalculo).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
            b.Property(t => t.Aliquota).HasPrecision(9, 6);
            b.Property(t => t.Valor).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
        });
    }
}

public sealed class FaturamentoDbContextFactory : IDesignTimeDbContextFactory<FaturamentoDbContext>
{
    public FaturamentoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FaturamentoDbContext>().UseNpgsql("Host=localhost;Database=faturamento").Options);
}

public sealed class NotaFiscalRepository(FaturamentoDbContext db) : INotaFiscalRepository
{
    public Task<NotaFiscal?> ObterPorIdAsync(Guid id, CancellationToken ct) => db.Notas.FirstOrDefaultAsync(n => n.Id == id, ct);

    public Task<NotaFiscal?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken ct) =>
        db.Notas.FirstOrDefaultAsync(n => n.PedidoId == pedidoId, ct);

    public Task<long> ProximoNumeroAsync(CancellationToken ct) =>
        db.Database.SqlQueryRaw<long>($"SELECT nextval('{FaturamentoDbContext.SequenciaNumeroNota}') AS \"Value\"").SingleAsync(ct);

    public void Adicionar(NotaFiscal nota) => db.Notas.Add(nota);
}
