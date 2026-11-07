using SalesGreen.Faturamento.Domain;
using SalesGreen.Faturamento.Domain.Tributacao;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.UnitTests.Faturamento;

public class MotorTributarioTests
{
    private static readonly ItemTributavel[] Itens =
    [
        new("789", Dinheiro.De(100m)),
        new("HF-1", Dinheiro.De(50m))
    ];

    [Fact]
    public void Regime_atual_calcula_icms_pis_cofins_consolidados()
    {
        var resultado = MotorTributario.Para(CenarioTributario.RegimeAtual).Calcular(Itens);

        resultado.PorTributo.Select(t => t.Tributo).Should().Equal("ICMS", "PIS", "COFINS");
        resultado.PorTributo.Single(t => t.Tributo == "ICMS").Valor.Valor.Should().Be(27m);     // 18% de 150
        resultado.PorTributo.Single(t => t.Tributo == "PIS").Valor.Valor.Should().Be(2.48m);    // 1,65 + 0,83 (arredondado por item)
        resultado.PorTributo.Single(t => t.Tributo == "COFINS").Valor.Valor.Should().Be(11.40m); // 7,6% de 150
        resultado.ValorTotalTributos.Valor.Should().Be(40.88m);
    }

    [Fact]
    public void Transicao_2026_adiciona_cbs_e_ibs_de_teste()
    {
        var resultado = MotorTributario.Para(CenarioTributario.Transicao2026).Calcular(Itens);

        resultado.PorTributo.Select(t => t.Tributo).Should().Contain(["ICMS", "PIS", "COFINS", "CBS", "IBS"]);
        resultado.PorTributo.Single(t => t.Tributo == "CBS").Valor.Valor.Should().Be(1.35m); // 0,9%
        resultado.PorTributo.Single(t => t.Tributo == "IBS").Valor.Valor.Should().Be(0.15m); // 0,1%
    }

    [Fact]
    public void Reforma_plena_substitui_os_tributos_atuais() =>
        MotorTributario.Para(CenarioTributario.ReformaPlena).Calcular(Itens).PorTributo.Select(t => t.Tributo)
            .Should().BeEquivalentTo(["CBS", "IBS"]);

    [Fact]
    public void Novas_regras_entram_sem_alterar_o_motor()
    {
        var motor = new MotorTributario([new RegraIcms(0.12m), new RegraSeletivo()]);

        var resultado = motor.Calcular(Itens);

        resultado.PorTributo.Single(t => t.Tributo == "IS").Valor.Valor.Should().Be(10m); // só no item 789
    }

    [Fact]
    public void Tributo_duplicado_e_rejeitado() =>
        FluentActions.Invoking(() => new MotorTributario([new RegraIcms(), new RegraIcms(0.12m)])).Should().Throw<DomainException>();

    /// <summary>Exemplo de regra condicional (imposto seletivo fictício só para um produto).</summary>
    private sealed class RegraSeletivo() : RegraAliquotaFixa("IS", 0.10m)
    {
        public override bool SeAplica(ItemTributavel item) => item.ProdutoCodigo == "789";
    }
}

public class NotaFiscalTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Emite_nota_com_totais_e_tributos()
    {
        var nota = NotaFiscal.Emitir(Guid.NewGuid(), "Mercado", 15,
            [new ItemNota("789", "Arroz", Quantidade.De(2), Dinheiro.De(27.90m))], CenarioTributario.RegimeAtual, Agora);

        nota.ValorProdutos.Valor.Should().Be(55.80m);
        nota.Tributos.Should().HaveCount(3);
        nota.ValorTributos.Valor.Should().Be(nota.Tributos.Sum(t => t.Valor.Valor));
        nota.ChaveAcesso.Should().StartWith("SIMULADA-202611-001-000000015");
    }

    [Fact]
    public void Valor_acima_do_limite_falha() =>
        FluentActions.Invoking(() => NotaFiscal.Emitir(Guid.NewGuid(), "Mercado", 1,
                [new ItemNota("789", "TV", Quantidade.De(10), Dinheiro.De(6000m))], CenarioTributario.RegimeAtual, Agora))
            .Should().Throw<DomainException>().WithMessage("*limite*");
}
