using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Pedidos.Domain;

public enum StatusPedido
{
    Pendente = 1,
    Confirmado = 2,
    Cancelado = 3
}

/// <summary>
/// Agregado Pedido. O status só muda por métodos com regra (máquina de estados do agregado);
/// quem decide QUANDO mudar é a Saga, que reage aos eventos dos outros serviços.
/// </summary>
public sealed class Pedido : AggregateRoot
{
    public const int MaximoItens = 100;

    private readonly List<ItemPedido> _itens = [];

    public string Cliente { get; private set; } = string.Empty;
    public StatusPedido Status { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public Guid? NotaFiscalId { get; private set; }
    public long? NumeroNotaFiscal { get; private set; }
    public IReadOnlyCollection<ItemPedido> Itens => _itens.AsReadOnly();

    public Dinheiro ValorTotal => _itens.Aggregate(Dinheiro.Zero, (total, i) => total + i.Subtotal);

    private Pedido() { } // EF Core

    public static Pedido Criar(string cliente, IReadOnlyCollection<ItemPedido> itens, DateTime agora)
    {
        var nome = (cliente ?? string.Empty).Trim();
        if (nome.Length is < 2 or > 120)
            throw new DomainException("Cliente deve ter entre 2 e 120 caracteres.");
        if (itens.Count == 0)
            throw new DomainException("Pedido deve ter ao menos um item.");
        if (itens.Count > MaximoItens)
            throw new DomainException($"Pedido aceita no máximo {MaximoItens} itens.");
        if (itens.GroupBy(i => i.ProdutoCodigo).Any(g => g.Count() > 1))
            throw new DomainException("Produto repetido no pedido: informe a quantidade total em um único item.");

        var pedido = new Pedido { Cliente = nome, Status = StatusPedido.Pendente, CriadoEm = agora, AtualizadoEm = agora };
        pedido._itens.AddRange(itens);
        pedido.Raise(new PedidoCriadoDomainEvent(pedido));
        return pedido;
    }

    public void Confirmar(Guid notaFiscalId, long numeroNotaFiscal, DateTime agora)
    {
        if (Status == StatusPedido.Confirmado && NotaFiscalId == notaFiscalId)
            return; // idempotente: mensagem repetida não muda nada
        if (Status != StatusPedido.Pendente)
            throw new DomainException($"Pedido {Status} não pode ser confirmado.");

        Status = StatusPedido.Confirmado;
        NotaFiscalId = notaFiscalId;
        NumeroNotaFiscal = numeroNotaFiscal;
        AtualizadoEm = agora;
        Raise(new PedidoConfirmadoDomainEvent(this));
    }

    public void Cancelar(string motivo, DateTime agora)
    {
        if (Status == StatusPedido.Cancelado)
            return; // idempotente
        if (Status == StatusPedido.Confirmado)
            throw new DomainException("Pedido confirmado não pode ser cancelado por este fluxo.");

        Status = StatusPedido.Cancelado;
        MotivoCancelamento = motivo.Length > 300 ? motivo[..300] : motivo;
        AtualizadoEm = agora;
    }
}

public sealed class ItemPedido : Entity
{
    public string ProdutoCodigo { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
    public Quantidade Quantidade { get; private set; }
    public Dinheiro PrecoUnitario { get; private set; }
    public Dinheiro Subtotal => PrecoUnitario * Quantidade.Valor;

    private ItemPedido() { } // EF Core

    public ItemPedido(CodigoProduto produtoCodigo, string descricao, Quantidade quantidade, Dinheiro precoUnitario)
    {
        if (quantidade <= Quantidade.Zero)
            throw new DomainException("Quantidade do item deve ser maior que zero.");
        if (precoUnitario <= Dinheiro.Zero)
            throw new DomainException("Preço unitário deve ser maior que zero.");
        ProdutoCodigo = produtoCodigo.Valor;
        Descricao = descricao;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }
}

public sealed record PedidoCriadoDomainEvent(Pedido Pedido) : IDomainEvent;
public sealed record PedidoConfirmadoDomainEvent(Pedido Pedido) : IDomainEvent;

public interface IPedidoRepository
{
    Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Pedido>> ListarAsync(StatusPedido? status, int pagina, int tamanho, CancellationToken ct);
    void Adicionar(Pedido pedido);
}
