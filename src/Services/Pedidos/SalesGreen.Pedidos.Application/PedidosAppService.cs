using System.ComponentModel.DataAnnotations;
using SalesGreen.Pedidos.Domain;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Pedidos.Application;

public sealed record ItemPedidoRequest([Required, StringLength(30)] string ProdutoCodigo, [Range(0.001, 100_000)] decimal Quantidade);

/// <summary>Repare: o cliente NÃO envia preço. O preço vem do Catálogo (evita manipulação de valores pelo cliente).</summary>
public sealed record CriarPedidoRequest([Required, StringLength(120)] string Cliente, [Required, MinLength(1)] List<ItemPedidoRequest> Itens);

public sealed record ItemPedidoResponse(string ProdutoCodigo, string Descricao, decimal Quantidade, decimal PrecoUnitario, decimal Subtotal);

public sealed record PedidoResponse(
    Guid Id, string Cliente, string Status, decimal ValorTotal, DateTime CriadoEm, DateTime AtualizadoEm,
    string? MotivoCancelamento, long? NumeroNotaFiscal, IReadOnlyList<ItemPedidoResponse> Itens)
{
    public static PedidoResponse De(Pedido p) => new(
        p.Id, p.Cliente, p.Status.ToString(), p.ValorTotal.Valor, p.CriadoEm, p.AtualizadoEm, p.MotivoCancelamento, p.NumeroNotaFiscal,
        p.Itens.Select(i => new ItemPedidoResponse(i.ProdutoCodigo, i.Descricao, i.Quantidade.Valor, i.PrecoUnitario.Valor, i.Subtotal.Valor)).ToList());
}

/// <summary>Porta para o Catálogo (implementada com HttpClient + Polly na Infrastructure).</summary>
public interface ICatalogoClient
{
    Task<ProdutoCatalogo?> ObterProdutoAsync(string codigo, CancellationToken ct);
}

public sealed record ProdutoCatalogo(string Codigo, string Descricao, decimal PrecoVenda);

public sealed class PedidosAppService(IPedidoRepository pedidos, ICatalogoClient catalogo, IUnitOfWork unitOfWork, TimeProvider relogio)
{
    public async Task<PedidoResponse> CriarAsync(CriarPedidoRequest request, CancellationToken ct)
    {
        if (request.Itens.Count > Pedido.MaximoItens)
            throw new DomainException($"Pedido aceita no máximo {Pedido.MaximoItens} itens.");

        var itens = new List<ItemPedido>();
        foreach (var item in request.Itens)
        {
            var codigo = CodigoProduto.De(item.ProdutoCodigo);
            var produto = await catalogo.ObterProdutoAsync(codigo.Valor, ct)
                          ?? throw new DomainException($"Produto {codigo} não existe no catálogo.");
            itens.Add(new ItemPedido(codigo, produto.Descricao, Quantidade.Positiva(item.Quantidade), Dinheiro.De(produto.PrecoVenda)));
        }

        var pedido = Pedido.Criar(request.Cliente, itens, Agora);
        pedidos.Adicionar(pedido);
        await unitOfWork.SalvarAsync(ct); // grava pedido + PedidoCriado (outbox) na mesma transação
        return PedidoResponse.De(pedido);
    }

    public async Task<PedidoResponse> ObterAsync(Guid id, CancellationToken ct) =>
        PedidoResponse.De(await pedidos.ObterPorIdAsync(id, ct) ?? throw new NotFoundException($"Pedido {id} não encontrado."));

    public async Task<IReadOnlyList<PedidoResponse>> ListarAsync(StatusPedido? status, int pagina, int tamanho, CancellationToken ct) =>
        (await pedidos.ListarAsync(status, Math.Max(1, pagina), Math.Clamp(tamanho, 1, 100), ct)).Select(PedidoResponse.De).ToList();

    public async Task ConfirmarAsync(Guid pedidoId, Guid notaFiscalId, long numeroNota, CancellationToken ct)
    {
        var pedido = await pedidos.ObterPorIdAsync(pedidoId, ct) ?? throw new NotFoundException($"Pedido {pedidoId} não encontrado.");
        pedido.Confirmar(notaFiscalId, numeroNota, Agora);
        await unitOfWork.SalvarAsync(ct);
    }

    public async Task CancelarAsync(Guid pedidoId, string motivo, CancellationToken ct)
    {
        var pedido = await pedidos.ObterPorIdAsync(pedidoId, ct) ?? throw new NotFoundException($"Pedido {pedidoId} não encontrado.");
        pedido.Cancelar(motivo, Agora);
        await unitOfWork.SalvarAsync(ct);
    }

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;
}
