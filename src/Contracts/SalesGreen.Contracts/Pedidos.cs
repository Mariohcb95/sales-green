namespace SalesGreen.Contracts.Pedidos;

public record ItemPedidoMensagem(string ProdutoCodigo, string Descricao, decimal Quantidade, decimal PrecoUnitario);

/// <summary>Início da Saga de pedido.</summary>
public record PedidoCriado(Guid PedidoId, string Cliente, decimal ValorTotal, IReadOnlyList<ItemPedidoMensagem> Itens, DateTime CriadoEm);

public record PedidoConfirmado(Guid PedidoId, Guid NotaFiscalId, long NumeroNota);

public record PedidoCancelado(Guid PedidoId, string Motivo);

/// <summary>Publicado depois que o pedido é efetivamente confirmado: alimenta o serviço de Análises.</summary>
public record VendaRealizada(Guid PedidoId, DateTime DataVenda, IReadOnlyList<ItemPedidoMensagem> Itens);
