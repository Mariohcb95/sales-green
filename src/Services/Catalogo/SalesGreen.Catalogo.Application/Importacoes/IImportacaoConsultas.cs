namespace SalesGreen.Catalogo.Application.Importacoes;

/// <summary>
/// Lado de leitura (CQRS "leve"): o relatório é uma consulta pura, sem regra de negócio,
/// então é implementado com Dapper/SQL direto em vez de carregar o agregado pelo EF.
/// </summary>
public interface IImportacaoConsultas
{
    Task<ImportacaoRelatorio?> ObterRelatorioAsync(Guid importacaoId, CancellationToken ct);
}

public sealed record ImportacaoRelatorio(
    Guid Id,
    string NomeArquivo,
    string Formato,
    string Status,
    DateTime RecebidaEm,
    DateTime? ProcessadaEm,
    int TotalLinhas,
    int LinhasAceitas,
    int LinhasRejeitadas,
    string? MensagemErro,
    IReadOnlyList<LinhaRejeitadaDto> Rejeicoes);

public sealed record LinhaRejeitadaDto(int NumeroLinha, string? CodigoProduto, string Motivo);
