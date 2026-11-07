using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalesGreen.BuildingBlocks.Messaging;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Estoque.Application;
using SalesGreen.Estoque.Domain;
using SalesGreen.Estoque.Infrastructure.Persistence;
using SalesGreen.SharedKernel;

namespace SalesGreen.Estoque.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEstoqueInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Estoque")
            ?? throw new InvalidOperationException("ConnectionStrings:Estoque não configurada.");

        services.AddDbContext<EstoqueDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EstoqueDbContext>());
        services.AddScoped<IEstoqueRepository, EstoqueRepository>();
        services.AddScoped<EstoqueAppService>();
        services.AddSingleton(TimeProvider.System);
        services.AddDomainEvents(typeof(SaldoAlteradoHandler).Assembly);

        services.AddMensageria<EstoqueDbContext>(configuration, x =>
        {
            x.AddConsumer<ProdutoCadastradoConsumer>();
            x.AddConsumer<ReservarEstoqueConsumer>();
            x.AddConsumer<LiberarReservaEstoqueConsumer>();
        });

        return services;
    }
}
