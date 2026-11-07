namespace SalesGreen.SharedKernel.ValueObjects;

/// <summary>
/// Value object monetário (BRL). Imutável, igualdade por valor e arredondamento centralizado
/// (evita espalhar Math.Round pelo código e erros de centavos).
/// </summary>
public readonly record struct Dinheiro : IComparable<Dinheiro>
{
    public decimal Valor { get; }

    private Dinheiro(decimal valor) => Valor = valor;

    public static Dinheiro Zero => new(0m);

    public static Dinheiro De(decimal valor)
    {
        if (valor < 0)
            throw new DomainException("Valor monetário não pode ser negativo.");
        return new Dinheiro(Math.Round(valor, 2, MidpointRounding.AwayFromZero));
    }

    public static Dinheiro operator +(Dinheiro a, Dinheiro b) => new(a.Valor + b.Valor);
    public static Dinheiro operator -(Dinheiro a, Dinheiro b) => De(a.Valor - b.Valor);
    public static Dinheiro operator *(Dinheiro a, decimal fator) => De(a.Valor * fator);
    public static bool operator >(Dinheiro a, Dinheiro b) => a.Valor > b.Valor;
    public static bool operator <(Dinheiro a, Dinheiro b) => a.Valor < b.Valor;
    public static bool operator >=(Dinheiro a, Dinheiro b) => a.Valor >= b.Valor;
    public static bool operator <=(Dinheiro a, Dinheiro b) => a.Valor <= b.Valor;

    public int CompareTo(Dinheiro other) => Valor.CompareTo(other.Valor);

    public override string ToString() => $"R$ {Valor:N2}";
}
