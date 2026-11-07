using MassTransit;
using SalesGreen.Analises.Api;
using SalesGreen.Analises.Api.Cassandra;
using SalesGreen.BuildingBlocks.Messaging;
using SalesGreen.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
       .AddCheck<CassandraHealthCheck>("cassandra", tags: [ServiceDefaults.TagReady]);

builder.Services.AddSingleton(builder.Configuration.GetSection("Cassandra").Get<CassandraOptions>() ?? new CassandraOptions());
builder.Services.AddSingleton<CassandraContexto>();
builder.Services.AddSingleton<AnalisesRepository>();
builder.Services.AddSingleton(TimeProvider.System);

// Sem banco relacional aqui, então sem Outbox/Inbox: a idempotência vem do modelo (upsert por chave).
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<VendaRealizadaConsumer>();
    x.AddConsumer<SaldoEstoqueAlteradoConsumer>();
    x.AddConfigureEndpointsCallback((_, _, endpoint) =>
        endpoint.UseMessageRetry(r => r.Intervals(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5))));
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigurarHost(builder.Configuration);
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

app.UseServiceDefaults();
await app.Services.GetRequiredService<CassandraContexto>().InicializarAsync(app.Lifetime.ApplicationStopping);
await app.RunAsync();

public partial class Program;
