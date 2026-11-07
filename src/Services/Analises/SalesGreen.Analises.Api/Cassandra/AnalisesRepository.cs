using Cassandra;

namespace SalesGreen.Analises.Api.Cassandra;

public sealed record VendaDia(DateOnly Dia, decimal Quantidade, decimal Valor, int Pedidos);
public sealed record SaldoProduto(string ProdutoCodigo, decimal Saldo, DateTimeOffset AtualizadoEm);

/// <summary>Acesso ao Cassandra com prepared statements (parse uma vez, executa muitas; e protege contra injeção de CQL).</summary>
public sealed class AnalisesRepository(CassandraContexto contexto)
{
    private PreparedStatement? _inserirVenda;
    private PreparedStatement? _atualizarSaldo;
    private PreparedStatement? _vendasPeriodo;

    public async Task RegistrarVendaAsync(string produtoCodigo, DateOnly dia, Guid pedidoId, string descricao, decimal quantidade, decimal valor)
    {
        _inserirVenda ??= await contexto.Session.PrepareAsync(
            "INSERT INTO vendas_por_produto_dia (produto_codigo, dia, pedido_id, descricao, quantidade, valor) VALUES (?, ?, ?, ?, ?, ?)");
        await contexto.Session.ExecuteAsync(_inserirVenda.Bind(
            produtoCodigo, new LocalDate(dia.Year, dia.Month, dia.Day), pedidoId, descricao, quantidade, valor));
    }

    /// <summary>
    /// USING TIMESTAMP com o horário do EVENTO: se mensagens chegarem fora de ordem, o Cassandra mantém
    /// a mais recente (last-write-wins pelo timestamp que nós informamos, não pelo horário de chegada).
    /// </summary>
    public async Task AtualizarSaldoAsync(string produtoCodigo, decimal saldo, DateTime alteradoEm)
    {
        _atualizarSaldo ??= await contexto.Session.PrepareAsync(
            "INSERT INTO saldo_estoque (produto_codigo, saldo, atualizado_em) VALUES (?, ?, ?) USING TIMESTAMP ?");
        var micros = new DateTimeOffset(DateTime.SpecifyKind(alteradoEm, DateTimeKind.Utc)).ToUnixTimeMilliseconds() * 1000;
        await contexto.Session.ExecuteAsync(_atualizarSaldo.Bind(produtoCodigo, saldo, new DateTimeOffset(alteradoEm, TimeSpan.Zero), micros));
    }

    public async Task<IReadOnlyList<VendaDia>> VendasPorDiaAsync(string produtoCodigo, DateOnly de, DateOnly ate)
    {
        _vendasPeriodo ??= await contexto.Session.PrepareAsync(
            "SELECT dia, quantidade, valor FROM vendas_por_produto_dia WHERE produto_codigo = ? AND dia >= ? AND dia <= ?");
        var linhas = await contexto.Session.ExecuteAsync(_vendasPeriodo.Bind(
            produtoCodigo, new LocalDate(de.Year, de.Month, de.Day), new LocalDate(ate.Year, ate.Month, ate.Day)));

        return linhas
            .Select(r => (Dia: r.GetValue<LocalDate>("dia"), Qtd: r.GetValue<decimal>("quantidade"), Valor: r.GetValue<decimal>("valor")))
            .GroupBy(r => new DateOnly(r.Dia.Year, r.Dia.Month, r.Dia.Day))
            .Select(g => new VendaDia(g.Key, g.Sum(x => x.Qtd), g.Sum(x => x.Valor), g.Count()))
            .OrderBy(v => v.Dia)
            .ToList();
    }

    /// <summary>
    /// Varre a tabela de saldos (pequena: uma linha por produto). Em um catálogo enorme, o ideal seria
    /// manter uma tabela já calculada de "produtos em risco" atualizada a cada evento.
    /// </summary>
    public async Task<IReadOnlyList<SaldoProduto>> SaldosAsync(int limite)
    {
        var linhas = await contexto.Session.ExecuteAsync(new SimpleStatement(
            "SELECT produto_codigo, saldo, atualizado_em FROM saldo_estoque LIMIT ?", limite));
        return linhas.Select(r => new SaldoProduto(
            r.GetValue<string>("produto_codigo"), r.GetValue<decimal>("saldo"), r.GetValue<DateTimeOffset>("atualizado_em"))).ToList();
    }
}
