using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Catalogo.Infrastructure.Persistence;

public sealed class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options, IDomainEventDispatcher? dispatcher = null)
    : AppDbContext(options, dispatcher), IUnitOfWork
{
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Importacao> Importacoes => Set<Importacao>();

    public Task SalvarAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void ConfigurarModelo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Produto>(b =>
        {
            b.ToTable("produtos");
            b.HasKey(p => p.Id);
            // Value objects persistidos como colunas simples via conversores.
            b.Property(p => p.Codigo).HasConversion(c => c.Valor, v => CodigoProduto.De(v)).HasMaxLength(CodigoProduto.TamanhoMaximo);
            b.HasIndex(p => p.Codigo).IsUnique(); // garante idempotência da importação também no banco
            b.Property(p => p.Descricao).HasMaxLength(120);
            b.Property(p => p.Categoria).HasMaxLength(60);
            b.Property(p => p.Unidade).HasConversion<string>().HasMaxLength(5);
            b.Property(p => p.PrecoCusto).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
            b.Property(p => p.PrecoVenda).HasConversion(d => d.Valor, v => Dinheiro.De(v)).HasPrecision(18, 2);
            b.Ignore(p => p.MargemPercentual);
        });

        modelBuilder.Entity<Importacao>(b =>
        {
            b.ToTable("importacoes");
            b.HasKey(i => i.Id);
            b.Property(i => i.NomeArquivo).HasMaxLength(255);
            b.Property(i => i.Formato).HasConversion<string>().HasMaxLength(10);
            b.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(i => i.MensagemErro).HasMaxLength(500);
            b.HasMany(i => i.Rejeicoes).WithOne().HasForeignKey("ImportacaoId").OnDelete(DeleteBehavior.Cascade);
            b.Navigation(i => i.Rejeicoes).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<LinhaRejeitada>(b =>
        {
            b.ToTable("importacao_linhas_rejeitadas");
            b.HasKey(l => l.Id);
            b.Property(l => l.CodigoProduto).HasMaxLength(30);
            b.Property(l => l.Motivo).HasMaxLength(300);
        });
    }
}

/// <summary>Usado apenas pelo "dotnet ef migrations" em tempo de design.</summary>
public sealed class CatalogoDbContextFactory : IDesignTimeDbContextFactory<CatalogoDbContext>
{
    public CatalogoDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CatalogoDbContext>().UseNpgsql("Host=localhost;Database=catalogo").Options);
}
