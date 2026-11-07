using Microsoft.Extensions.DependencyInjection;
using SalesGreen.Catalogo.Application.Importacoes;
using SalesGreen.Catalogo.Application.Produtos;

namespace SalesGreen.Catalogo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogoApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProdutosAppService>();
        services.AddScoped<ImportacoesAppService>();
        return services;
    }
}
