using SalesGreen.Contracts.Pedidos;

namespace SalesGreen.Contracts.Faturamento;

/// <summary>Comando da Saga para o Faturamento.</summary>
public record GerarNotaFiscal(Guid PedidoId, string Cliente, IReadOnlyList<ItemPedidoMensagem> Itens);

public record NotaFiscalGerada(Guid PedidoId, Guid NotaFiscalId, long Numero, decimal ValorProdutos, decimal ValorTributos);

public record FaturamentoFalhou(Guid PedidoId, string Motivo);
