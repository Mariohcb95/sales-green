using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Estoque.Domain;

public enum StatusReserva
{
    Ativa = 1,
    Liberada = 2
}

/// <summary>
/// Registro do que foi reservado para um pedido (produto + lote + quantidade).
/// É o que torna a compensação possível: sabemos exatamente o que devolver e para qual lote.
/// PedidoId é único: reservar o mesmo pedido duas vezes é impossível (idempotência).
/// </summary>
public sealed class Reserva : AggregateRoot
{
    private readonly List<ItemReserva> _itens = [];

    public Guid PedidoId { get; private set; }
    public StatusReserva Status { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public DateTime? LiberadaEm { get; private set; }
    public string? MotivoLiberacao { get; private set; }
    public IReadOnlyCollection<ItemReserva> Itens => _itens.AsReadOnly();

    private Reserva() { } // EF Core

    public static Reserva Criar(Guid pedidoId, IEnumerable<ItemReserva> itens, DateTime agora)
    {
        var reserva = new Reserva { PedidoId = pedidoId, Status = StatusReserva.Ativa, CriadaEm = agora };
        reserva._itens.AddRange(itens);
        if (reserva._itens.Count == 0)
            throw new DomainException("Reserva sem itens.");
        return reserva;
    }

    public bool Liberar(string motivo, DateTime agora)
    {
        if (Status == StatusReserva.Liberada)
            return false; // idempotente
        Status = StatusReserva.Liberada;
        MotivoLiberacao = motivo.Length > 300 ? motivo[..300] : motivo;
        LiberadaEm = agora;
        return true;
    }
}

public sealed class ItemReserva : Entity
{
    public string ProdutoCodigo { get; private set; } = string.Empty;
    public Guid LoteId { get; private set; }
    public Quantidade Quantidade { get; private set; }

    private ItemReserva() { } // EF Core

    public ItemReserva(string produtoCodigo, Guid loteId, Quantidade quantidade)
    {
        ProdutoCodigo = produtoCodigo;
        LoteId = loteId;
        Quantidade = quantidade;
    }
}

public interface IEstoqueRepository
{
    Task<ItemEstoque?> ObterAsync(string produtoCodigo, CancellationToken ct);
    Task<IReadOnlyDictionary<string, ItemEstoque>> ObterVariosAsync(IReadOnlyCollection<string> produtoCodigos, CancellationToken ct);
    void Adicionar(ItemEstoque item);
    Task<Reserva?> ObterReservaAsync(Guid pedidoId, CancellationToken ct);
    void AdicionarReserva(Reserva reserva);
}
