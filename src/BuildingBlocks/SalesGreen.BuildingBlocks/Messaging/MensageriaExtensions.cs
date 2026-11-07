using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalesGreen.SharedKernel;

namespace SalesGreen.BuildingBlocks.Messaging;

public static class MensageriaExtensions
{
    /// <summary>
    /// Configura MassTransit + RabbitMQ com:
    /// - Outbox transacional (EF Core/PostgreSQL): eventos só saem se o commit do banco acontecer;
    /// - Inbox (idempotência): a mesma mensagem (MessageId) não é processada duas vezes pelo consumidor;
    /// - Retry com intervalos crescentes; esgotadas as tentativas, a mensagem vai para a fila "_error" (DLQ).
    /// </summary>
    public static IServiceCollection AddMensageria<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator> registrar)
        where TDbContext : DbContext
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            registrar(x);

            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30);
            });

            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(r =>
                {
                    r.Intervals(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
                    r.Ignore<DomainException>(); // erro de negócio não melhora com retry: vai direto para a DLQ
                });
                endpoint.UseEntityFrameworkOutbox<TDbContext>(context);
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.ConfigurarHost(configuration);
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>Lê ConnectionStrings:RabbitMq no formato amqp://usuario:senha@host:porta/</summary>
    public static void ConfigurarHost(this IRabbitMqBusFactoryConfigurator cfg, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException("ConnectionStrings:RabbitMq não configurada.");
        cfg.Host(new Uri(connectionString));
    }
}
