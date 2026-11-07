using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.UnitTests.Catalogo;

public class ProdutoTests
{
    private static readonly DateTime Agora = new(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);

    internal static DadosProduto Dados(bool perecivel = false, int? prazo = null, string descricao = "Arroz Tipo 1 5kg",
        decimal custo = 18.5m, decimal venda = 27.9m) =>
        new(descricao, "Mercearia", UnidadeMedida.UN, Dinheiro.De(custo), Dinheiro.De(venda), perecivel, prazo);

    [Fact]
    public void Criar_gera_domain_event_de_cadastro()
    {
        var produto = Produto.Criar(CodigoProduto.De("789"), Dados(), Agora);

        produto.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProdutoCadastradoDomainEvent>();
        produto.CriadoEm.Should().Be(Agora);
    }

    [Fact]
    public void Calcula_margem_sobre_preco_de_venda() =>
        Produto.Criar(CodigoProduto.De("789"), Dados(custo: 6, venda: 10), Agora).MargemPercentual.Should().Be(40m);

    [Fact]
    public void Perecivel_exige_prazo_de_validade() =>
        FluentActions.Invoking(() => Produto.Criar(CodigoProduto.De("HF-1"), Dados(perecivel: true), Agora))
            .Should().Throw<DomainException>().WithMessage("*prazo de validade*");

    [Fact]
    public void Nao_perecivel_nao_aceita_prazo() =>
        FluentActions.Invoking(() => Produto.Criar(CodigoProduto.De("X1"), Dados(perecivel: false, prazo: 10), Agora))
            .Should().Throw<DomainException>();

    [Fact]
    public void Preco_de_venda_zero_e_invalido() =>
        FluentActions.Invoking(() => Produto.Criar(CodigoProduto.De("X1"), Dados(venda: 0), Agora))
            .Should().Throw<DomainException>();

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")")]
    [InlineData("+cmd")]
    [InlineData("@SUM(1)")]
    public void Bloqueia_injecao_de_formula_na_descricao(string descricao) =>
        FluentActions.Invoking(() => Produto.Criar(CodigoProduto.De("X1"), Dados(descricao: descricao), Agora))
            .Should().Throw<DomainException>();

    [Fact]
    public void Atualizar_altera_dados_e_nao_gera_novo_evento_de_cadastro()
    {
        var produto = Produto.Criar(CodigoProduto.De("789"), Dados(), Agora);
        produto.ClearDomainEvents();

        produto.Atualizar(Dados(venda: 29.9m), Agora.AddDays(1));

        produto.PrecoVenda.Valor.Should().Be(29.9m);
        produto.AtualizadoEm.Should().Be(Agora.AddDays(1));
        produto.DomainEvents.Should().BeEmpty();
    }
}
