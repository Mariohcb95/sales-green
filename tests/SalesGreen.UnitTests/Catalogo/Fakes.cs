using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.UnitTests.Catalogo;

/// <summary>Repositórios em memória: testam o caso de uso sem banco (o banco é coberto nos testes de integração).</summary>
internal sealed class ProdutoRepositoryFake : IProdutoRepository
{
    public List<Produto> Produtos { get; } = [];

    public Task<Produto?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Produtos.FirstOrDefault(p => p.Id == id));

    public Task<Produto?> ObterPorCodigoAsync(CodigoProduto codigo, CancellationToken ct) =>
        Task.FromResult(Produtos.FirstOrDefault(p => p.Codigo == codigo));

    public Task<IReadOnlyDictionary<string, Produto>> ObterPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, Produto>>(Produtos.Where(p => codigos.Contains(p.Codigo.Valor)).ToDictionary(p => p.Codigo.Valor));

    public Task<IReadOnlyList<Produto>> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Produto>>(Produtos);

    public void Adicionar(Produto produto) => Produtos.Add(produto);
}

internal sealed class ImportacaoRepositoryFake : IImportacaoRepository
{
    public List<Importacao> Importacoes { get; } = [];

    public Task<Importacao?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Importacoes.FirstOrDefault(i => i.Id == id));

    public void Adicionar(Importacao importacao) => Importacoes.Add(importacao);
}

internal sealed class UnitOfWorkFake : IUnitOfWork
{
    public int Salvamentos { get; private set; }

    public Task SalvarAsync(CancellationToken ct)
    {
        Salvamentos++;
        return Task.CompletedTask;
    }
}

internal sealed class RelogioFixo(DateTime agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(agora, TimeSpan.Zero);
}
