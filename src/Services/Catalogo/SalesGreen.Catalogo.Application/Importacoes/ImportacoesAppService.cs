using Microsoft.Extensions.Logging;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.SharedKernel;

namespace SalesGreen.Catalogo.Application.Importacoes;

/// <summary>
/// Importação assíncrona de produtos:
/// 1) <see cref="ReceberAsync"/> (HTTP) valida o arquivo, grava a importação e — via domain event + Outbox —
///    publica ImportacaoProdutosSolicitada, respondendo 202 Accepted imediatamente;
/// 2) <see cref="ProcessarAsync"/> (consumidor RabbitMQ) lê as linhas, faz upsert por código e grava o relatório.
/// </summary>
public sealed class ImportacoesAppService(
    IImportacaoRepository importacoes,
    IProdutoRepository produtos,
    IUnitOfWork unitOfWork,
    IEnumerable<IPlanilhaLeitor> leitores,
    TimeProvider relogio,
    ILogger<ImportacoesAppService> logger)
{
    public async Task<Guid> ReceberAsync(string nomeArquivo, string? contentType, byte[] conteudo, CancellationToken ct)
    {
        var formato = ValidadorArquivoPlanilha.Validar(nomeArquivo, contentType, conteudo);
        var importacao = Importacao.Receber(nomeArquivo, formato, conteudo, Agora);
        importacoes.Adicionar(importacao);
        await unitOfWork.SalvarAsync(ct);
        logger.LogInformation("Importação {ImportacaoId} recebida ({Formato}, {Bytes} bytes)", importacao.Id, formato, conteudo.Length);
        return importacao.Id;
    }

    public async Task ProcessarAsync(Guid importacaoId, CancellationToken ct)
    {
        var importacao = await importacoes.ObterPorIdAsync(importacaoId, ct);
        if (importacao is null)
        {
            logger.LogWarning("Importação {ImportacaoId} não encontrada", importacaoId);
            return;
        }
        if (importacao.JaProcessada)
        {
            logger.LogInformation("Importação {ImportacaoId} já processada: mensagem ignorada (idempotência)", importacaoId);
            return;
        }

        try
        {
            var leitor = leitores.Single(l => l.Formato == importacao.Formato);
            var linhas = leitor.Ler(importacao.Conteudo).Take(ValidadorArquivoPlanilha.MaximoLinhas + 1).ToList();
            if (linhas.Count > ValidadorArquivoPlanilha.MaximoLinhas)
                throw new DomainException($"Planilha excede o limite de {ValidadorArquivoPlanilha.MaximoLinhas} linhas.");

            var (aceitas, rejeicoes) = await ImportarLinhasAsync(linhas, ct);
            importacao.Concluir(linhas.Count, aceitas, rejeicoes, Agora);
            logger.LogInformation("Importação {ImportacaoId} concluída: {Aceitas} aceitas, {Rejeitadas} rejeitadas",
                importacaoId, aceitas, rejeicoes.Count);
        }
        catch (DomainException ex)
        {
            importacao.Falhar(ex.Message, Agora);
            logger.LogWarning("Importação {ImportacaoId} falhou: {Motivo}", importacaoId, ex.Message);
        }

        await unitOfWork.SalvarAsync(ct);
    }

    private async Task<(int Aceitas, List<LinhaRejeitada> Rejeicoes)> ImportarLinhasAsync(List<LinhaPlanilha> linhas, CancellationToken ct)
    {
        // Carrega de uma vez os produtos já existentes (evita N+1 consultas).
        var codigos = linhas.Select(l => l.Campo(LayoutPlanilhaProdutos.Codigo)?.ToUpperInvariant())
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).Distinct().ToList();
        var existentes = await produtos.ObterPorCodigosAsync(codigos, ct);

        var rejeicoes = new List<LinhaRejeitada>();
        var codigosNaPlanilha = new Dictionary<string, int>();
        var aceitas = 0;

        foreach (var linha in linhas)
        {
            try
            {
                var (codigo, dados) = ConversorLinhaProduto.Converter(linha);
                if (!codigosNaPlanilha.TryAdd(codigo.Valor, linha.Numero))
                    throw new DomainException($"Código duplicado na planilha (já informado na linha {codigosNaPlanilha[codigo.Valor]}).");

                // Idempotência: o código do produto é a chave natural. Se existe, atualiza; senão, cria.
                if (existentes.TryGetValue(codigo.Valor, out var produto))
                {
                    produto.Atualizar(dados, Agora);
                }
                else
                {
                    produtos.Adicionar(Produto.Criar(codigo, dados, Agora));
                }
                aceitas++;
            }
            catch (DomainException ex)
            {
                rejeicoes.Add(new LinhaRejeitada(linha.Numero, linha.Campo(LayoutPlanilhaProdutos.Codigo), ex.Message));
            }
        }

        return (aceitas, rejeicoes);
    }

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;
}
