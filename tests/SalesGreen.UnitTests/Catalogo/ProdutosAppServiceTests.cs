using SalesGreen.Catalogo.Application.Produtos;
using SalesGreen.SharedKernel;

namespace SalesGreen.UnitTests.Catalogo;

public class ProdutosAppServiceTests
{
    private readonly ProdutoRepositoryFake _repo = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ProdutosAppService _service;

    public ProdutosAppServiceTests() =>
        _service = new ProdutosAppService(_repo, _uow, new RelogioFixo(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)));

    private static CadastrarProdutoRequest Request(string codigo = "789", string unidade = "un") =>
        new(codigo, "Feijão Carioca 1kg", "Mercearia", unidade, 5.5m, 8.99m, false, null);

    [Fact]
    public async Task Cadastra_e_salva()
    {
        var resposta = await _service.CadastrarAsync(Request(), default);

        resposta.Codigo.Should().Be("789");
        resposta.Unidade.Should().Be("UN");
        _repo.Produtos.Should().ContainSingle();
        _uow.Salvamentos.Should().Be(1);
    }

    [Fact]
    public async Task Codigo_duplicado_gera_conflito()
    {
        await _service.CadastrarAsync(Request(), default);

        await FluentActions.Awaiting(() => _service.CadastrarAsync(Request(" 789 "), default))
            .Should().ThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Unidade_invalida_gera_erro_de_negocio(string unidade) =>
        await FluentActions.Awaiting(() => _service.CadastrarAsync(Request(unidade: unidade), default))
            .Should().ThrowAsync<DomainException>();

    [Fact]
    public async Task Atualizar_produto_inexistente_gera_not_found() =>
        await FluentActions.Awaiting(() => _service.AtualizarAsync(Guid.NewGuid(), Request(), default))
            .Should().ThrowAsync<NotFoundException>();
}
