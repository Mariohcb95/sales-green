using System.Text;
using ClosedXML.Excel;
using SalesGreen.Catalogo.Application.Importacoes;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.Catalogo.Infrastructure.Planilhas;
using SalesGreen.SharedKernel;

namespace SalesGreen.UnitTests.Catalogo;

public class ValidadorArquivoPlanilhaTests
{
    private static readonly byte[] Csv = Encoding.UTF8.GetBytes("codigo;descricao\n1;x\n");

    [Fact]
    public void Aceita_csv_de_texto() =>
        ValidadorArquivoPlanilha.Validar("produtos.CSV", "text/csv", Csv).Should().Be(FormatoPlanilha.Csv);

    [Fact]
    public void Aceita_xlsx_com_assinatura_zip() =>
        ValidadorArquivoPlanilha.Validar("p.xlsx", null, PlanilhaXlsx.Gerar(["codigo"])).Should().Be(FormatoPlanilha.Xlsx);

    [Theory]
    [InlineData("virus.exe", "application/octet-stream")]
    [InlineData("produtos.csv", "application/x-msdownload")]
    [InlineData("produtos.txt", "text/plain")]
    public void Rejeita_extensao_ou_content_type_suspeito(string nome, string contentType) =>
        FluentActions.Invoking(() => ValidadorArquivoPlanilha.Validar(nome, contentType, Csv)).Should().Throw<DomainException>();

    [Fact]
    public void Rejeita_xlsx_falso_pela_assinatura() =>
        FluentActions.Invoking(() => ValidadorArquivoPlanilha.Validar("p.xlsx", null, Csv))
            .Should().Throw<DomainException>().WithMessage("*XLSX válido*");

    [Fact]
    public void Rejeita_binario_disfarcado_de_csv() =>
        FluentActions.Invoking(() => ValidadorArquivoPlanilha.Validar("p.csv", "text/csv", [0x4D, 0x5A, 0x00, 0x01]))
            .Should().Throw<DomainException>();

    [Fact]
    public void Rejeita_arquivo_acima_do_limite() =>
        FluentActions.Invoking(() => ValidadorArquivoPlanilha.Validar("p.csv", "text/csv", new byte[ValidadorArquivoPlanilha.TamanhoMaximoBytes + 1]))
            .Should().Throw<DomainException>().WithMessage("*limite*");
}

public class LeitoresPlanilhaTests
{
    private const string Cabecalho = "codigo;descricao;categoria;unidade;preco_custo;preco_venda;perecivel;validade_dias";

    [Fact]
    public void Csv_le_separador_ponto_e_virgula_e_aspas()
    {
        var csv = $"{Cabecalho}\n789;\"Arroz; tipo \"\"1\"\"\";Mercearia;UN;18,50;27,90;nao;\n";

        var linhas = new CsvPlanilhaLeitor().Ler(Encoding.UTF8.GetBytes(csv)).ToList();

        linhas.Should().ContainSingle();
        linhas[0].Numero.Should().Be(2);
        linhas[0].Campo("descricao").Should().Be("Arroz; tipo \"1\"");
        linhas[0].Campo("preco_venda").Should().Be("27,90");
    }

    [Fact]
    public void Csv_aceita_virgula_como_separador()
    {
        var csv = Cabecalho.Replace(';', ',') + "\n789,Arroz,Mercearia,UN,18.5,27.9,nao,\n";
        new CsvPlanilhaLeitor().Ler(Encoding.UTF8.GetBytes(csv)).Single().Campo("preco_custo").Should().Be("18.5");
    }

    [Fact]
    public void Cabecalho_sem_colunas_obrigatorias_falha() =>
        FluentActions.Invoking(() => new CsvPlanilhaLeitor().Ler(Encoding.UTF8.GetBytes("codigo;descricao\n1;x")).ToList())
            .Should().Throw<DomainException>().WithMessage("*Colunas ausentes*");

    [Fact]
    public void Xlsx_le_primeira_aba()
    {
        var bytes = PlanilhaXlsx.Gerar(Cabecalho.Split(';'), ["HF-1", "Tomate", "Hortifruti", "KG", "4.2", "8.99", "sim", "7"]);

        var linha = new XlsxPlanilhaLeitor().Ler(bytes).Single();

        linha.Campo("codigo").Should().Be("HF-1");
        linha.Campo("validade_dias").Should().Be("7");
    }

    [Fact]
    public void Xlsx_corrompido_vira_erro_de_negocio() =>
        FluentActions.Invoking(() => new XlsxPlanilhaLeitor().Ler([0x50, 0x4B, 0x03, 0x04, 0x00]).ToList())
            .Should().Throw<DomainException>();
}

public class ConversorLinhaProdutoTests
{
    [Theory]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("27,9", 27.9)]
    [InlineData("27.90", 27.90)]
    [InlineData("R$ 8,99", 8.99)]
    public void Converte_decimais_no_formato_brasileiro_e_invariante(string texto, decimal esperado) =>
        ConversorLinhaProduto.ConverterDecimal(texto, "preço").Should().Be(esperado);

    [Theory]
    [InlineData("Sim", true)]
    [InlineData("S", true)]
    [InlineData("não", false)]
    [InlineData("", false)]
    public void Converte_booleano(string texto, bool esperado) => ConversorLinhaProduto.ConverterBooleano(texto).Should().Be(esperado);
}

internal static class PlanilhaXlsx
{
    public static byte[] Gerar(string[] cabecalho, params string[][] linhas)
    {
        using var workbook = new XLWorkbook();
        var aba = workbook.AddWorksheet("Produtos");
        for (var c = 0; c < cabecalho.Length; c++) aba.Cell(1, c + 1).Value = cabecalho[c];
        for (var l = 0; l < linhas.Length; l++)
            for (var c = 0; c < linhas[l].Length; c++)
                aba.Cell(l + 2, c + 1).Value = linhas[l][c];
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
