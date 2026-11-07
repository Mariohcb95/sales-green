using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.Extensions.Logging;
using SalesGreen.Contracts.Estoque;
using SalesGreen.Contracts.Pedidos;
using SalesGreen.Estoque.Domain;
using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Estoque.Application;

public sealed record EntradaEstoqueRequest(
    [Required, StringLength(30)] string ProdutoCodigo,
    [Range(0.001, 1_000_000)] decimal Quantidade,
    [StringLength(40)] string? Lote,
    DateOnly? Validade);

public sealed record LoteResponse(Guid Id, string Codigo, DateOnly? Validade, decimal Disponivel, bool Vencido);

public sealed record SaldoResponse(string ProdutoCodigo, string Descricao, bool Perecivel, decimal SaldoDisponivel, IReadOnlyList<LoteResponse> Lotes);

public sealed class EstoqueAppService(
    IEstoqueRepository estoque,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publish,
    TimeProvider relogio,
    ILogger<EstoqueAppService> logger)
{
    /// <summary>Cria o item de estoque (saldo zero) quando o Catálogo cadastra um produto. Idempotente.</summary>
    public async Task RegistrarProdutoAsync(string codigo, string descricao, bool perecivel, CancellationToken ct)
    {
        if (await estoque.ObterAsync(codigo, ct) is not null)
            return;
        estoque.Adicionar(ItemEstoque.Registrar(CodigoProduto.De(codigo), descricao, perecivel, Agora));
        await unitOfWork.SalvarAsync(ct);
    }

    public async Task<SaldoResponse> DarEntradaAsync(EntradaEstoqueRequest request, CancellationToken ct)
    {
        var codigo = CodigoProduto.De(request.ProdutoCodigo);
        var item = await estoque.ObterAsync(codigo.Valor, ct)
                   ?? throw new NotFoundException($"Produto {codigo} não está registrado no estoque (cadastre-o no Catálogo).");
        item.DarEntrada(Quantidade.Positiva(request.Quantidade), request.Lote, request.Validade, Agora);
        await unitOfWork.SalvarAsync(ct);
        return Mapear(item);
    }

    public async Task<SaldoResponse> ObterSaldoAsync(string codigo, CancellationToken ct) =>
        Mapear(await estoque.ObterAsync(CodigoProduto.De(codigo).Valor, ct)
               ?? throw new NotFoundException($"Produto {codigo} não encontrado no estoque."));

    /// <summary>
    /// Reserva tudo ou nada. Responde à Saga com EstoqueReservado ou EstoqueInsuficiente.
    /// Concorrência entre pedidos simultâneos é tratada por concorrência otimista (xmin) + retry do consumidor.
    /// </summary>
    public async Task ReservarAsync(Guid pedidoId, IReadOnlyList<ItemPedidoMensagem> itens, CancellationToken ct)
    {
        if (await estoque.ObterReservaAsync(pedidoId, ct) is { } existente)
        {
            logger.LogInformation("Pedido {PedidoId} já possui reserva {Status}: reenviando resposta", pedidoId, existente.Status);
            if (existente.Status == StatusReserva.Ativa)
                await publish.Publish(new EstoqueReservado(pedidoId), ct);
            return;
        }

        var hoje = DateOnly.FromDateTime(Agora);
        var codigos = itens.Select(i => i.ProdutoCodigo).Distinct().ToList();
        var produtos = await estoque.ObterVariosAsync(codigos, ct);

        var faltas = itens
            .Where(i => !produtos.TryGetValue(i.ProdutoCodigo, out var p) || !p.PodeReservar(Quantidade.De(i.Quantidade), hoje))
            .Select(i => produtos.TryGetValue(i.ProdutoCodigo, out var p)
                ? $"{i.ProdutoCodigo} (disponível {p.SaldoDisponivel(hoje)}, solicitado {i.Quantidade})"
                : $"{i.ProdutoCodigo} (sem estoque cadastrado)")
            .ToList();

        if (faltas.Count > 0)
        {
            var motivo = "Estoque insuficiente: " + string.Join("; ", faltas);
            logger.LogWarning("Pedido {PedidoId}: {Motivo}", pedidoId, motivo);
            await publish.Publish(new EstoqueInsuficiente(pedidoId, motivo), ct);
            await unitOfWork.SalvarAsync(ct); // grava o outbox
            return;
        }

        var itensReserva = new List<ItemReserva>();
        foreach (var item in itens)
        {
            var produto = produtos[item.ProdutoCodigo];
            foreach (var (loteId, quantidade) in produto.Reservar(Quantidade.De(item.Quantidade), Agora))
                itensReserva.Add(new ItemReserva(item.ProdutoCodigo, loteId, quantidade));
        }

        estoque.AdicionarReserva(Reserva.Criar(pedidoId, itensReserva, Agora));
        await publish.Publish(new EstoqueReservado(pedidoId), ct);
        await unitOfWork.SalvarAsync(ct);
        logger.LogInformation("Pedido {PedidoId}: estoque reservado ({Itens} itens)", pedidoId, itens.Count);
    }

    /// <summary>Compensação da Saga: devolve as quantidades aos lotes de origem. Idempotente.</summary>
    public async Task LiberarAsync(Guid pedidoId, string motivo, CancellationToken ct)
    {
        var reserva = await estoque.ObterReservaAsync(pedidoId, ct);
        if (reserva is null || !reserva.Liberar(motivo, Agora))
            return;

        var produtos = await estoque.ObterVariosAsync(reserva.Itens.Select(i => i.ProdutoCodigo).Distinct().ToList(), ct);
        foreach (var item in reserva.Itens)
            produtos[item.ProdutoCodigo].Devolver(item.LoteId, item.Quantidade, Agora);

        await unitOfWork.SalvarAsync(ct);
        logger.LogInformation("Pedido {PedidoId}: reserva liberada ({Motivo})", pedidoId, motivo);
    }

    private SaldoResponse Mapear(ItemEstoque item)
    {
        var hoje = DateOnly.FromDateTime(Agora);
        return new SaldoResponse(item.ProdutoCodigo, item.Descricao, item.Perecivel, item.SaldoDisponivel(hoje).Valor,
            item.Lotes.OrderBy(l => l.Validade).Select(l => new LoteResponse(l.Id, l.Codigo, l.Validade, l.Disponivel.Valor, l.Vencido(hoje))).ToList());
    }

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;
}
