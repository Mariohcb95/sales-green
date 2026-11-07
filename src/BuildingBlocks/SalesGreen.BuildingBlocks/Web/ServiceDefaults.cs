using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

namespace SalesGreen.BuildingBlocks.Web;

public static class ServiceDefaults
{
    public const string TagReady = "ready";

    /// <summary>Logs estruturados, ProblemDetails, health checks, controllers e Swagger — padrão para todos os serviços.</summary>
    public static IHealthChecksBuilder AddServiceDefaults(this WebApplicationBuilder builder)
    {
        var servico = builder.Environment.ApplicationName;

        builder.Services.AddSerilog((services, log) =>
        {
            log.ReadFrom.Configuration(builder.Configuration)
               .ReadFrom.Services(services)
               .Enrich.FromLogContext()
               .Enrich.WithProperty("Servico", servico);

            // Em produção: JSON (fácil de indexar no Seq/Elastic/Loki). Em dev: texto legível.
            if (builder.Environment.IsDevelopment())
                log.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}");
            else
                log.WriteTo.Console(new RenderedCompactJsonFormatter());
        });

        builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["servico"] = servico);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = servico, Version = "v1" }));

        return builder.Services.AddHealthChecks();
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Habilitado"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Liveness: o processo responde? (sem dependências — evita reiniciar o container quando o banco cai)
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        // Readiness: as dependências (PostgreSQL, RabbitMQ, Cassandra) estão acessíveis?
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains(TagReady),
            ResponseWriter = EscreverJson
        });

        app.MapControllers();
        return app;
    }

    private static Task EscreverJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { nome = e.Key, status = e.Value.Status.ToString(), descricao = e.Value.Description })
        }));
    }
}
