namespace SalesGreen.SharedKernel.ValueObjects;

/// <summary>
/// Quantidade positiva com até 3 casas decimais (suporta itens pesáveis em KG, comuns em hortifrúti).
/// </summary>
public readonly record struct Quantidade : IComparable<Quantidade>
{
    public decimal Valor { get; }

    private Quantidade(decimal valor) => Valor = valor;

    public static Quantidade Zero => new(0m);

    public static Quantidade De(decimal valor)
    {
        if (valor < 0)
            throw new DomainException("Quantidade não pode ser negativa.");
        if (decimal.Round(valor, 3) != valor)
            throw new DomainException("Quantidade aceita no máximo 3 casas decimais.");
        return new Quantidade(valor);
    }

    public static Quantidade Positiva(decimal valor)
    {
        if (valor <= 0)
            throw new DomainException("Quantidade deve ser maior que zero.");
        return De(valor);
    }

    public static Quantidade operator +(Quantidade a, Quantidade b) => new(a.Valor + b.Valor);
    public static Quantidade operator -(Quantidade a, Quantidade b) => De(a.Valor - b.Valor);
    public static bool operator >(Quantidade a, Quantidade b) => a.Valor > b.Valor;
    public static bool operator <(Quantidade a, Quantidade b) => a.Valor < b.Valor;
    public static bool operator >=(Quantidade a, Quantidade b) => a.Valor >= b.Valor;
    public static bool operator <=(Quantidade a, Quantidade b) => a.Valor <= b.Valor;
    public static Quantidade Min(Quantidade a, Quantidade b) => a <= b ? a : b;

    public int CompareTo(Quantidade other) => Valor.CompareTo(other.Valor);

    public override string ToString() => Valor.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
