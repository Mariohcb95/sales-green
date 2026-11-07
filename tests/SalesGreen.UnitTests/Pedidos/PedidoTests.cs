using SalesGreen.Pedidos.Domain;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.UnitTests.Pedidos;

public class PedidoTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);

    private static ItemPedido Item(string codigo = "789", decimal qtd = 2, decimal preco = 10) =>
        new(CodigoProduto.De(codigo), "Produto", Quantidade.Positiva(qtd), Dinheiro.De(preco));

    [Fact]
    public void Criar_calcula_total_e_gera_evento()
    {
        var pedido = Pedido.Criar("Mercado Bom Preço", [Item(qtd: 2, preco: 10), Item("HF-1", 1.5m, 8.99m)], Agora);

        pedido.Status.Should().Be(StatusPedido.Pendente);
        pedido.ValorTotal.Valor.Should().Be(33.49m); // 20 + 13,485 → 13,49
        pedido.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PedidoCriadoDomainEvent>();
    }

    [Fact]
    public void Pedido_sem_itens_e_invalido() =>
        FluentActions.Invoking(() => Pedido.Criar("Cliente", [], Agora)).Should().Throw<DomainException>();

    [Fact]
    public void Produto_repetido_e_invalido() =>
        FluentActions.Invoking(() => Pedido.Criar("Cliente", [Item(), Item()], Agora)).Should().Throw<DomainException>();

    [Fact]
    public void Confirmar_e_idempotente_para_a_mesma_nota()
    {
        var pedido = Pedido.Criar("Cliente", [Item()], Agora);
        var nota = Guid.NewGuid();

        pedido.Confirmar(nota, 1, Agora);
        pedido.Confirmar(nota, 1, Agora);

        pedido.Status.Should().Be(StatusPedido.Confirmado);
        pedido.DomainEvents.OfType<PedidoConfirmadoDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Pedido_confirmado_nao_pode_ser_cancelado()
    {
        var pedido = Pedido.Criar("Cliente", [Item()], Agora);
        pedido.Confirmar(Guid.NewGuid(), 1, Agora);

        FluentActions.Invoking(() => pedido.Cancelar("teste", Agora)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Pedido_cancelado_nao_pode_ser_confirmado()
    {
        var pedido = Pedido.Criar("Cliente", [Item()], Agora);
        pedido.Cancelar("Estoque insuficiente", Agora);

        pedido.MotivoCancelamento.Should().Be("Estoque insuficiente");
        FluentActions.Invoking(() => pedido.Confirmar(Guid.NewGuid(), 1, Agora)).Should().Throw<DomainException>();
    }
}
