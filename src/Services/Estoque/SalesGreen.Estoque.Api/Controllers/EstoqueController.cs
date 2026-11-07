using Microsoft.AspNetCore.Mvc;
using SalesGreen.Estoque.Application;

namespace SalesGreen.Estoque.Api.Controllers;

[ApiController]
[Route("api/estoque")]
[Produces("application/json")]
public sealed class EstoqueController(EstoqueAppService estoque) : ControllerBase
{
    /// <summary>Entrada de mercadoria (compra/recebimento). Perecíveis exigem lote com validade.</summary>
    [HttpPost("entradas")]
    [ProducesResponseType<SaldoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<SaldoResponse> DarEntrada(EntradaEstoqueRequest request, CancellationToken ct) => estoque.DarEntradaAsync(request, ct);

    [HttpGet("{produtoCodigo}")]
    [ProducesResponseType<SaldoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<SaldoResponse> ObterSaldo(string produtoCodigo, CancellationToken ct) => estoque.ObterSaldoAsync(produtoCodigo, ct);
}
