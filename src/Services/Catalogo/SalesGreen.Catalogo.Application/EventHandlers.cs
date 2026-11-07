using MassTransit;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.Contracts.Catalogo;
using SalesGreen.SharedKernel;

namespace SalesGreen.Catalogo.Application;

/// <summary>
/// Traduz domain events (internos) em eventos de integração (contrato público).
/// O IPublishEndpoint está ligado ao Outbox: a mensagem é gravada na mesma transação do SaveChanges.
/// </summary>
public sealed class ProdutoCadastradoHandler(IPublishEndpoint publish) : IDomainEventHandler<ProdutoCadastradoDomainEvent>
{
    public Task HandleAsync(ProdutoCadastradoDomainEvent e, CancellationToken ct) =>
        publish.Publish(new ProdutoCadastrado(
            e.Produto.Id, e.Produto.Codigo.Valor, e.Produto.Descricao, e.Produto.Unidade.ToString(),
            e.Produto.Perecivel, e.Produto.PrazoValidadeDias), ct);
}

public sealed class ImportacaoRecebidaHandler(IPublishEndpoint publish) : IDomainEventHandler<ImportacaoRecebidaDomainEvent>
{
    public Task HandleAsync(ImportacaoRecebidaDomainEvent e, CancellationToken ct) =>
        publish.Publish(new ImportacaoProdutosSolicitada(e.ImportacaoId), ct);
}
