using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using SalesGreen.Catalogo.Application.Importacoes;
using SalesGreen.Catalogo.Application.Produtos;

namespace SalesGreen.IntegrationTests;

public sealed class CatalogoApiFactory(InfraFixture infra) : WebApplicationFactory<Program>
{
    private readonly string _database = $"catalogo_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting é lido antes do Program registrar os serviços (diferente de ConfigureAppConfiguration).
        builder.UseSetting("ConnectionStrings:Catalogo", infra.ConnectionString(_database));
        builder.UseSetting("ConnectionStrings:RabbitMq", infra.RabbitMqConnectionString);
    }
}

/// <summary>Ponta a ponta no Catálogo: HTTP → EF Core/PostgreSQL → Outbox → RabbitMQ → consumidor → Dapper.</summary>
[Collection(InfraCollection.Nome)]
public sealed class CatalogoApiTests(InfraFixture infra)
{
    private readonly HttpClient _http = infra.CatalogoApi.CreateClient();

    [Fact]
    public async Task Cadastra_consulta_e_rejeita_codigo_duplicado()
    {
        var request = new CadastrarProdutoRequest("789100", "Café Torrado 500g", "Mercearia", "UN", 12.3m, 19.9m, false, null);

        var criado = await _http.PostAsJsonAsync("api/produtos", request);
        criado.StatusCode.Should().Be(HttpStatusCode.Created);

        var produto = await _http.GetFromJsonAsync<ProdutoResponse>("api/produtos/codigo/789100");
        produto!.Descricao.Should().Be("Café Torrado 500g");

        var duplicado = await _http.PostAsJsonAsync("api/produtos", request);
        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problema = await duplicado.Content.ReadFromJsonAsync<ProblemDetails>();
        problema!.Title.Should().Be("Conflito");
        problema.Extensions.Should().ContainKey("traceId");
    }

    [Fact]
    public async Task Regra_de_negocio_violada_retorna_422_com_problem_details()
    {
        var request = new CadastrarProdutoRequest("HF-99", "Alface Crespa", "Hortifruti", "UN", 1m, 2.5m, true, null);

        var resposta = await _http.PostAsJsonAsync("api/produtos", request);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        resposta.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Importacao_assincrona_gera_relatorio_e_e_idempotente()
    {
        const string csv = """
            codigo;descricao;categoria;unidade;preco_custo;preco_venda;perecivel;validade_dias
            IMP-1;Banana Prata;Hortifruti;KG;3,10;5,99;sim;5
            IMP-2;Detergente 500ml;Limpeza;UN;1,80;2,79;nao;
            IMP-3;=cmd|' /C calc'!A0;Limpeza;UN;1;2;nao;
            """;

        var primeira = await ImportarEAguardarAsync(csv);
        primeira.Status.Should().Be("Concluida");
        primeira.LinhasAceitas.Should().Be(2);
        primeira.Rejeicoes.Should().ContainSingle(r => r.CodigoProduto == "IMP-3");

        var segunda = await ImportarEAguardarAsync(csv);
        segunda.LinhasAceitas.Should().Be(2);

        var produtos = await _http.GetFromJsonAsync<List<ProdutoResponse>>("api/produtos?busca=Banana");
        produtos.Should().ContainSingle(p => p.Codigo == "IMP-1", "reimportar não pode duplicar produtos");
    }

    [Fact]
    public async Task Arquivo_com_extensao_nao_suportada_e_rejeitado()
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent("MZ"u8.ToArray());
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(arquivo, "arquivo", "planilha.exe");

        var resposta = await _http.PostAsync("api/produtos/importacoes", form);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Health_check_de_readiness_verifica_postgres_e_rabbitmq()
    {
        var resposta = await AguardarAsync(async () =>
        {
            var r = await _http.GetAsync("health/ready");
            return r.StatusCode == HttpStatusCode.OK ? r : null;
        });

        (await resposta.Content.ReadAsStringAsync()).Should().Contain("postgres").And.Contain("masstransit");
    }

    private async Task<ImportacaoRelatorio> ImportarEAguardarAsync(string csv)
    {
        using var form = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(arquivo, "arquivo", "produtos.csv");

        var resposta = await _http.PostAsync("api/produtos/importacoes", form);
        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var location = resposta.Headers.Location!;

        return await AguardarAsync(async () =>
        {
            var relatorio = await _http.GetFromJsonAsync<ImportacaoRelatorio>(location);
            return relatorio!.Status == "Pendente" ? null : relatorio;
        });
    }

    private static async Task<T> AguardarAsync<T>(Func<Task<T?>> condicao) where T : class
    {
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < limite)
        {
            if (await condicao() is { } resultado) return resultado;
            await Task.Delay(500);
        }
        throw new TimeoutException("Condição não atendida em 60s.");
    }
}
