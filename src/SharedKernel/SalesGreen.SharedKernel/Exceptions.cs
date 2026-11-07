namespace SalesGreen.SharedKernel;

/// <summary>Violação de regra de negócio (invariante). Vira HTTP 422 via ProblemDetails.</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Recurso não encontrado. Vira HTTP 404 via ProblemDetails.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>Conflito de estado (ex.: código duplicado). Vira HTTP 409 via ProblemDetails.</summary>
public class ConflictException(string message) : Exception(message);

/// <summary>Dependência externa indisponível (ex.: circuito aberto). Vira HTTP 503 via ProblemDetails.</summary>
public class ServicoIndisponivelException(string message, Exception? inner = null) : Exception(message, inner);
