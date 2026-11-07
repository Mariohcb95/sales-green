using Microsoft.EntityFrameworkCore;
using SalesGreen.Pedidos.Domain;

namespace SalesGreen.Pedidos.Infrastructure.Persistence;

public sealed class PedidoRepository(PedidosDbContext db) : IPedidoRepository
{
    public Task<Pedido?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Pedidos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Pedido>> ListarAsync(StatusPedido? status, int pagina, int tamanho, CancellationToken ct)
    {
        var query = db.Pedidos.AsNoTracking();
        if (status is not null)
            query = query.Where(p => p.Status == status);
        return await query.OrderByDescending(p => p.CriadoEm).Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
    }

    public void Adicionar(Pedido pedido) => db.Pedidos.Add(pedido);
}
