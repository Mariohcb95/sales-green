using Microsoft.EntityFrameworkCore;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Catalogo.Infrastructure.Persistence;

public sealed class ProdutoRepository(CatalogoDbContext db) : IProdutoRepository
{
    public Task<Produto?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Produtos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Produto?> ObterPorCodigoAsync(CodigoProduto codigo, CancellationToken ct) =>
        db.Produtos.FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

    public async Task<IReadOnlyDictionary<string, Produto>> ObterPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct)
    {
        var validos = codigos.Select(TentarCodigo).OfType<CodigoProduto>().ToList();
        var produtos = await db.Produtos.Where(p => validos.Contains(p.Codigo)).ToListAsync(ct);
        return produtos.ToDictionary(p => p.Codigo.Valor);
    }

    public async Task<IReadOnlyList<Produto>> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken ct)
    {
        var query = db.Produtos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
            query = query.Where(p => EF.Functions.ILike(p.Descricao, $"%{busca.Trim()}%")); // parametrizado: sem SQL injection
        return await query.OrderBy(p => p.Descricao).Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
    }

    public void Adicionar(Produto produto) => db.Produtos.Add(produto);

    private static CodigoProduto? TentarCodigo(string codigo)
    {
        try { return CodigoProduto.De(codigo); }
        catch (SalesGreen.SharedKernel.DomainException) { return null; }
    }
}

public sealed class ImportacaoRepository(CatalogoDbContext db) : IImportacaoRepository
{
    public Task<Importacao?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Importacoes.FirstOrDefaultAsync(i => i.Id == id, ct);

    public void Adicionar(Importacao importacao) => db.Importacoes.Add(importacao);
}
