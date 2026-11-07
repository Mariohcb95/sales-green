using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SalesGreen.BuildingBlocks.Persistence;

public static class MigrationExtensions
{
    /// <summary>Aplica as migrations na subida, com novas tentativas enquanto o PostgreSQL ainda está iniciando.</summary>
    public static async Task AplicarMigrationsAsync<TDbContext>(this WebApplication app, int tentativas = 10)
        where TDbContext : DbContext
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations de {DbContext} aplicadas", typeof(TDbContext).Name);
                return;
            }
            catch (Exception ex) when (tentativa < tentativas)
            {
                logger.LogWarning(ex, "Banco indisponível (tentativa {Tentativa}/{Total}). Nova tentativa em 3s", tentativa, tentativas);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }
}
