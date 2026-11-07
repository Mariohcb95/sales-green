using System.Text;
using ClosedXML.Excel;
using SalesGreen.Catalogo.Application.Importacoes;
using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.SharedKernel;

namespace SalesGreen.Catalogo.Infrastructure.Planilhas;

/// <summary>
/// Leitor CSV simples (separador ';' padrão do Excel pt-BR ou ','; aspas duplas suportadas).
/// Escrito à mão para não trazer dependência extra para um formato pequeno.
/// </summary>
public sealed class CsvPlanilhaLeitor : IPlanilhaLeitor
{
    public FormatoPlanilha Formato => FormatoPlanilha.Csv;

    public IEnumerable<LinhaPlanilha> Ler(byte[] conteudo)
    {
        using var reader = new StreamReader(new MemoryStream(conteudo), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var primeira = reader.ReadLine() ?? throw new DomainException("Planilha sem cabeçalho.");
        var separador = primeira.Count(c => c == ';') >= primeira.Count(c => c == ',') ? ';' : ',';
        var cabecalho = SepararCampos(primeira, separador).Select(LayoutPlanilhaProdutos.NormalizarCabecalho).ToArray();
        LayoutPlanilhaProdutos.ValidarCabecalho(cabecalho);

        var numero = 1;
        while (reader.ReadLine() is { } linha)
        {
            numero++;
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var campos = SepararCampos(linha, separador);
            var dict = new Dictionary<string, string?>();
            for (var i = 0; i < cabecalho.Length; i++)
                dict[cabecalho[i]] = i < campos.Count ? campos[i] : null;
            yield return new LinhaPlanilha(numero, dict);
        }
    }

    internal static List<string> SepararCampos(string linha, char separador)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var entreAspas = false;
        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];
            if (c == '"')
            {
                if (entreAspas && i + 1 < linha.Length && linha[i + 1] == '"') { atual.Append('"'); i++; }
                else entreAspas = !entreAspas;
            }
            else if (c == separador && !entreAspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else atual.Append(c);
        }
        campos.Add(atual.ToString());
        return campos;
    }
}

/// <summary>Leitor XLSX com ClosedXML (licença MIT). Lê apenas a primeira aba e valores já calculados (nunca fórmulas).</summary>
public sealed class XlsxPlanilhaLeitor : IPlanilhaLeitor
{
    public FormatoPlanilha Formato => FormatoPlanilha.Xlsx;

    public IEnumerable<LinhaPlanilha> Ler(byte[] conteudo)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(conteudo));
        }
        catch (Exception ex) when (ex is not DomainException)
        {
            throw new DomainException("Não foi possível abrir o arquivo XLSX (arquivo corrompido ou protegido).");
        }

        using (workbook)
        {
            var aba = workbook.Worksheets.FirstOrDefault() ?? throw new DomainException("Planilha sem abas.");
            var usadas = aba.RangeUsed() ?? throw new DomainException("Planilha vazia.");
            var linhas = usadas.RowsUsed().ToList();

            var cabecalho = linhas[0].Cells().Select(c => LayoutPlanilhaProdutos.NormalizarCabecalho(c.GetString())).ToArray();
            LayoutPlanilhaProdutos.ValidarCabecalho(cabecalho);

            foreach (var linha in linhas.Skip(1))
            {
                var dict = new Dictionary<string, string?>();
                for (var i = 0; i < cabecalho.Length; i++)
                    dict[cabecalho[i]] = linha.Cell(i + 1).GetFormattedString();
                yield return new LinhaPlanilha(linha.RowNumber(), dict);
            }
        }
    }
}
