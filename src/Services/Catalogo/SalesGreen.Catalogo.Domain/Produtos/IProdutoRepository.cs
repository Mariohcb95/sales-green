using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Catalogo.Domain.Produtos;

public interface IProdutoRepository
{
    Task<Produto?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<Produto?> ObterPorCodigoAsync(CodigoProduto codigo, CancellationToken ct);
    Task<IReadOnlyDictionary<string, Produto>> ObterPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct);
    Task<IReadOnlyList<Produto>> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken ct);
    void Adicionar(Produto produto);
}
