using SalesGreen.Faturamento.Domain.Tributacao;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Faturamento.Domain;

/// <summary>
/// Nota fiscal SIMULADA (não há integração com a SEFAZ). Um pedido gera no máximo uma nota (PedidoId único).
/// </summary>
public sealed class NotaFiscal : AggregateRoot
{
    /// <summary>Limite de demonstração: acima dele o faturamento falha e a Saga executa a compensação.</summary>
    public static readonly Dinheiro ValorMaximoEmissao = Dinheiro.De(50_000m);
    public const int Serie = 1;

    private readonly List<ItemNota> _itens = [];
    private readonly List<TributoNota> _tributos = [];

    public Guid PedidoId { get; private set; }
    public long Numero { get; private set; }
    public string ChaveAcesso { get; private set; } = string.Empty;
    public string Cliente { get; private set; } = string.Empty;
    public CenarioTributario Cenario { get; private set; }
    public DateTime EmitidaEm { get; private set; }
    public Dinheiro ValorProdutos { get; private set; }
    public Dinheiro ValorTributos { get; private set; }
    public IReadOnlyCollection<ItemNota> Itens => _itens.AsReadOnly();
    public IReadOnlyCollection<TributoNota> Tributos => _tributos.AsReadOnly();

    private NotaFiscal() { } // EF Core

    public static NotaFiscal Emitir(Guid pedidoId, string cliente, long numero, IReadOnlyCollection<ItemNota> itens,
        CenarioTributario cenario, DateTime agora)
    {
        if (itens.Count == 0)
            throw new DomainException("Nota fiscal sem itens.");
        if (numero <= 0)
            throw new DomainException("Número da nota inválido.");

        var valorProdutos = itens.Aggregate(Dinheiro.Zero, (s, i) => s + i.ValorTotal);
        if (valorProdutos > ValorMaximoEmissao)
            throw new DomainException($"Valor {valorProdutos} excede o limite de emissão simulada ({ValorMaximoEmissao}).");

        var resultado = MotorTributario.Para(cenario)
            .Calcular(itens.Select(i => new ItemTributavel(i.ProdutoCodigo, i.ValorTotal)));

        var nota = new NotaFiscal
        {
            PedidoId = pedidoId,
            Numero = numero,
            ChaveAcesso = $"SIMULADA-{agora:yyyyMM}-{Serie:000}-{numero:000000000}",
            Cliente = cliente,
            Cenario = cenario,
            EmitidaEm = agora,
            ValorProdutos = valorProdutos,
            ValorTributos = resultado.ValorTotalTributos
        };
        nota._itens.AddRange(itens);
        nota._tributos.AddRange(resultado.PorTributo.Select(t => new TributoNota(t.Tributo, t.BaseCalculo, t.Aliquota, t.Valor)));
        return nota;
    }
}

public sealed class ItemNota : Entity
{
    public string ProdutoCodigo { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public Quantidade Quantidade { get; private set; }
    public Dinheiro ValorUnitario { get; private set; }
    public Dinheiro ValorTotal { get; private set; }

    private ItemNota() { } // EF Core

    public ItemNota(string produtoCodigo, string descricao, Quantidade quantidade, Dinheiro valorUnitario)
    {
        ProdutoCodigo = produtoCodigo;
        Descricao = descricao;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
        ValorTotal = valorUnitario * quantidade.Valor;
    }
}

public sealed class TributoNota : Entity
{
    public string Tributo { get; private set; } = string.Empty;
    public Dinheiro BaseCalculo { get; private set; }
    public decimal Aliquota { get; private set; }
    public Dinheiro Valor { get; private set; }

    private TributoNota() { } // EF Core

    public TributoNota(string tributo, Dinheiro baseCalculo, decimal aliquota, Dinheiro valor)
    {
        Tributo = tributo;
        BaseCalculo = baseCalculo;
        Aliquota = aliquota;
        Valor = valor;
    }
}

public interface INotaFiscalRepository
{
    Task<NotaFiscal?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<NotaFiscal?> ObterPorPedidoAsync(Guid pedidoId, CancellationToken ct);
    Task<long> ProximoNumeroAsync(CancellationToken ct);
    void Adicionar(NotaFiscal nota);
}
