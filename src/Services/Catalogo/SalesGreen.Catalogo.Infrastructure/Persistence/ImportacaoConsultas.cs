using Dapper;
using Npgsql;
using SalesGreen.Catalogo.Application.Importacoes;

namespace SalesGreen.Catalogo.Infrastructure.Persistence;

/// <summary>
/// Relatório de importação com Dapper: SQL explícito, sem change tracking e sem materializar o agregado
/// (que carregaria o arquivo inteiro em memória). Leitura rápida e previsível.
/// </summary>
public sealed class ImportacaoConsultas(NpgsqlDataSource dataSource) : IImportacaoConsultas
{
    private const string SqlCabecalho = """
        SELECT i."Id", i."NomeArquivo", i."Formato", i."Status", i."RecebidaEm", i."ProcessadaEm",
               i."TotalLinhas", i."LinhasAceitas", i."MensagemErro"
          FROM importacoes i
         WHERE i."Id" = @Id
        """;

    private const string SqlRejeicoes = """
        SELECT r."NumeroLinha", r."CodigoProduto", r."Motivo"
          FROM importacao_linhas_rejeitadas r
         WHERE r."ImportacaoId" = @Id
         ORDER BY r."NumeroLinha"
         LIMIT 1000
        """;

    public async Task<ImportacaoRelatorio?> ObterRelatorioAsync(Guid importacaoId, CancellationToken ct)
    {
        await using var conexao = await dataSource.OpenConnectionAsync(ct);
        var cabecalho = await conexao.QuerySingleOrDefaultAsync<CabecalhoRow>(
            new CommandDefinition(SqlCabecalho, new { Id = importacaoId }, cancellationToken: ct));
        if (cabecalho is null) return null;

        var rejeicoes = (await conexao.QueryAsync<LinhaRejeitadaDto>(
            new CommandDefinition(SqlRejeicoes, new { Id = importacaoId }, cancellationToken: ct))).ToList();

        return new ImportacaoRelatorio(
            cabecalho.Id, cabecalho.NomeArquivo, cabecalho.Formato, cabecalho.Status, cabecalho.RecebidaEm, cabecalho.ProcessadaEm,
            cabecalho.TotalLinhas, cabecalho.LinhasAceitas, cabecalho.TotalLinhas - cabecalho.LinhasAceitas, cabecalho.MensagemErro, rejeicoes);
    }

    private sealed record CabecalhoRow(
        Guid Id, string NomeArquivo, string Formato, string Status, DateTime RecebidaEm, DateTime? ProcessadaEm,
        int TotalLinhas, int LinhasAceitas, string? MensagemErro);
}
