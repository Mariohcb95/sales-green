# ADR 0011 — Estratégia de testes: unidade no domínio, harness na saga, Testcontainers na integração

- **Status:** aceita
- **Data:** 2026-11-07

## Decisão
- **Unidade (xUnit + FluentAssertions)** — `tests/SalesGreen.UnitTests`:
  - Value objects, agregados (invariantes, FEFO, idempotência de `Confirmar`/`Liberar`), motor tributário.
  - Casos de uso com repositórios *fake* em memória (`ProdutosAppService`, `ImportacoesAppService`).
  - Leitores CSV/XLSX e validação de arquivo.
  - **Saga** com o *test harness* do MassTransit (barramento e repositório em memória): caminho feliz, estoque insuficiente, compensação no faturamento e evento duplicado.
- **Integração (Testcontainers)** — `tests/SalesGreen.IntegrationTests`:
  - PostgreSQL e RabbitMQ **reais** em containers descartáveis, iniciados uma vez por execução.
  - Catálogo de ponta a ponta com `WebApplicationFactory`: HTTP → EF/migrations → Outbox → RabbitMQ → consumidor → relatório via Dapper; ProblemDetails; health checks.
  - Estoque direto na Infrastructure: concorrência otimista e índice único de reserva.
  - Uma única instância da API por execução: duas instâncias com bancos diferentes competiriam pela mesma fila.

## Por que não EF InMemory/SQLite nos testes de integração?
Eles não reproduzem o PostgreSQL (tipos, índices, concorrência, SQL do Dapper). Os testes com containers reais encontraram dois bugs que mocks não pegariam: entidades filhas novas virando `UPDATE` e o `xmin` não sendo conferido (ADR 0003).

## Como defender
"Pirâmide: muitos testes rápidos no domínio, a saga testada com o harness em memória, e poucos testes de integração com infraestrutura real via Testcontainers — que pegaram bugs de mapeamento que nenhum mock pegaria."
