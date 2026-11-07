using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SalesGreen.BuildingBlocks.Messaging;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Pedidos.Application;
using SalesGreen.Pedidos.Application.Saga;
using SalesGreen.Pedidos.Domain;
using SalesGreen.Pedidos.Infrastructure.Http;
using SalesGreen.Pedidos.Infrastructure.Persistence;
using SalesGreen.SharedKernel;

namespace SalesGreen.Pedidos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPedidosInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Pedidos")
            ?? throw new InvalidOperationException("ConnectionStrings:Pedidos não configurada.");

        services.AddDbContext<PedidosDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PedidosDbContext>());
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddDomainEvents(typeof(PedidoCriadoHandler).Assembly);

        services.AddHttpClient<ICatalogoClient, CatalogoHttpClient>(c =>
                c.BaseAddress = new Uri(configuration["Servicos:CatalogoUrl"]
                                        ?? throw new InvalidOperationException("Servicos:CatalogoUrl não configurada.")))
            // Polly (v8, via Microsoft.Extensions.Http.Resilience). Ordem: de fora para dentro.
            .AddResilienceHandler("catalogo", pipeline =>
            {
                pipeline.AddTimeout(TimeSpan.FromSeconds(10)); // tempo total, incluindo as tentativas
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true, // evita que várias instâncias tentem de novo no mesmo instante
                    Delay = TimeSpan.FromMilliseconds(200)
                });
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15) // falha rápido em vez de empilhar requisições
                });
                pipeline.AddTimeout(TimeSpan.FromSeconds(2)); // por tentativa
            });

        services.AddMensageria<PedidosDbContext>(configuration, x =>
        {
            x.AddConsumer<PedidoConfirmadoConsumer>();
            x.AddConsumer<PedidoCanceladoConsumer>();
            x.AddSagaStateMachine<PedidoStateMachine, PedidoSagaState>()
                .EntityFrameworkRepository(r =>
                {
                    r.ExistingDbContext<PedidosDbContext>();
                    r.UsePostgres();
                    r.ConcurrencyMode = ConcurrencyMode.Pessimistic; // SELECT ... FOR UPDATE na linha da saga
                });
        });

        return services;
    }
}
