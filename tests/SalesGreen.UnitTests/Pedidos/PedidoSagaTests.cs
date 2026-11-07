using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using SalesGreen.Contracts.Estoque;
using SalesGreen.Contracts.Faturamento;
using SalesGreen.Contracts.Pedidos;
using SalesGreen.Pedidos.Application.Saga;

namespace SalesGreen.UnitTests.Pedidos;

/// <summary>
/// Testa a Saga com o test harness do MassTransit (barramento e repositório em memória):
/// sem RabbitMQ e sem banco, mas com o mesmo código de produção da state machine.
/// </summary>
public sealed class PedidoSagaTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<PedidoStateMachine, PedidoSagaState> _saga = null!;
    private PedidoStateMachine _maquina = null!;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => x.AddSagaStateMachine<PedidoStateMachine, PedidoSagaState>().InMemoryRepository())
            .BuildServiceProvider(validateScopes: true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
        _saga = _harness.GetSagaStateMachineHarness<PedidoStateMachine, PedidoSagaState>();
        _maquina = _saga.StateMachine;
    }

    public async Task DisposeAsync() => await _provider.DisposeAsync();

    private async Task<Guid> CriarPedidoAsync()
    {
        var pedidoId = NewId.NextGuid();
        await _harness.Bus.Publish(new PedidoCriado(pedidoId, "Mercado Teste", 20m,
            [new ItemPedidoMensagem("789", "Arroz", 2, 10m)], DateTime.UtcNow));
        (await _saga.Exists(pedidoId, _maquina.AguardandoEstoque)).Should().NotBeNull();
        return pedidoId;
    }

    [Fact]
    public async Task Caminho_feliz_reserva_fatura_e_confirma()
    {
        var pedidoId = await CriarPedidoAsync();
        (await _harness.Published.Any<ReservarEstoque>(m => m.Context.Message.PedidoId == pedidoId)).Should().BeTrue();

        await _harness.Bus.Publish(new EstoqueReservado(pedidoId));
        (await _saga.Exists(pedidoId, _maquina.AguardandoFaturamento)).Should().NotBeNull();
        (await _harness.Published.Any<GerarNotaFiscal>(m =>
            m.Context.Message.PedidoId == pedidoId && m.Context.Message.Itens.Count == 1)).Should().BeTrue();

        var notaId = Guid.NewGuid();
        await _harness.Bus.Publish(new NotaFiscalGerada(pedidoId, notaId, 42, 20m, 5.6m));
        (await _saga.Exists(pedidoId, _maquina.Confirmado)).Should().NotBeNull();
        (await _harness.Published.Any<PedidoConfirmado>(m =>
            m.Context.Message.PedidoId == pedidoId && m.Context.Message.NumeroNota == 42)).Should().BeTrue();
    }

    [Fact]
    public async Task Estoque_insuficiente_cancela_o_pedido()
    {
        var pedidoId = await CriarPedidoAsync();

        await _harness.Bus.Publish(new EstoqueInsuficiente(pedidoId, "Sem saldo de 789"));

        (await _saga.Exists(pedidoId, _maquina.Cancelado)).Should().NotBeNull();
        (await _harness.Published.Any<PedidoCancelado>(m => m.Context.Message.Motivo == "Sem saldo de 789")).Should().BeTrue();
        (await _harness.Published.Any<GerarNotaFiscal>()).Should().BeFalse();
    }

    [Fact]
    public async Task Falha_no_faturamento_compensa_liberando_o_estoque()
    {
        var pedidoId = await CriarPedidoAsync();
        await _harness.Bus.Publish(new EstoqueReservado(pedidoId));
        (await _saga.Exists(pedidoId, _maquina.AguardandoFaturamento)).Should().NotBeNull();

        await _harness.Bus.Publish(new FaturamentoFalhou(pedidoId, "Limite excedido"));

        (await _saga.Exists(pedidoId, _maquina.Cancelado)).Should().NotBeNull();
        (await _harness.Published.Any<LiberarReservaEstoque>(m => m.Context.Message.PedidoId == pedidoId)).Should().BeTrue();
        (await _harness.Published.Any<PedidoCancelado>(m => m.Context.Message.PedidoId == pedidoId)).Should().BeTrue();
    }

    [Fact]
    public async Task Evento_duplicado_e_ignorado()
    {
        var pedidoId = await CriarPedidoAsync();
        await _harness.Bus.Publish(new EstoqueReservado(pedidoId));
        (await _saga.Exists(pedidoId, _maquina.AguardandoFaturamento)).Should().NotBeNull();

        await _harness.Bus.Publish(new EstoqueReservado(pedidoId)); // reentrega

        (await _saga.Consumed.SelectAsync<EstoqueReservado>().Count()).Should().Be(2);
        (await _harness.Published.SelectAsync<GerarNotaFiscal>().Count()).Should().Be(1);
        (await _saga.Exists(pedidoId, _maquina.AguardandoFaturamento)).Should().NotBeNull();
    }
}
