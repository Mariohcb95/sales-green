using SalesGreen.SharedKernel;

namespace SalesGreen.Catalogo.Domain.Importacoes;

public enum FormatoPlanilha
{
    Csv = 1,
    Xlsx = 2
}

public enum StatusImportacao
{
    Pendente = 1,
    Concluida = 2,
    Falhou = 3
}

/// <summary>
/// Agregado que representa um pedido de importação de planilha. Guarda o arquivo recebido
/// e, após o processamento assíncrono, o relatório de linhas aceitas e rejeitadas.
/// </summary>
public sealed class Importacao : AggregateRoot
{
    private readonly List<LinhaRejeitada> _rejeicoes = [];

    public string NomeArquivo { get; private set; } = string.Empty;
    public FormatoPlanilha Formato { get; private set; }
    public byte[] Conteudo { get; private set; } = [];
    public StatusImportacao Status { get; private set; }
    public DateTime RecebidaEm { get; private set; }
    public DateTime? ProcessadaEm { get; private set; }
    public int TotalLinhas { get; private set; }
    public int LinhasAceitas { get; private set; }
    public string? MensagemErro { get; private set; }
    public IReadOnlyCollection<LinhaRejeitada> Rejeicoes => _rejeicoes.AsReadOnly();

    private Importacao() { } // EF Core

    public static Importacao Receber(string nomeArquivo, FormatoPlanilha formato, byte[] conteudo, DateTime agora)
    {
        if (conteudo.Length == 0)
            throw new DomainException("Arquivo vazio.");

        var importacao = new Importacao
        {
            NomeArquivo = Path.GetFileName(nomeArquivo),
            Formato = formato,
            Conteudo = conteudo,
            Status = StatusImportacao.Pendente,
            RecebidaEm = agora
        };
        importacao.Raise(new ImportacaoRecebidaDomainEvent(importacao.Id));
        return importacao;
    }

    public bool JaProcessada => Status != StatusImportacao.Pendente;

    public void Concluir(int totalLinhas, int linhasAceitas, IEnumerable<LinhaRejeitada> rejeicoes, DateTime agora)
    {
        GarantirPendente();
        TotalLinhas = totalLinhas;
        LinhasAceitas = linhasAceitas;
        _rejeicoes.AddRange(rejeicoes);
        Status = StatusImportacao.Concluida;
        ProcessadaEm = agora;
        Conteudo = []; // o arquivo não é mais necessário: libera espaço no banco
    }

    public void Falhar(string motivo, DateTime agora)
    {
        GarantirPendente();
        MensagemErro = motivo.Length > 500 ? motivo[..500] : motivo;
        Status = StatusImportacao.Falhou;
        ProcessadaEm = agora;
    }

    private void GarantirPendente()
    {
        if (JaProcessada)
            throw new DomainException("Importação já foi processada.");
    }
}

public sealed class LinhaRejeitada : Entity
{
    public int NumeroLinha { get; private set; }
    public string? CodigoProduto { get; private set; }
    public string Motivo { get; private set; } = string.Empty;

    private LinhaRejeitada() { } // EF Core

    public LinhaRejeitada(int numeroLinha, string? codigoProduto, string motivo)
    {
        NumeroLinha = numeroLinha;
        CodigoProduto = codigoProduto is { Length: > 30 } ? codigoProduto[..30] : codigoProduto;
        Motivo = motivo.Length > 300 ? motivo[..300] : motivo;
    }
}

public sealed record ImportacaoRecebidaDomainEvent(Guid ImportacaoId) : IDomainEvent;

public interface IImportacaoRepository
{
    Task<Importacao?> ObterPorIdAsync(Guid id, CancellationToken ct);
    void Adicionar(Importacao importacao);
}
