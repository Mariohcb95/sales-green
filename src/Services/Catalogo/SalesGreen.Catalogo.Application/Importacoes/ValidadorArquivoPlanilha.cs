using SalesGreen.Catalogo.Domain.Importacoes;
using SalesGreen.SharedKernel;

namespace SalesGreen.Catalogo.Application.Importacoes;

/// <summary>
/// Validação do arquivo enviado (OWASP: Unrestricted File Upload). Não confia no nome nem no Content-Type
/// informados pelo cliente: confere extensão, tamanho E a assinatura binária (magic bytes) do conteúdo.
/// </summary>
public static class ValidadorArquivoPlanilha
{
    public const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5 MB
    public const int MaximoLinhas = 10_000;

    private static readonly byte[] AssinaturaZip = [0x50, 0x4B, 0x03, 0x04]; // XLSX é um pacote ZIP

    private static readonly HashSet<string> ContentTypesCsv = new(StringComparer.OrdinalIgnoreCase)
        { "text/csv", "text/plain", "application/vnd.ms-excel", "application/csv", "application/octet-stream" };

    private static readonly HashSet<string> ContentTypesXlsx = new(StringComparer.OrdinalIgnoreCase)
        { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream" };

    public static FormatoPlanilha Validar(string? nomeArquivo, string? contentType, ReadOnlySpan<byte> conteudo)
    {
        if (conteudo.Length == 0)
            throw new DomainException("Arquivo vazio.");
        if (conteudo.Length > TamanhoMaximoBytes)
            throw new DomainException($"Arquivo maior que o limite de {TamanhoMaximoBytes / 1024 / 1024} MB.");

        var extensao = Path.GetExtension(nomeArquivo ?? string.Empty).ToLowerInvariant();
        var tipo = (contentType ?? string.Empty).Split(';')[0].Trim();

        switch (extensao)
        {
            case ".xlsx":
                if (tipo.Length > 0 && !ContentTypesXlsx.Contains(tipo))
                    throw new DomainException($"Content-Type '{tipo}' não corresponde a um arquivo XLSX.");
                if (!conteudo.StartsWith(AssinaturaZip))
                    throw new DomainException("Conteúdo não é um XLSX válido.");
                return FormatoPlanilha.Xlsx;

            case ".csv":
                if (tipo.Length > 0 && !ContentTypesCsv.Contains(tipo))
                    throw new DomainException($"Content-Type '{tipo}' não corresponde a um arquivo CSV.");
                var amostra = conteudo[..Math.Min(conteudo.Length, 8192)];
                if (amostra.StartsWith(AssinaturaZip) || amostra.Contains((byte)0))
                    throw new DomainException("Conteúdo não é um CSV de texto válido.");
                return FormatoPlanilha.Csv;

            default:
                throw new DomainException("Formato não suportado. Envie um arquivo .csv ou .xlsx.");
        }
    }
}
