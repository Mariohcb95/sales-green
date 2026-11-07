using Cassandra;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CassandraSession = Cassandra.ISession;

namespace SalesGreen.Analises.Api.Cassandra;

public sealed class CassandraOptions
{
    public string[] ContactPoints { get; set; } = ["localhost"];
    public int Port { get; set; } = 9042;
    public string Keyspace { get; set; } = "salesgreen_analises";
    public string? Username { get; set; }
    public string? Password { get; set; }
}

/// <summary>
/// Dono da sessão do Cassandra (singleton: a sessão é thread-safe e cara de criar) e do schema.
/// As tabelas são modeladas a partir das CONSULTAS (query-first), como manda a modelagem no Cassandra.
/// </summary>
public sealed class CassandraContexto(CassandraOptions options, ILogger<CassandraContexto> logger) : IDisposable
{
    private Cluster? _cluster;
    private CassandraSession? _session;

    public CassandraSession Session => _session ?? throw new InvalidOperationException("Cassandra ainda não inicializado.");

    public async Task InicializarAsync(CancellationToken ct, int tentativas = 30)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                var builder = Cluster.Builder().AddContactPoints(options.ContactPoints).WithPort(options.Port);
                if (!string.IsNullOrEmpty(options.Username))
                    builder = builder.WithCredentials(options.Username, options.Password);
                _cluster = builder.Build();
                var session = await _cluster.ConnectAsync();
                await CriarSchemaAsync(session);
                _session = session;
                logger.LogInformation("Cassandra conectado (keyspace {Keyspace})", options.Keyspace);
                return;
            }
            catch (Exception ex) when (tentativa < tentativas && !ct.IsCancellationRequested)
            {
                _cluster?.Dispose();
                logger.LogWarning("Cassandra indisponível (tentativa {Tentativa}/{Total}): {Erro}", tentativa, tentativas, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task CriarSchemaAsync(CassandraSession session)
    {
        // SimpleStrategy/RF=1 é para ambiente local. Em produção: NetworkTopologyStrategy com RF=3.
        await session.ExecuteAsync(new SimpleStatement(
            $"CREATE KEYSPACE IF NOT EXISTS {options.Keyspace} WITH replication = {{'class': 'SimpleStrategy', 'replication_factor': 1}}"));
        session.ChangeKeyspace(options.Keyspace);

        // Consulta: "vendas do produto X por dia, entre datas" → partição = produto, clustering = dia.
        // pedido_id na chave torna a escrita idempotente: reprocessar a mesma venda sobrescreve a mesma linha.
        await session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS vendas_por_produto_dia (
                produto_codigo text,
                dia date,
                pedido_id uuid,
                descricao text,
                quantidade decimal,
                valor decimal,
                PRIMARY KEY ((produto_codigo), dia, pedido_id)
            ) WITH CLUSTERING ORDER BY (dia DESC, pedido_id ASC)
            """));

        // Consulta: "saldo atual de cada produto" (base do cálculo de ruptura).
        await session.ExecuteAsync(new SimpleStatement("""
            CREATE TABLE IF NOT EXISTS saldo_estoque (
                produto_codigo text PRIMARY KEY,
                saldo decimal,
                atualizado_em timestamp
            )
            """));
    }

    public void Dispose()
    {
        _session?.Dispose();
        _cluster?.Dispose();
    }
}

public sealed class CassandraHealthCheck(CassandraContexto contexto) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await contexto.Session.ExecuteAsync(new SimpleStatement("SELECT release_version FROM system.local"));
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cassandra inacessível", ex);
        }
    }
}
