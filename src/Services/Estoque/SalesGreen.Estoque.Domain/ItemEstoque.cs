using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Estoque.Domain;

/// <summary>
/// Agregado com o saldo de UM produto, dividido em lotes. Para perecíveis cada lote tem validade
/// e a reserva segue FEFO (First Expired, First Out): sai primeiro o que vence primeiro — reduz perdas.
/// </summary>
public sealed class ItemEstoque : AggregateRoot
{
    public const string LotePadrao = "UNICO";

    private readonly List<Lote> _lotes = [];

    public string ProdutoCodigo { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public bool Perecivel { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    /// <summary>Versão do agregado: incrementada a cada alteração e usada como token de concorrência otimista.</summary>
    public int Versao { get; private set; }

    public IReadOnlyCollection<Lote> Lotes => _lotes.AsReadOnly();

    private ItemEstoque() { } // EF Core

    public static ItemEstoque Registrar(CodigoProduto codigo, string descricao, bool perecivel, DateTime agora) => new()
    {
        ProdutoCodigo = codigo.Valor,
        Descricao = descricao,
        Perecivel = perecivel,
        AtualizadoEm = agora
    };

    /// <summary>Saldo que pode ser vendido: lotes não vencidos.</summary>
    public Quantidade SaldoDisponivel(DateOnly hoje) =>
        _lotes.Where(l => !l.Vencido(hoje)).Aggregate(Quantidade.Zero, (s, l) => s + l.Disponivel);

    public void DarEntrada(Quantidade quantidade, string? codigoLote, DateOnly? validade, DateTime agora)
    {
        if (quantidade <= Quantidade.Zero)
            throw new DomainException("Quantidade de entrada deve ser maior que zero.");

        var hoje = DateOnly.FromDateTime(agora);
        if (Perecivel)
        {
            if (validade is null)
                throw new DomainException("Produto perecível exige data de validade na entrada.");
            if (validade <= hoje)
                throw new DomainException("Não é possível dar entrada em lote já vencido.");
        }
        else
        {
            validade = null;
        }

        var loteCodigo = string.IsNullOrWhiteSpace(codigoLote) ? LotePadrao : codigoLote.Trim().ToUpperInvariant();
        if (loteCodigo.Length > 40)
            throw new DomainException("Código do lote deve ter no máximo 40 caracteres.");

        var lote = _lotes.FirstOrDefault(l => l.Codigo == loteCodigo && l.Validade == validade);
        if (lote is null)
            _lotes.Add(new Lote(loteCodigo, validade, quantidade));
        else
            lote.Adicionar(quantidade);

        Tocar(agora);
    }

    public bool PodeReservar(Quantidade quantidade, DateOnly hoje) => SaldoDisponivel(hoje) >= quantidade;

    /// <summary>Retira a quantidade dos lotes em ordem FEFO e devolve de quais lotes saiu (para poder compensar).</summary>
    public IReadOnlyList<(Guid LoteId, Quantidade Quantidade)> Reservar(Quantidade quantidade, DateTime agora)
    {
        var hoje = DateOnly.FromDateTime(agora);
        if (!PodeReservar(quantidade, hoje))
            throw new DomainException($"Saldo insuficiente para {ProdutoCodigo}: disponível {SaldoDisponivel(hoje)}, solicitado {quantidade}.");

        var alocacoes = new List<(Guid, Quantidade)>();
        var restante = quantidade;
        foreach (var lote in _lotes.Where(l => !l.Vencido(hoje) && l.Disponivel > Quantidade.Zero)
                                   .OrderBy(l => l.Validade ?? DateOnly.MaxValue))
        {
            var retirar = Quantidade.Min(lote.Disponivel, restante);
            lote.Retirar(retirar);
            alocacoes.Add((lote.Id, retirar));
            restante -= retirar;
            if (restante == Quantidade.Zero) break;
        }

        Tocar(agora);
        return alocacoes;
    }

    /// <summary>Compensação: devolve ao lote de origem o que foi reservado.</summary>
    public void Devolver(Guid loteId, Quantidade quantidade, DateTime agora)
    {
        var lote = _lotes.FirstOrDefault(l => l.Id == loteId)
                   ?? throw new DomainException($"Lote {loteId} não pertence ao produto {ProdutoCodigo}.");
        lote.Adicionar(quantidade);
        Tocar(agora);
    }

    private void Tocar(DateTime agora)
    {
        AtualizadoEm = agora;
        Versao++; // garante UPDATE na linha do agregado → o EF confere a versão lida (concorrência otimista)
        Raise(new SaldoAlteradoDomainEvent(ProdutoCodigo, SaldoDisponivel(DateOnly.FromDateTime(agora)).Valor, agora));
    }
}

public sealed class Lote : Entity
{
    public string Codigo { get; private set; } = string.Empty;
    public DateOnly? Validade { get; private set; }
    public Quantidade Disponivel { get; private set; }

    private Lote() { } // EF Core

    internal Lote(string codigo, DateOnly? validade, Quantidade quantidade)
    {
        Codigo = codigo;
        Validade = validade;
        Disponivel = quantidade;
    }

    public bool Vencido(DateOnly hoje) => Validade is not null && Validade <= hoje;

    internal void Adicionar(Quantidade quantidade) => Disponivel += quantidade;

    internal void Retirar(Quantidade quantidade) => Disponivel -= quantidade; // Quantidade nunca fica negativa (VO valida)
}

public sealed record SaldoAlteradoDomainEvent(string ProdutoCodigo, decimal SaldoDisponivel, DateTime AlteradoEm) : IDomainEvent;
