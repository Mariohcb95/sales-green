using System.Net;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SalesGreen.Pedidos.Application;
using SalesGreen.SharedKernel;

namespace SalesGreen.Pedidos.Infrastructure.Http;

/// <summary>
/// Chamada síncrona ao Catálogo para obter descrição e preço vigente.
/// A resiliência (retry, circuit breaker, timeout) é configurada com Polly no registro do HttpClient.
/// </summary>
public sealed class CatalogoHttpClient(HttpClient http) : ICatalogoClient
{
    public async Task<ProdutoCatalogo?> ObterProdutoAsync(string codigo, CancellationToken ct)
    {
        try
        {
            using var resposta = await http.GetAsync($"api/produtos/codigo/{Uri.EscapeDataString(codigo)}", ct);
            if (resposta.StatusCode == HttpStatusCode.NotFound)
                return null;
            resposta.EnsureSuccessStatusCode();
            return await resposta.Content.ReadFromJsonAsync<ProdutoCatalogo>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TimeoutRejectedException)
        {
            throw new ServicoIndisponivelException("Catálogo indisponível no momento. Tente novamente em instantes.", ex);
        }
    }
}
