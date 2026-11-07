using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.UnitTests.SharedKernel;

public class DinheiroTests
{
    [Fact]
    public void Arredonda_para_duas_casas_com_meio_para_cima()
    {
        Dinheiro.De(10.005m).Valor.Should().Be(10.01m);
        Dinheiro.De(10.004m).Valor.Should().Be(10.00m);
    }

    [Fact]
    public void Nao_aceita_valor_negativo() =>
        FluentActions.Invoking(() => Dinheiro.De(-0.01m)).Should().Throw<DomainException>();

    [Fact]
    public void Igualdade_por_valor() => Dinheiro.De(5m).Should().Be(Dinheiro.De(5.00m));

    [Fact]
    public void Multiplicacao_aplica_arredondamento() => (Dinheiro.De(8.99m) * 1.375m).Valor.Should().Be(12.36m);
}

public class QuantidadeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(0.5)]
    [InlineData(1.255)]
    public void Aceita_ate_tres_casas(decimal valor) => Quantidade.Positiva(valor).Valor.Should().Be(valor);

    [Fact]
    public void Rejeita_mais_de_tres_casas() =>
        FluentActions.Invoking(() => Quantidade.De(1.2345m)).Should().Throw<DomainException>();

    [Fact]
    public void Subtracao_nao_pode_ficar_negativa() =>
        FluentActions.Invoking(() => Quantidade.De(1) - Quantidade.De(2)).Should().Throw<DomainException>();

    [Fact]
    public void Positiva_rejeita_zero() =>
        FluentActions.Invoking(() => Quantidade.Positiva(0)).Should().Throw<DomainException>();
}

public class CodigoProdutoTests
{
    [Fact]
    public void Normaliza_espacos_e_caixa() => CodigoProduto.De("  hf-tomate ").Valor.Should().Be("HF-TOMATE");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("COD COM ESPACO")]
    [InlineData("=CMD()")]
    [InlineData("1234567890123456789012345678901")]
    public void Rejeita_codigos_invalidos(string codigo) =>
        FluentActions.Invoking(() => CodigoProduto.De(codigo)).Should().Throw<DomainException>();
}
