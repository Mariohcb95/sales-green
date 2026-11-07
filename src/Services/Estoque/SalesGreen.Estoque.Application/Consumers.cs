using MassTransit;
using SalesGreen.Contracts.Catalogo;
using SalesGreen.Contracts.Estoque;
using SalesGreen.Estoque.Domain;
using SalesGreen.SharedKernel;

namespace SalesGreen.Estoque.Application;

public sealed class ProdutoCadastradoConsumer(EstoqueAppService estoque) : IConsumer<ProdutoCadastrado>
{
    public Task Consume(ConsumeContext<ProdutoCadastrado> context) =>
        estoque.RegistrarProdutoAsync(context.Message.Codigo, context.Message.Descricao, context.Message.Perecivel, context.CancellationToken);
}

public sealed class ReservarEstoqueConsumer(EstoqueAppService estoque) : IConsumer<ReservarEstoque>
{
    public Task Consume(ConsumeContext<ReservarEstoque> context) =>
        estoque.ReservarAsync(context.Message.PedidoId, context.Message.Itens, context.CancellationToken);
}

public sealed class LiberarReservaEstoqueConsumer(EstoqueAppService estoque) : IConsumer<LiberarReservaEstoque>
{
    public Task Consume(ConsumeContext<LiberarReservaEstoque> context) =>
        estoque.LiberarAsync(context.Message.PedidoId, context.Message.Motivo, context.CancellationToken);
}

/// <summary>Domain event → evento de integração (consumido pelo serviço de Análises).</summary>
public sealed class SaldoAlteradoHandler(IPublishEndpoint publish) : IDomainEventHandler<SaldoAlteradoDomainEvent>
{
    public Task HandleAsync(SaldoAlteradoDomainEvent e, CancellationToken ct) =>
        publish.Publish(new SaldoEstoqueAlterado(e.ProdutoCodigo, e.SaldoDisponivel, e.AlteradoEm), ct);
}
