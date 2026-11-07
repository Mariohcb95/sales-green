using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.BuildingBlocks.Web;
using SalesGreen.Catalogo.Application;
using SalesGreen.Catalogo.Infrastructure;
using SalesGreen.Catalogo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
       .AddDbContextCheck<CatalogoDbContext>("postgres", tags: [ServiceDefaults.TagReady]);

builder.Services
       .AddCatalogoApplication()
       .AddCatalogoInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
await app.AplicarMigrationsAsync<CatalogoDbContext>();
await app.RunAsync();

public partial class Program; // exposto para os testes de integração (WebApplicationFactory)
