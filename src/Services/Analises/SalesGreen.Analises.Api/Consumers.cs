using MassTransit;
using SalesGreen.Analises.Api.Cassandra;
using SalesGreen.Contracts.Estoque;
using SalesGreen.Contracts.Pedidos;

namespace SalesGreen.Analises.Api;

/// <summary>Projeta cada venda confirmada na tabela vendas_por_produto_dia. Idempotente por (produto, dia, pedido).</summary>
public sealed class VendaRealizadaConsumer(AnalisesRepository repositorio) : IConsumer<VendaRealizada>
{
    public async Task Consume(ConsumeContext<VendaRealizada> context)
    {
        var venda = context.Message;
        var dia = DateOnly.FromDateTime(venda.DataVenda);
        foreach (var item in venda.Itens)
            await repositorio.RegistrarVendaAsync(item.ProdutoCodigo, dia, venda.PedidoId, item.Descricao, item.Quantidade,
                Math.Round(item.Quantidade * item.PrecoUnitario, 2, MidpointRounding.AwayFromZero)); // mesma regra do VO Dinheiro
    }
}

public sealed class SaldoEstoqueAlteradoConsumer(AnalisesRepository repositorio) : IConsumer<SaldoEstoqueAlterado>
{
    public Task Consume(ConsumeContext<SaldoEstoqueAlterado> context) =>
        repositorio.AtualizarSaldoAsync(context.Message.ProdutoCodigo, context.Message.SaldoDisponivel, context.Message.AlteradoEm);
}
