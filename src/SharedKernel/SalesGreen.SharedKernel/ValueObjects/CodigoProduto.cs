using System.Text.RegularExpressions;

namespace SalesGreen.SharedKernel.ValueObjects;

/// <summary>
/// Código do produto (SKU/EAN interno). É a chave de negócio compartilhada entre os bounded contexts
/// e a chave de idempotência da importação de planilhas.
/// </summary>
public readonly partial record struct CodigoProduto
{
    public const int TamanhoMaximo = 30;

    public string Valor { get; }

    private CodigoProduto(string valor) => Valor = valor;

    public static CodigoProduto De(string? valor)
    {
        var normalizado = (valor ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizado.Length == 0)
            throw new DomainException("Código do produto é obrigatório.");
        if (normalizado.Length > TamanhoMaximo)
            throw new DomainException($"Código do produto deve ter no máximo {TamanhoMaximo} caracteres.");
        if (!Formato().IsMatch(normalizado))
            throw new DomainException("Código do produto aceita apenas letras, números, '-', '_' e '.'.");
        return new CodigoProduto(normalizado);
    }

    public override string ToString() => Valor;

    [GeneratedRegex("^[A-Z0-9._-]+$")]
    private static partial Regex Formato();
}
