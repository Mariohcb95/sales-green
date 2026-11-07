namespace SalesGreen.SharedKernel;

/// <summary>Entidade: identidade estável ao longo do tempo (igualdade por Id, não por atributos).</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
