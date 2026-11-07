using Microsoft.AspNetCore.Mvc;
using SalesGreen.Catalogo.Application.Produtos;

namespace SalesGreen.Catalogo.Api.Controllers;

[ApiController]
[Route("api/produtos")]
[Produces("application/json")]
public sealed class ProdutosController(ProdutosAppService produtos) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProdutoResponse>> Listar([FromQuery] string? busca, [FromQuery] int pagina = 1, [FromQuery] int tamanho = 20,
        CancellationToken ct = default) => produtos.ListarAsync(busca, pagina, tamanho, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProdutoResponse> Obter(Guid id, CancellationToken ct) => produtos.ObterAsync(id, ct);

    /// <summary>Consulta por código (usada pelo serviço de Pedidos via HTTP).</summary>
    [HttpGet("codigo/{codigo}")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ProdutoResponse> ObterPorCodigo(string codigo, CancellationToken ct) => produtos.ObterPorCodigoAsync(codigo, ct);

    [HttpPost]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ProdutoResponse>> Cadastrar(CadastrarProdutoRequest request, CancellationToken ct)
    {
        var produto = await produtos.CadastrarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = produto.Id }, produto);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProdutoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ProdutoResponse> Atualizar(Guid id, AtualizarProdutoRequest request, CancellationToken ct) =>
        produtos.AtualizarAsync(id, request, ct);
}
