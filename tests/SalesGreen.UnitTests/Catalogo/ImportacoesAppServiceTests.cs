using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SalesGreen.Catalogo.Application.Importacoes;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Infrastructure.Planilhas;

namespace SalesGreen.UnitTests.Catalogo;

public class ImportacoesAppServiceTests
{
    private const string Cabecalho = "codigo;descricao;categoria;unidade;preco_custo;preco_venda;perecivel;validade_dias";

    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly ImportacaoRepositoryFake _importacoes = new();
    private readonly ImportacoesAppService _service;

    public ImportacoesAppServiceTests() =>
        _service = new ImportacoesAppService(_importacoes, _produtos, new UnitOfWorkFake(),
            [new CsvPlanilhaLeitor(), new XlsxPlanilhaLeitor()],
            new RelogioFixo(new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc)),
            NullLogger<ImportacoesAppService>.Instance);

    private async Task<Importacao> ImportarAsync(params string[] linhas)
    {
        var csv = Encoding.UTF8.GetBytes(Cabecalho + "\n" + string.Join("\n", linhas));
        var id = await _service.ReceberAsync("produtos.csv", "text/csv", csv, default);
        await _service.ProcessarAsync(id, default);
        return _importacoes.Importacoes.Single(i => i.Id == id);
    }

    [Fact]
    public async Task Aceita_linhas_validas_e_registra_motivo_das_rejeitadas()
    {
        var importacao = await ImportarAsync(
            "789;Arroz 5kg;Mercearia;UN;18,50;27,90;nao;",
            "HF-1;Tomate;Hortifruti;KG;4,20;8,99;sim;",          // perecível sem validade
            "X 1;Item;Mercearia;UN;1;2;nao;",                     // código inválido
            "ABC;Item;Mercearia;LITRO;1;2;nao;");                // unidade inválida

        importacao.Status.Should().Be(StatusImportacao.Concluida);
        importacao.TotalLinhas.Should().Be(4);
        importacao.LinhasAceitas.Should().Be(1);
        importacao.Rejeicoes.Select(r => r.NumeroLinha).Should().BeEquivalentTo([3, 4, 5]);
        importacao.Rejeicoes.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Motivo));
        importacao.Conteudo.Should().BeEmpty("o arquivo é descartado após o processamento");
        _produtos.Produtos.Should().ContainSingle();
    }

    [Fact]
    public async Task Reimportar_a_mesma_planilha_nao_duplica_produtos()
    {
        await ImportarAsync("789;Arroz 5kg;Mercearia;UN;18,50;27,90;nao;");
        var segunda = await ImportarAsync("789;Arroz 5kg;Mercearia;UN;18,50;29,90;nao;");

        segunda.LinhasAceitas.Should().Be(1);
        _produtos.Produtos.Should().ContainSingle().Which.PrecoVenda.Valor.Should().Be(29.90m);
    }

    [Fact]
    public async Task Codigo_repetido_na_mesma_planilha_e_rejeitado()
    {
        var importacao = await ImportarAsync(
            "789;Arroz 5kg;Mercearia;UN;18,50;27,90;nao;",
            "789;Arroz 5kg;Mercearia;UN;18,50;27,90;nao;");

        importacao.LinhasAceitas.Should().Be(1);
        importacao.Rejeicoes.Should().ContainSingle().Which.Motivo.Should().Contain("duplicado");
    }

    [Fact]
    public async Task Mensagem_repetida_nao_reprocessa_importacao()
    {
        var importacao = await ImportarAsync("789;Arroz 5kg;Mercearia;UN;18,50;27,90;nao;");

        await _service.ProcessarAsync(importacao.Id, default); // reentrega da mesma mensagem

        importacao.LinhasAceitas.Should().Be(1);
        _produtos.Produtos.Should().ContainSingle();
    }

    [Fact]
    public async Task Cabecalho_invalido_marca_importacao_como_falha()
    {
        var id = await _service.ReceberAsync("p.csv", "text/csv", Encoding.UTF8.GetBytes("a;b\n1;2"), default);
        await _service.ProcessarAsync(id, default);

        var importacao = _importacoes.Importacoes.Single();
        importacao.Status.Should().Be(StatusImportacao.Falhou);
        importacao.MensagemErro.Should().Contain("Colunas ausentes");
    }
}
