using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SalesGreen.SharedKernel;

namespace SalesGreen.BuildingBlocks.Web;

/// <summary>
/// Converte exceções em ProblemDetails (RFC 9457). Erros 500 nunca expõem detalhes internos
/// (OWASP A05 – Security Misconfiguration / vazamento de stack trace).
/// </summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada"),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflito"),
            ServicoIndisponivelException => (StatusCodes.Status503ServiceUnavailable, "Serviço dependente indisponível"),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        if (status >= 500)
            logger.LogError(exception, "Erro não tratado em {Metodo} {Caminho}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning("Requisição rejeitada ({Status}): {Mensagem}", status, exception.Message);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = status == StatusCodes.Status500InternalServerError ? "Ocorreu um erro inesperado. Consulte os logs pelo traceId." : exception.Message
            }
        });
    }
}
