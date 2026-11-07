using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.Extensions.Logging;
using SalesGreen.Contracts.Faturamento;
using SalesGreen.Contracts.Pedidos;
using SalesGreen.Faturamento.Domain;
using SalesGreen.Faturamento.Domain.Tributacao;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Faturamento.Application;

public sealed class FaturamentoOptions
{
    public CenarioTributario CenarioTributario { get; set; } = CenarioTributario.Transicao2026;
}

public sealed record TributoResponse(string Tributo, decimal BaseCalculo, decimal Aliquota, decimal Valor);
public sealed record ItemNotaResponse(string ProdutoCodigo, string Descricao, decimal Quantidade, decimal ValorUnitario, decimal ValorTotal);
public sealed record NotaFiscalResponse(
    Guid Id, Guid PedidoId, long Numero, int Serie, string ChaveAcesso, string Cliente, string Cenario, DateTime EmitidaEm,
    decimal ValorProdutos, decimal ValorTributos, IReadOnlyList<ItemNotaResponse> Itens, IReadOnlyList<TributoResponse> Tributos)
{
    public static NotaFiscalResponse De(NotaFiscal n) => new(
        n.Id, n.PedidoId, n.Numero, NotaFiscal.Serie, n.ChaveAcesso, n.Cliente, n.Cenario.ToString(), n.EmitidaEm,
        n.ValorProdutos.Valor, n.ValorTributos.Valor,
        n.Itens.Select(i => new ItemNotaResponse(i.ProdutoCodigo, i.Descricao, i.Quantidade.Valor, i.ValorUnitario.Valor, i.ValorTotal.Valor)).ToList(),
        n.Tributos.OrderBy(t => t.Tributo).Select(t => new TributoResponse(t.Tributo, t.BaseCalculo.Valor, t.Aliquota, t.Valor.Valor)).ToList());
}

public sealed record SimulacaoItemRequest([Required, StringLength(30)] string ProdutoCodigo, [Range(0.01, 10_000_000)] decimal ValorTotal);
public sealed record SimulacaoRequest(CenarioTributario Cenario, [Required, MinLength(1), MaxLength(100)] List<SimulacaoItemRequest> Itens);
public sealed record SimulacaoResponse(string Cenario, decimal ValorTotalTributos, IReadOnlyList<TributoResponse> Tributos);

public sealed class FaturamentoAppService(
    INotaFiscalRepository notas,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publish,
    FaturamentoOptions options,
    TimeProvider relogio,
    ILogger<FaturamentoAppService> logger)
{
    /// <summary>Comando da Saga. Sucesso → NotaFiscalGerada; erro de negócio → FaturamentoFalhou (dispara compensação).</summary>
    public async Task GerarNotaAsync(Guid pedidoId, string cliente, IReadOnlyList<ItemPedidoMensagem> itens, CancellationToken ct)
    {
        if (await notas.ObterPorPedidoAsync(pedidoId, ct) is { } existente)
        {
            logger.LogInformation("Pedido {PedidoId} já possui a nota {Numero}: reenviando resposta (idempotência)", pedidoId, existente.Numero);
            await publish.Publish(Gerada(existente), ct);
            return;
        }

        try
        {
            var itensNota = itens.Select(i => new ItemNota(i.ProdutoCodigo, i.Descricao, Quantidade.Positiva(i.Quantidade), Dinheiro.De(i.PrecoUnitario))).ToList();
            var numero = await notas.ProximoNumeroAsync(ct);
            var nota = NotaFiscal.Emitir(pedidoId, cliente, numero, itensNota, options.CenarioTributario, relogio.GetUtcNow().UtcDateTime);
            notas.Adicionar(nota);
            await publish.Publish(Gerada(nota), ct);
            logger.LogInformation("Nota {Numero} emitida para o pedido {PedidoId}: produtos {ValorProdutos}, tributos {ValorTributos}",
                nota.Numero, pedidoId, nota.ValorProdutos.Valor, nota.ValorTributos.Valor);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Faturamento do pedido {PedidoId} falhou: {Motivo}", pedidoId, ex.Message);
            await publish.Publish(new FaturamentoFalhou(pedidoId, ex.Message), ct);
        }

        await unitOfWork.SalvarAsync(ct);
    }

    public async Task<NotaFiscalResponse> ObterAsync(Guid id, CancellationToken ct) =>
        NotaFiscalResponse.De(await notas.ObterPorIdAsync(id, ct) ?? throw new NotFoundException($"Nota {id} não encontrada."));

    public async Task<NotaFiscalResponse> ObterPorPedidoAsync(Guid pedidoId, CancellationToken ct) =>
        NotaFiscalResponse.De(await notas.ObterPorPedidoAsync(pedidoId, ct) ?? throw new NotFoundException($"Pedido {pedidoId} sem nota."));

    /// <summary>Simula o cálculo sem emitir nota — útil para comparar o regime atual com a reforma.</summary>
    public static SimulacaoResponse Simular(SimulacaoRequest request)
    {
        var resultado = MotorTributario.Para(request.Cenario)
            .Calcular(request.Itens.Select(i => new ItemTributavel(i.ProdutoCodigo, Dinheiro.De(i.ValorTotal))));
        return new SimulacaoResponse(request.Cenario.ToString(), resultado.ValorTotalTributos.Valor,
            resultado.PorTributo.Select(t => new TributoResponse(t.Tributo, t.BaseCalculo.Valor, t.Aliquota, t.Valor.Valor)).ToList());
    }

    private static NotaFiscalGerada Gerada(NotaFiscal n) => new(n.PedidoId, n.Id, n.Numero, n.ValorProdutos.Valor, n.ValorTributos.Valor);
}

public sealed class GerarNotaFiscalConsumer(FaturamentoAppService faturamento) : IConsumer<GerarNotaFiscal>
{
    public Task Consume(ConsumeContext<GerarNotaFiscal> context) =>
        faturamento.GerarNotaAsync(context.Message.PedidoId, context.Message.Cliente, context.Message.Itens, context.CancellationToken);
}
