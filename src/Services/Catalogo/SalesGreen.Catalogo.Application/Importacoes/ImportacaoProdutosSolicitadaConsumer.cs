using MassTransit;
using SalesGreen.Contracts.Catalogo;

namespace SalesGreen.Catalogo.Application.Importacoes;

/// <summary>Consumidor fino: só traduz a mensagem em chamada ao caso de uso.</summary>
public sealed class ImportacaoProdutosSolicitadaConsumer(ImportacoesAppService importacoes) : IConsumer<ImportacaoProdutosSolicitada>
{
    public Task Consume(ConsumeContext<ImportacaoProdutosSolicitada> context) =>
        importacoes.ProcessarAsync(context.Message.ImportacaoId, context.CancellationToken);
}
