using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Catalogo.Application.Produtos;

/// <summary>Casos de uso de produtos. Recebe DTOs, usa o agregado e confirma com o Unit of Work.</summary>
public sealed class ProdutosAppService(IProdutoRepository produtos, IUnitOfWork unitOfWork, TimeProvider relogio)
{
    public async Task<ProdutoResponse> CadastrarAsync(CadastrarProdutoRequest request, CancellationToken ct)
    {
        var codigo = CodigoProduto.De(request.Codigo);
        if (await produtos.ObterPorCodigoAsync(codigo, ct) is not null)
            throw new ConflictException($"Já existe produto com o código {codigo}.");

        var produto = Produto.Criar(codigo, MapearDados(request), relogio.GetUtcNow().UtcDateTime);
        produtos.Adicionar(produto);
        await unitOfWork.SalvarAsync(ct);
        return ProdutoResponse.De(produto);
    }

    public async Task<ProdutoResponse> AtualizarAsync(Guid id, AtualizarProdutoRequest request, CancellationToken ct)
    {
        var produto = await produtos.ObterPorIdAsync(id, ct) ?? throw new NotFoundException($"Produto {id} não encontrado.");
        produto.Atualizar(MapearDados(request), relogio.GetUtcNow().UtcDateTime);
        await unitOfWork.SalvarAsync(ct);
        return ProdutoResponse.De(produto);
    }

    public async Task<ProdutoResponse> ObterAsync(Guid id, CancellationToken ct) =>
        ProdutoResponse.De(await produtos.ObterPorIdAsync(id, ct) ?? throw new NotFoundException($"Produto {id} não encontrado."));

    public async Task<ProdutoResponse> ObterPorCodigoAsync(string codigo, CancellationToken ct) =>
        ProdutoResponse.De(await produtos.ObterPorCodigoAsync(CodigoProduto.De(codigo), ct)
                           ?? throw new NotFoundException($"Produto {codigo} não encontrado."));

    public async Task<IReadOnlyList<ProdutoResponse>> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken ct)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100); // limita paginação: evita "dump" da tabela (OWASP API4 – consumo de recursos)
        var lista = await produtos.ListarAsync(busca, pagina, tamanho, ct);
        return lista.Select(ProdutoResponse.De).ToList();
    }

    internal static DadosProduto MapearDados(AtualizarProdutoRequest r) => new(
        r.Descricao,
        r.Categoria,
        ConverterUnidade(r.Unidade),
        Dinheiro.De(r.PrecoCusto),
        Dinheiro.De(r.PrecoVenda),
        r.Perecivel,
        r.PrazoValidadeDias);

    internal static UnidadeMedida ConverterUnidade(string? unidade) =>
        Enum.TryParse<UnidadeMedida>(unidade?.Trim(), ignoreCase: true, out var u) && Enum.IsDefined(u) && !int.TryParse(unidade, out _)
            ? u
            : throw new DomainException($"Unidade de medida '{unidade}' inválida. Use: {string.Join(", ", Enum.GetNames<UnidadeMedida>())}.");
}
