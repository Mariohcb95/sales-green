using System.Text.Json;
using MassTransit;
using SalesGreen.Contracts.Estoque;
using SalesGreen.Contracts.Faturamento;
using SalesGreen.Contracts.Pedidos;

namespace SalesGreen.Pedidos.Application.Saga;

/// <summary>Estado persistido da Saga (uma linha por pedido no banco de Pedidos).</summary>
public class PedidoSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; } // = PedidoId
    public string CurrentState { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string ItensJson { get; set; } = "[]";
    public decimal ValorTotal { get; set; }
    public Guid? NotaFiscalId { get; set; }
    public long? NumeroNotaFiscal { get; set; }
    public string? MotivoCancelamento { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public IReadOnlyList<ItemPedidoMensagem> Itens() =>
        JsonSerializer.Deserialize<List<ItemPedidoMensagem>>(ItensJson) ?? [];
}

/// <summary>
/// Saga orquestrada do pedido:
///   PedidoCriado → [AguardandoEstoque] → EstoqueReservado → [AguardandoFaturamento] → NotaFiscalGerada → [Confirmado]
/// Compensações:
///   EstoqueInsuficiente → [Cancelado]
///   FaturamentoFalhou   → LiberarReservaEstoque + [Cancelado]
/// Orquestração (em vez de coreografia) deixa o fluxo explícito em UM lugar — fácil de ler, testar e evoluir.
/// </summary>
public sealed class PedidoStateMachine : MassTransitStateMachine<PedidoSagaState>
{
    public State AguardandoEstoque { get; private set; } = null!;
    public State AguardandoFaturamento { get; private set; } = null!;
    public State Confirmado { get; private set; } = null!;
    public State Cancelado { get; private set; } = null!;

    public Event<PedidoCriado> PedidoCriado { get; private set; } = null!;
    public Event<EstoqueReservado> EstoqueReservado { get; private set; } = null!;
    public Event<EstoqueInsuficiente> EstoqueInsuficiente { get; private set; } = null!;
    public Event<NotaFiscalGerada> NotaFiscalGerada { get; private set; } = null!;
    public Event<FaturamentoFalhou> FaturamentoFalhou { get; private set; } = null!;

    public PedidoStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PedidoCriado, e => e.CorrelateById(m => m.Message.PedidoId));
        Event(() => EstoqueReservado, e => e.CorrelateById(m => m.Message.PedidoId));
        Event(() => EstoqueInsuficiente, e => e.CorrelateById(m => m.Message.PedidoId));
        Event(() => NotaFiscalGerada, e => e.CorrelateById(m => m.Message.PedidoId));
        Event(() => FaturamentoFalhou, e => e.CorrelateById(m => m.Message.PedidoId));

        Initially(
            When(PedidoCriado)
                .Then(ctx =>
                {
                    ctx.Saga.Cliente = ctx.Message.Cliente;
                    ctx.Saga.ItensJson = JsonSerializer.Serialize(ctx.Message.Itens);
                    ctx.Saga.ValorTotal = ctx.Message.ValorTotal;
                    ctx.Saga.CriadoEm = ctx.Saga.AtualizadoEm = DateTime.UtcNow;
                })
                .Publish(ctx => new ReservarEstoque(ctx.Saga.CorrelationId, ctx.Message.Itens))
                .TransitionTo(AguardandoEstoque));

        During(AguardandoEstoque,
            When(EstoqueReservado)
                .Then(ctx => ctx.Saga.AtualizadoEm = DateTime.UtcNow)
                .Publish(ctx => new GerarNotaFiscal(ctx.Saga.CorrelationId, ctx.Saga.Cliente, ctx.Saga.Itens()))
                .TransitionTo(AguardandoFaturamento),
            When(EstoqueInsuficiente)
                .Then(ctx => Cancelar(ctx.Saga, ctx.Message.Motivo))
                .Publish(ctx => new PedidoCancelado(ctx.Saga.CorrelationId, ctx.Message.Motivo))
                .TransitionTo(Cancelado));

        During(AguardandoFaturamento,
            When(NotaFiscalGerada)
                .Then(ctx =>
                {
                    ctx.Saga.NotaFiscalId = ctx.Message.NotaFiscalId;
                    ctx.Saga.NumeroNotaFiscal = ctx.Message.Numero;
                    ctx.Saga.AtualizadoEm = DateTime.UtcNow;
                })
                .Publish(ctx => new PedidoConfirmado(ctx.Saga.CorrelationId, ctx.Message.NotaFiscalId, ctx.Message.Numero))
                .TransitionTo(Confirmado),
            When(FaturamentoFalhou)
                .Then(ctx => Cancelar(ctx.Saga, ctx.Message.Motivo))
                // Compensação: o estoque já foi reservado, então precisa ser devolvido.
                .Publish(ctx => new LiberarReservaEstoque(ctx.Saga.CorrelationId, ctx.Message.Motivo))
                .Publish(ctx => new PedidoCancelado(ctx.Saga.CorrelationId, ctx.Message.Motivo))
                .TransitionTo(Cancelado));

        // Entrega "at-least-once": mensagens duplicadas ou atrasadas são ignoradas com segurança.
        During(AguardandoEstoque, AguardandoFaturamento, Confirmado, Cancelado, Ignore(PedidoCriado));
        During(AguardandoFaturamento, Confirmado, Cancelado, Ignore(EstoqueReservado), Ignore(EstoqueInsuficiente));
        During(Confirmado, Cancelado, Ignore(NotaFiscalGerada), Ignore(FaturamentoFalhou));
    }

    private static void Cancelar(PedidoSagaState saga, string motivo)
    {
        saga.MotivoCancelamento = motivo;
        saga.AtualizadoEm = DateTime.UtcNow;
    }
}
