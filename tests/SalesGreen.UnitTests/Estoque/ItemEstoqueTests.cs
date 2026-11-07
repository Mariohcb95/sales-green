using SalesGreen.Estoque.Domain;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.UnitTests.Estoque;

public class ItemEstoqueTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoje = DateOnly.FromDateTime(Agora);

    private static ItemEstoque Perecivel() => ItemEstoque.Registrar(CodigoProduto.De("HF-TOMATE"), "Tomate", perecivel: true, Agora);

    [Fact]
    public void Reserva_segue_FEFO_saindo_primeiro_o_lote_que_vence_antes()
    {
        var item = Perecivel();
        item.DarEntrada(Quantidade.De(10), "L-TARDE", Hoje.AddDays(10), Agora);
        item.DarEntrada(Quantidade.De(5), "L-CEDO", Hoje.AddDays(2), Agora);

        var alocacoes = item.Reservar(Quantidade.De(7), Agora);

        var cedo = item.Lotes.Single(l => l.Codigo == "L-CEDO");
        var tarde = item.Lotes.Single(l => l.Codigo == "L-TARDE");
        alocacoes.Should().HaveCount(2);
        alocacoes[0].Should().Be((cedo.Id, Quantidade.De(5)));
        alocacoes[1].Should().Be((tarde.Id, Quantidade.De(2)));
        item.SaldoDisponivel(Hoje).Valor.Should().Be(8);
    }

    [Fact]
    public void Lote_vencido_nao_entra_no_saldo_disponivel()
    {
        var item = Perecivel();
        item.DarEntrada(Quantidade.De(10), "L1", Hoje.AddDays(1), Agora);

        item.SaldoDisponivel(Hoje.AddDays(1)).Should().Be(Quantidade.Zero);
        item.PodeReservar(Quantidade.De(1), Hoje.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void Perecivel_exige_validade_futura()
    {
        var item = Perecivel();
        FluentActions.Invoking(() => item.DarEntrada(Quantidade.De(1), "L1", null, Agora)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => item.DarEntrada(Quantidade.De(1), "L1", Hoje, Agora)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Reservar_alem_do_saldo_falha()
    {
        var item = ItemEstoque.Registrar(CodigoProduto.De("789"), "Arroz", perecivel: false, Agora);
        item.DarEntrada(Quantidade.De(3), null, null, Agora);

        FluentActions.Invoking(() => item.Reservar(Quantidade.De(4), Agora)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Devolver_restaura_o_saldo_do_lote_de_origem()
    {
        var item = ItemEstoque.Registrar(CodigoProduto.De("789"), "Arroz", perecivel: false, Agora);
        item.DarEntrada(Quantidade.De(10), null, null, Agora);
        var (loteId, quantidade) = item.Reservar(Quantidade.De(4), Agora).Single();

        item.Devolver(loteId, quantidade, Agora);

        item.SaldoDisponivel(Hoje).Valor.Should().Be(10);
    }

    [Fact]
    public void Toda_alteracao_publica_evento_de_saldo()
    {
        var item = ItemEstoque.Registrar(CodigoProduto.De("789"), "Arroz", perecivel: false, Agora);
        item.DarEntrada(Quantidade.De(10), null, null, Agora);
        item.Reservar(Quantidade.De(4), Agora);

        item.DomainEvents.OfType<SaldoAlteradoDomainEvent>().Select(e => e.SaldoDisponivel).Should().Equal(10m, 6m);
    }

    [Fact]
    public void Reserva_liberada_duas_vezes_so_conta_uma()
    {
        var reserva = Reserva.Criar(Guid.NewGuid(), [new ItemReserva("789", Guid.NewGuid(), Quantidade.De(1))], Agora);

        reserva.Liberar("compensação", Agora).Should().BeTrue();
        reserva.Liberar("compensação", Agora).Should().BeFalse();
    }
}
