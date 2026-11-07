using System.Globalization;
using SalesGreen.Catalogo.Application.Produtos;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Catalogo.Application.Importacoes;

/// <summary>Linha da planilha já separada em colunas (chave = nome da coluna normalizado).</summary>
public sealed record LinhaPlanilha(int Numero, IReadOnlyDictionary<string, string?> Campos)
{
    public string? Campo(string nome) => Campos.TryGetValue(nome, out var v) ? v?.Trim() : null;
}

/// <summary>Estratégia de leitura por formato (Strategy + Open/Closed: um novo formato é uma nova classe).</summary>
public interface IPlanilhaLeitor
{
    FormatoPlanilha Formato { get; }
    IEnumerable<LinhaPlanilha> Ler(byte[] conteudo);
}

/// <summary>Layout esperado da planilha de produtos.</summary>
public static class LayoutPlanilhaProdutos
{
    public const string Codigo = "codigo";
    public const string Descricao = "descricao";
    public const string Categoria = "categoria";
    public const string Unidade = "unidade";
    public const string PrecoCusto = "preco_custo";
    public const string PrecoVenda = "preco_venda";
    public const string Perecivel = "perecivel";
    public const string ValidadeDias = "validade_dias";

    public static readonly string[] ColunasObrigatorias = [Codigo, Descricao, Categoria, Unidade, PrecoCusto, PrecoVenda, Perecivel];

    public static string NormalizarCabecalho(string? nome) =>
        (nome ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_')
            .Replace("ç", "c").Replace("ã", "a").Replace("é", "e").Replace("í", "i");

    public static void ValidarCabecalho(IEnumerable<string> colunas)
    {
        var faltando = ColunasObrigatorias.Except(colunas).ToList();
        if (faltando.Count > 0)
            throw new DomainException($"Cabeçalho inválido. Colunas ausentes: {string.Join(", ", faltando)}.");
    }
}

/// <summary>Converte uma linha da planilha nos tipos do domínio. Erros viram DomainException com mensagem amigável.</summary>
public static class ConversorLinhaProduto
{
    // Formato brasileiro montado à mão: o projeto usa InvariantGlobalization (imagem Docker menor, sem ICU).
    private static readonly NumberFormatInfo FormatoBrasileiro = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static (CodigoProduto Codigo, DadosProduto Dados) Converter(LinhaPlanilha linha)
    {
        var codigo = CodigoProduto.De(linha.Campo(LayoutPlanilhaProdutos.Codigo));
        var perecivel = ConverterBooleano(linha.Campo(LayoutPlanilhaProdutos.Perecivel));
        var validade = linha.Campo(LayoutPlanilhaProdutos.ValidadeDias);

        var dados = new DadosProduto(
            linha.Campo(LayoutPlanilhaProdutos.Descricao) ?? string.Empty,
            linha.Campo(LayoutPlanilhaProdutos.Categoria) ?? string.Empty,
            ProdutosAppService.ConverterUnidade(linha.Campo(LayoutPlanilhaProdutos.Unidade)),
            Dinheiro.De(ConverterDecimal(linha.Campo(LayoutPlanilhaProdutos.PrecoCusto), "preço de custo")),
            Dinheiro.De(ConverterDecimal(linha.Campo(LayoutPlanilhaProdutos.PrecoVenda), "preço de venda")),
            perecivel,
            string.IsNullOrWhiteSpace(validade) ? null
                : int.TryParse(validade, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dias) ? dias
                : throw new DomainException($"Validade em dias '{validade}' inválida."));

        return (codigo, dados);
    }

    /// <summary>Aceita "1.234,56" (pt-BR) e "1234.56" (invariante).</summary>
    public static decimal ConverterDecimal(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException($"Campo {campo} é obrigatório.");
        var texto = valor.Replace("R$", string.Empty).Trim();
        IFormatProvider formato = texto.Contains(',') ? FormatoBrasileiro : CultureInfo.InvariantCulture;
        return decimal.TryParse(texto, NumberStyles.Number, formato, out var numero)
            ? numero
            : throw new DomainException($"Valor '{valor}' inválido para {campo}.");
    }

    public static bool ConverterBooleano(string? valor) => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "sim" or "s" or "true" or "1" or "x" => true,
        "nao" or "não" or "n" or "false" or "0" or "" => false,
        _ => throw new DomainException($"Valor '{valor}' inválido para perecível (use sim/não).")
    };
}
