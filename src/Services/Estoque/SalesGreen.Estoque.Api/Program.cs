using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.BuildingBlocks.Web;
using SalesGreen.Estoque.Infrastructure;
using SalesGreen.Estoque.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
       .AddDbContextCheck<EstoqueDbContext>("postgres", tags: [ServiceDefaults.TagReady]);

builder.Services.AddEstoqueInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
await app.AplicarMigrationsAsync<EstoqueDbContext>();
await app.RunAsync();

public partial class Program;
