using SalesGreen.Contracts.Pedidos;

namespace SalesGreen.Contracts.Estoque;

/// <summary>Comando da Saga para o Estoque.</summary>
public record ReservarEstoque(Guid PedidoId, IReadOnlyList<ItemPedidoMensagem> Itens);

/// <summary>Comando de compensação: devolve o que foi reservado.</summary>
public record LiberarReservaEstoque(Guid PedidoId, string Motivo);

public record EstoqueReservado(Guid PedidoId);

public record EstoqueInsuficiente(Guid PedidoId, string Motivo);

/// <summary>Publicado sempre que o saldo disponível de um produto muda (alimenta a análise de ruptura).</summary>
public record SaldoEstoqueAlterado(string ProdutoCodigo, decimal SaldoDisponivel, DateTime AlteradoEm);
