using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Faturamento.Domain.Tributacao;

/// <summary>
/// ATENÇÃO: alíquotas ilustrativas para fins de demonstração. Não use para cálculo fiscal real.
/// </summary>
public enum CenarioTributario
{
    /// <summary>ICMS + PIS + COFINS.</summary>
    RegimeAtual = 1,
    /// <summary>2026: regime atual + CBS 0,9% e IBS 0,1% em caráter de teste (reforma tributária).</summary>
    Transicao2026 = 2,
    /// <summary>Cenário final da reforma: só CBS e IBS (alíquotas de referência estimadas).</summary>
    ReformaPlena = 3
}

public sealed record ItemTributavel(string ProdutoCodigo, Dinheiro ValorTotal);

public sealed record ImpostoCalculado(string Tributo, Dinheiro BaseCalculo, decimal Aliquota, Dinheiro Valor);

/// <summary>Uma regra = um tributo. Novas regras (ex.: imposto seletivo) entram sem alterar o motor (Open/Closed).</summary>
public interface IRegraTributaria
{
    string Tributo { get; }
    bool SeAplica(ItemTributavel item);
    ImpostoCalculado Calcular(ItemTributavel item);
}

public abstract class RegraAliquotaFixa(string tributo, decimal aliquota) : IRegraTributaria
{
    public string Tributo { get; } = tributo;
    public decimal Aliquota { get; } = aliquota is < 0 or > 1 ? throw new DomainException("Alíquota deve estar entre 0 e 1.") : aliquota;

    public virtual bool SeAplica(ItemTributavel item) => true;

    public ImpostoCalculado Calcular(ItemTributavel item) =>
        new(Tributo, item.ValorTotal, Aliquota, item.ValorTotal * Aliquota);
}

public sealed class RegraIcms(decimal aliquota = 0.18m) : RegraAliquotaFixa("ICMS", aliquota);
public sealed class RegraPis(decimal aliquota = 0.0165m) : RegraAliquotaFixa("PIS", aliquota);
public sealed class RegraCofins(decimal aliquota = 0.076m) : RegraAliquotaFixa("COFINS", aliquota);
public sealed class RegraCbs(decimal aliquota) : RegraAliquotaFixa("CBS", aliquota);
public sealed class RegraIbs(decimal aliquota) : RegraAliquotaFixa("IBS", aliquota);

public sealed record ResultadoTributacao(IReadOnlyList<ImpostoCalculado> PorTributo, Dinheiro ValorTotalTributos);

/// <summary>
/// Motor de regras tributárias: aplica todas as regras do cenário a cada item e consolida por tributo.
/// Fica no domínio, isolado de infraestrutura — dá para testar qualquer cenário só com testes de unidade.
/// </summary>
public sealed class MotorTributario
{
    private readonly IReadOnlyList<IRegraTributaria> _regras;

    public MotorTributario(IEnumerable<IRegraTributaria> regras)
    {
        _regras = regras.ToList();
        if (_regras.Count == 0)
            throw new DomainException("Motor tributário sem regras.");
        if (_regras.GroupBy(r => r.Tributo).Any(g => g.Count() > 1))
            throw new DomainException("Tributo duplicado no motor.");
    }

    public IReadOnlyList<string> Tributos => _regras.Select(r => r.Tributo).ToList();

    public static MotorTributario Para(CenarioTributario cenario) => cenario switch
    {
        CenarioTributario.RegimeAtual => new([new RegraIcms(), new RegraPis(), new RegraCofins()]),
        CenarioTributario.Transicao2026 => new([new RegraIcms(), new RegraPis(), new RegraCofins(), new RegraCbs(0.009m), new RegraIbs(0.001m)]),
        CenarioTributario.ReformaPlena => new([new RegraCbs(0.088m), new RegraIbs(0.177m)]),
        _ => throw new DomainException($"Cenário tributário {cenario} não suportado.")
    };

    public ResultadoTributacao Calcular(IEnumerable<ItemTributavel> itens)
    {
        var porItem = itens.SelectMany(item => _regras.Where(r => r.SeAplica(item)).Select(r => r.Calcular(item))).ToList();

        var consolidado = porItem
            .GroupBy(i => i.Tributo)
            .Select(g => new ImpostoCalculado(
                g.Key,
                g.Aggregate(Dinheiro.Zero, (s, i) => s + i.BaseCalculo),
                g.First().Aliquota,
                g.Aggregate(Dinheiro.Zero, (s, i) => s + i.Valor)))
            .ToList();

        return new ResultadoTributacao(consolidado, consolidado.Aggregate(Dinheiro.Zero, (s, i) => s + i.Valor));
    }
}
