using Microsoft.AspNetCore.Mvc;
using SalesGreen.Analises.Api.Cassandra;
using SalesGreen.SharedKernel;

namespace SalesGreen.Analises.Api;

public sealed record RiscoRuptura(string ProdutoCodigo, decimal SaldoAtual, decimal MediaVendaDiaria, decimal? DiasDeCobertura, string Nivel);

[ApiController]
[Route("api/analises")]
[Produces("application/json")]
public sealed class AnalisesController(AnalisesRepository repositorio, TimeProvider relogio) : ControllerBase
{
    private const int JanelaMaximaDias = 366;

    /// <summary>Vendas de um produto agrupadas por dia (padrão: últimos 30 dias).</summary>
    [HttpGet("vendas/{produtoCodigo}")]
    public async Task<IReadOnlyList<VendaDia>> VendasPorDia(string produtoCodigo, [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate)
    {
        var fim = ate ?? Hoje;
        var inicio = de ?? fim.AddDays(-29);
        if (inicio > fim || fim.DayNumber - inicio.DayNumber > JanelaMaximaDias)
            throw new DomainException($"Período inválido (máximo de {JanelaMaximaDias} dias).");
        return await repositorio.VendasPorDiaAsync(produtoCodigo.Trim().ToUpperInvariant(), inicio, fim);
    }

    /// <summary>
    /// Produtos com risco de ruptura: dias de cobertura = saldo atual / média de venda diária na janela.
    /// Nível ALTO se a cobertura for menor que o lead time de reposição informado.
    /// </summary>
    [HttpGet("ruptura")]
    public async Task<IReadOnlyList<RiscoRuptura>> RiscoDeRuptura([FromQuery] int janelaDias = 14, [FromQuery] int leadTimeDias = 3)
    {
        janelaDias = Math.Clamp(janelaDias, 1, 90);
        leadTimeDias = Math.Clamp(leadTimeDias, 1, 60);
        var fim = Hoje;
        var inicio = fim.AddDays(-(janelaDias - 1));

        var resultado = new List<RiscoRuptura>();
        foreach (var saldo in await repositorio.SaldosAsync(limite: 5000))
        {
            var vendas = await repositorio.VendasPorDiaAsync(saldo.ProdutoCodigo, inicio, fim);
            var media = Math.Round(vendas.Sum(v => v.Quantidade) / janelaDias, 3);
            decimal? cobertura = media == 0 ? null : Math.Round(saldo.Saldo / media, 1);

            var nivel = saldo.Saldo <= 0 ? "RUPTURA"
                : cobertura is null ? "SEM_GIRO"
                : cobertura < leadTimeDias ? "ALTO"
                : cobertura < leadTimeDias * 2 ? "MEDIO"
                : "BAIXO";

            if (nivel is "RUPTURA" or "ALTO" or "MEDIO")
                resultado.Add(new RiscoRuptura(saldo.ProdutoCodigo, saldo.Saldo, media, cobertura, nivel));
        }

        return resultado.OrderBy(r => r.DiasDeCobertura ?? 0).ToList();
    }

    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetUtcNow().UtcDateTime);
}
