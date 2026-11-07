using Microsoft.AspNetCore.Mvc;
using SalesGreen.Pedidos.Application;
using SalesGreen.Pedidos.Domain;

namespace SalesGreen.Pedidos.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
[Produces("application/json")]
public sealed class PedidosController(PedidosAppService pedidos) : ControllerBase
{
    /// <summary>
    /// Cria o pedido (status Pendente) e devolve 202: a confirmação ou o cancelamento acontecem de forma
    /// assíncrona pela Saga. Consulte GET /api/pedidos/{id} para acompanhar.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<PedidoResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Criar(CriarPedidoRequest request, CancellationToken ct)
    {
        var pedido = await pedidos.CriarAsync(request, ct);
        return AcceptedAtAction(nameof(Obter), new { id = pedido.Id }, pedido);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PedidoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PedidoResponse> Obter(Guid id, CancellationToken ct) => pedidos.ObterAsync(id, ct);

    [HttpGet]
    public Task<IReadOnlyList<PedidoResponse>> Listar([FromQuery] StatusPedido? status, [FromQuery] int pagina = 1,
        [FromQuery] int tamanho = 20, CancellationToken ct = default) => pedidos.ListarAsync(status, pagina, tamanho, ct);
}
