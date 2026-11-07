using System.ComponentModel.DataAnnotations;
using SalesGreen.Catalogo.Domain.Produtos;

namespace SalesGreen.Catalogo.Application.Produtos;

/// <summary>
/// DTOs de entrada: validação "de formato" com DataAnnotations (400 automático pelo [ApiController]).
/// As regras de negócio continuam no agregado (422).
/// </summary>
public record AtualizarProdutoRequest(
    [Required, StringLength(120)] string Descricao,
    [Required, StringLength(60)] string Categoria,
    [Required, StringLength(5)] string Unidade,
    [Range(0, 9_999_999)] decimal PrecoCusto,
    [Range(0.01, 9_999_999)] decimal PrecoVenda,
    bool Perecivel,
    [Range(1, Produto.PrazoValidadeMaximoDias)] int? PrazoValidadeDias);

public sealed record CadastrarProdutoRequest(
    [Required, StringLength(30)] string Codigo,
    [Required, StringLength(120)] string Descricao,
    [Required, StringLength(60)] string Categoria,
    [Required, StringLength(5)] string Unidade,
    [Range(0, 9_999_999)] decimal PrecoCusto,
    [Range(0.01, 9_999_999)] decimal PrecoVenda,
    bool Perecivel,
    [Range(1, Produto.PrazoValidadeMaximoDias)] int? PrazoValidadeDias)
    : AtualizarProdutoRequest(Descricao, Categoria, Unidade, PrecoCusto, PrecoVenda, Perecivel, PrazoValidadeDias);

public sealed record ProdutoResponse(
    Guid Id,
    string Codigo,
    string Descricao,
    string Categoria,
    string Unidade,
    decimal PrecoCusto,
    decimal PrecoVenda,
    decimal MargemPercentual,
    bool Perecivel,
    int? PrazoValidadeDias,
    DateTime CriadoEm,
    DateTime AtualizadoEm)
{
    public static ProdutoResponse De(Produto p) => new(
        p.Id, p.Codigo.Valor, p.Descricao, p.Categoria, p.Unidade.ToString(), p.PrecoCusto.Valor, p.PrecoVenda.Valor,
        p.MargemPercentual, p.Perecivel, p.PrazoValidadeDias, p.CriadoEm, p.AtualizadoEm);
}
