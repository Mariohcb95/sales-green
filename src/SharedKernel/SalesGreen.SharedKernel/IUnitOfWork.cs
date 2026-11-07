namespace SalesGreen.SharedKernel;

/// <summary>Confirma a transação (dados de negócio + outbox) de uma só vez.</summary>
public interface IUnitOfWork
{
    Task SalvarAsync(CancellationToken ct);
}
