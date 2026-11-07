using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace SalesGreen.IntegrationTests;

/// <summary>
/// Sobe PostgreSQL e RabbitMQ REAIS em containers descartáveis (Testcontainers), uma vez para toda a coleção.
/// Testa o que mocks não pegam: SQL gerado, migrations, constraints, outbox e o broker de verdade.
/// </summary>
public sealed class InfraFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4-management-alpine").Build();

    private CatalogoApiFactory? _catalogo;

    public string RabbitMqConnectionString => _rabbit.GetConnectionString();

    /// <summary>
    /// UMA instância da API para toda a coleção: duas instâncias com bancos diferentes competiriam
    /// pela mesma fila do RabbitMQ (competing consumers) e uma processaria a mensagem da outra.
    /// </summary>
    public CatalogoApiFactory CatalogoApi => _catalogo ??= new CatalogoApiFactory(this);

    /// <summary>Cada teste/serviço usa um banco próprio no mesmo servidor (as migrations criam o banco).</summary>
    public string ConnectionString(string database) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;

    public Task InitializeAsync() => Task.WhenAll(_postgres.StartAsync(), _rabbit.StartAsync());

    public async Task DisposeAsync()
    {
        if (_catalogo is not null) await _catalogo.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbit.DisposeAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class InfraCollection : ICollectionFixture<InfraFixture>
{
    public const string Nome = "infra";
}
