using Microsoft.AspNetCore.Mvc;
using SalesGreen.Faturamento.Application;

namespace SalesGreen.Faturamento.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public sealed class NotasFiscaisController(FaturamentoAppService faturamento) : ControllerBase
{
    [HttpGet("notas-fiscais/{id:guid}")]
    [ProducesResponseType<NotaFiscalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<NotaFiscalResponse> Obter(Guid id, CancellationToken ct) => faturamento.ObterAsync(id, ct);

    [HttpGet("notas-fiscais/pedido/{pedidoId:guid}")]
    [ProducesResponseType<NotaFiscalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<NotaFiscalResponse> ObterPorPedido(Guid pedidoId, CancellationToken ct) => faturamento.ObterPorPedidoAsync(pedidoId, ct);

    /// <summary>Simula os tributos de uma lista de itens em um cenário (RegimeAtual, Transicao2026, ReformaPlena).</summary>
    [HttpPost("tributos/simulacoes")]
    [ProducesResponseType<SimulacaoResponse>(StatusCodes.Status200OK)]
    public SimulacaoResponse Simular(SimulacaoRequest request) => FaturamentoAppService.Simular(request);
}
