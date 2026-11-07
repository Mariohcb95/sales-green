using MassTransit;
using SalesGreen.Contracts.Pedidos;
using SalesGreen.Pedidos.Domain;
using SalesGreen.SharedKernel;

namespace SalesGreen.Pedidos.Application;

internal static class Mapeamento
{
    public static List<ItemPedidoMensagem> Itens(Pedido p) =>
        p.Itens.Select(i => new ItemPedidoMensagem(i.ProdutoCodigo, i.Descricao, i.Quantidade.Valor, i.PrecoUnitario.Valor)).ToList();
}

/// <summary>Domain event → evento de integração que inicia a Saga (gravado no Outbox).</summary>
public sealed class PedidoCriadoHandler(IPublishEndpoint publish) : IDomainEventHandler<PedidoCriadoDomainEvent>
{
    public Task HandleAsync(PedidoCriadoDomainEvent e, CancellationToken ct) =>
        publish.Publish(new PedidoCriado(e.Pedido.Id, e.Pedido.Cliente, e.Pedido.ValorTotal.Valor, Mapeamento.Itens(e.Pedido), e.Pedido.CriadoEm), ct);
}

/// <summary>Venda efetivada: alimenta o serviço de Análises.</summary>
public sealed class PedidoConfirmadoHandler(IPublishEndpoint publish) : IDomainEventHandler<PedidoConfirmadoDomainEvent>
{
    public Task HandleAsync(PedidoConfirmadoDomainEvent e, CancellationToken ct) =>
        publish.Publish(new VendaRealizada(e.Pedido.Id, e.Pedido.AtualizadoEm, Mapeamento.Itens(e.Pedido)), ct);
}

/// <summary>A Saga decidiu: confirmar o agregado Pedido.</summary>
public sealed class PedidoConfirmadoConsumer(PedidosAppService pedidos) : IConsumer<PedidoConfirmado>
{
    public Task Consume(ConsumeContext<PedidoConfirmado> context) =>
        pedidos.ConfirmarAsync(context.Message.PedidoId, context.Message.NotaFiscalId, context.Message.NumeroNota, context.CancellationToken);
}

/// <summary>A Saga decidiu: cancelar o agregado Pedido (compensação).</summary>
public sealed class PedidoCanceladoConsumer(PedidosAppService pedidos) : IConsumer<PedidoCancelado>
{
    public Task Consume(ConsumeContext<PedidoCancelado> context) =>
        pedidos.CancelarAsync(context.Message.PedidoId, context.Message.Motivo, context.CancellationToken);
}
