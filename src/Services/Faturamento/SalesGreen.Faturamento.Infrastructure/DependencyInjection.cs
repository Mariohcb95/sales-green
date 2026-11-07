using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalesGreen.BuildingBlocks.Messaging;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Faturamento.Application;
using SalesGreen.Faturamento.Domain;
using SalesGreen.Faturamento.Infrastructure.Persistence;
using SalesGreen.SharedKernel;

namespace SalesGreen.Faturamento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFaturamentoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Faturamento")
            ?? throw new InvalidOperationException("ConnectionStrings:Faturamento não configurada.");

        var options = configuration.GetSection("Faturamento").Get<FaturamentoOptions>() ?? new FaturamentoOptions();
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<FaturamentoDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FaturamentoDbContext>());
        services.AddScoped<INotaFiscalRepository, NotaFiscalRepository>();
        services.AddScoped<FaturamentoAppService>();
        services.AddDomainEvents(typeof(FaturamentoAppService).Assembly);

        services.AddMensageria<FaturamentoDbContext>(configuration, x => x.AddConsumer<GerarNotaFiscalConsumer>());
        return services;
    }
}
