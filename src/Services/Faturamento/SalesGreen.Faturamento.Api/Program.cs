using System.Text.Json.Serialization;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.BuildingBlocks.Web;
using SalesGreen.Faturamento.Infrastructure;
using SalesGreen.Faturamento.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
       .AddDbContextCheck<FaturamentoDbContext>("postgres", tags: [ServiceDefaults.TagReady]);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddFaturamentoInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
await app.AplicarMigrationsAsync<FaturamentoDbContext>();
await app.RunAsync();

public partial class Program;
