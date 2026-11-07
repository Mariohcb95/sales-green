using Microsoft.AspNetCore.Mvc;
using SalesGreen.Catalogo.Application.Importacoes;
using SalesGreen.SharedKernel;

namespace SalesGreen.Catalogo.Api.Controllers;

[ApiController]
[Route("api/produtos/importacoes")]
[Produces("application/json")]
public sealed class ImportacoesController(ImportacoesAppService importacoes, IImportacaoConsultas consultas) : ControllerBase
{
    private const long LimiteRequisicao = ValidadorArquivoPlanilha.TamanhoMaximoBytes + 64 * 1024;

    /// <summary>
    /// Recebe uma planilha (.csv ou .xlsx) e devolve 202 Accepted com o link do relatório.
    /// O processamento acontece em segundo plano (consumidor RabbitMQ).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteRequisicao)] // o Kestrel corta antes de ler tudo para a memória
    [RequestFormLimits(MultipartBodyLengthLimit = LimiteRequisicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Importar(IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo.Length > ValidadorArquivoPlanilha.TamanhoMaximoBytes)
            throw new DomainException("Arquivo maior que o limite permitido.");

        using var memoria = new MemoryStream((int)arquivo.Length);
        await arquivo.CopyToAsync(memoria, ct);

        var id = await importacoes.ReceberAsync(arquivo.FileName, arquivo.ContentType, memoria.ToArray(), ct);
        return AcceptedAtAction(nameof(ObterRelatorio), new { id }, new { importacaoId = id, status = "Pendente" });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ImportacaoRelatorio>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ImportacaoRelatorio> ObterRelatorio(Guid id, CancellationToken ct) =>
        await consultas.ObterRelatorioAsync(id, ct) ?? throw new NotFoundException($"Importação {id} não encontrada.");

    /// <summary>Modelo de planilha CSV para download.</summary>
    [HttpGet("modelo.csv")]
    [Produces("text/csv")]
    public IActionResult Modelo() => File(
        System.Text.Encoding.UTF8.GetBytes(
            "codigo;descricao;categoria;unidade;preco_custo;preco_venda;perecivel;validade_dias\n" +
            "7891000100103;Arroz Tipo 1 5kg;Mercearia;UN;18,50;27,90;nao;\n" +
            "HF-TOMATE;Tomate Italiano;Hortifruti;KG;4,20;8,99;sim;7\n"),
        "text/csv", "modelo-produtos.csv");
}
