using Microsoft.EntityFrameworkCore;
using SalesGreen.Estoque.Domain;
using SalesGreen.Estoque.Infrastructure.Persistence;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.IntegrationTests;

/// <summary>Comportamentos que só um PostgreSQL de verdade garante: concorrência otimista (xmin) e índice único.</summary>
[Collection(InfraCollection.Nome)]
public sealed class EstoquePersistenciaTests(InfraFixture infra) : IAsyncLifetime
{
    private readonly DbContextOptions<EstoqueDbContext> _options = new DbContextOptionsBuilder<EstoqueDbContext>()
        .UseNpgsql(infra.ConnectionString($"estoque_{Guid.NewGuid():N}")).Options;

    private EstoqueDbContext NovoContexto() => new(_options);

    public async Task InitializeAsync()
    {
        await using var db = NovoContexto();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Reservas_simultaneas_do_mesmo_produto_disparam_conflito_de_concorrencia()
    {
        var agora = DateTime.UtcNow;
        await using (var db = NovoContexto())
        {
            var item = ItemEstoque.Registrar(CodigoProduto.De("CONC-1"), "Produto", perecivel: false, agora);
            item.DarEntrada(Quantidade.De(10), null, null, agora);
            db.Itens.Add(item);
            await db.SaveChangesAsync();
        }

        await using var pedidoA = NovoContexto();
        await using var pedidoB = NovoContexto();
        var itemA = await pedidoA.Itens.SingleAsync(i => i.ProdutoCodigo == "CONC-1");
        var itemB = await pedidoB.Itens.SingleAsync(i => i.ProdutoCodigo == "CONC-1");

        itemA.Reservar(Quantidade.De(8), agora);
        itemB.Reservar(Quantidade.De(8), agora); // cada um "viu" saldo 10 → sem controle, venderíamos 16

        await pedidoA.SaveChangesAsync();
        await FluentActions.Awaiting(() => pedidoB.SaveChangesAsync()).Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var leitura = NovoContexto();
        var saldo = (await leitura.Itens.SingleAsync(i => i.ProdutoCodigo == "CONC-1")).SaldoDisponivel(DateOnly.FromDateTime(agora));
        saldo.Valor.Should().Be(2);
    }

    [Fact]
    public async Task Nova_entrada_em_item_existente_insere_novo_lote()
    {
        var agora = DateTime.UtcNow;
        await using (var db = NovoContexto())
        {
            db.Itens.Add(ItemEstoque.Registrar(CodigoProduto.De("LOTE-1"), "Iogurte", perecivel: true, agora));
            await db.SaveChangesAsync();
        }

        await using (var db = NovoContexto())
        {
            var item = await db.Itens.SingleAsync(i => i.ProdutoCodigo == "LOTE-1");
            item.DarEntrada(Quantidade.De(12), "L-A", DateOnly.FromDateTime(agora).AddDays(20), agora);
            await db.SaveChangesAsync();
        }

        await using var leitura = NovoContexto();
        (await leitura.Itens.SingleAsync(i => i.ProdutoCodigo == "LOTE-1")).Lotes.Should().ContainSingle(l => l.Codigo == "L-A");
    }

    [Fact]
    public async Task Nao_permite_duas_reservas_para_o_mesmo_pedido()
    {
        var pedidoId = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        await using var db = NovoContexto();
        db.Reservas.Add(Reserva.Criar(pedidoId, [new ItemReserva("X", Guid.NewGuid(), Quantidade.De(1))], agora));
        db.Reservas.Add(Reserva.Criar(pedidoId, [new ItemReserva("X", Guid.NewGuid(), Quantidade.De(1))], agora));

        await FluentActions.Awaiting(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }
}
