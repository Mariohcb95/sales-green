using Microsoft.Extensions.DependencyInjection;

namespace SalesGreen.Pedidos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPedidosApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PedidosAppService>();
        return services;
    }
}
