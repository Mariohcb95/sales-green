using System.Text.Json.Serialization;
using SalesGreen.BuildingBlocks.Persistence;
using SalesGreen.BuildingBlocks.Web;
using SalesGreen.Pedidos.Application;
using SalesGreen.Pedidos.Infrastructure;
using SalesGreen.Pedidos.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults()
       .AddDbContextCheck<PedidosDbContext>("postgres", tags: [ServiceDefaults.TagReady]);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
       .AddPedidosApplication()
       .AddPedidosInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseServiceDefaults();
await app.AplicarMigrationsAsync<PedidosDbContext>();
await app.RunAsync();

public partial class Program;
