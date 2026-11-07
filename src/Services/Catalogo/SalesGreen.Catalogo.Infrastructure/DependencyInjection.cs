using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SalesGreen.BuildingBlocks.Messaging;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.Catalogo.Application;
using SalesGreen.Catalogo.Application.Importacoes;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Domain.Produtos;
using SalesGreen.Catalogo.Infrastructure.Persistence;
using SalesGreen.Catalogo.Infrastructure.Planilhas;
using SalesGreen.SharedKernel;

namespace SalesGreen.Catalogo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogoInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Catalogo")
            ?? throw new InvalidOperationException("ConnectionStrings:Catalogo não configurada.");

        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<CatalogoDbContext>((sp, o) => o.UseNpgsql(sp.GetRequiredService<NpgsqlDataSource>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CatalogoDbContext>());
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IImportacaoRepository, ImportacaoRepository>();
        services.AddScoped<IImportacaoConsultas, ImportacaoConsultas>();
        services.AddSingleton<IPlanilhaLeitor, CsvPlanilhaLeitor>();
        services.AddSingleton<IPlanilhaLeitor, XlsxPlanilhaLeitor>();
        services.AddDomainEvents(typeof(ProdutoCadastradoHandler).Assembly); // handlers ficam na Application

        services.AddMensageria<CatalogoDbContext>(configuration, x =>
            x.AddConsumer<ImportacaoProdutosSolicitadaConsumer>());

        return services;
    }
}
