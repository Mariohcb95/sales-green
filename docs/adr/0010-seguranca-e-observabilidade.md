# ADR 0010 — Segurança básica (OWASP), ProblemDetails, logs estruturados e health checks

- **Status:** aceita
- **Data:** 2026-11-07

## Decisão

### Segurança (OWASP Top 10 / API Top 10)
| Risco | Medida |
|---|---|
| Injeção (A03) | EF Core/Dapper/CQL sempre parametrizados; `ILIKE` com parâmetro; prepared statements no Cassandra. |
| Upload inseguro | Extensão + Content-Type + magic bytes + tamanho + limite de linhas (ADR 0006). |
| CSV/Formula Injection | Textos começando com `= + - @` são rejeitados no domínio. |
| Manipulação de valores | Preço do pedido vem do Catálogo, nunca do cliente. |
| Consumo excessivo (API4) | Paginação com teto (100), limite de itens por pedido, janela máxima nas análises. |
| Exposição de detalhes (A05) | `GlobalExceptionHandler` → ProblemDetails; erro 500 nunca devolve mensagem/stack, só `traceId`. |
| Segredos (A02/A07) | Nenhuma senha em `appsettings.json`. Credenciais vêm de variáveis de ambiente; o `docker-compose` usa padrões **apenas de desenvolvimento** sobrescrevíveis por `.env` (ignorado pelo git). `appsettings.Development.json` contém só credenciais do ambiente local descartável. Em produção: secret store (Key Vault, Secrets Manager, Docker secrets). |
| Containers | Imagem runtime enxuta, usuário **não-root** (`$APP_UID`). |

**Fora do escopo (próximos passos):** autenticação/autorização (JWT/OIDC com escopos por serviço), rate limiting (`AddRateLimiter`), HTTPS/mTLS entre serviços, usuários de banco com privilégios mínimos.

### Erros
`DomainException` → 422, `NotFoundException` → 404, `ConflictException` → 409, `ServicoIndisponivelException` → 503, validação de DTO → 400 (`ValidationProblemDetails`), resto → 500. Todos em `application/problem+json` com `traceId` e `servico`.

### Observabilidade
- **Serilog** com logs estruturados: JSON compacto em produção (pronto para Seq/Elastic/Loki), texto em desenvolvimento; propriedade `Servico` em todo log; *request logging* por requisição.
- Mensagens de log com *templates* (`"Pedido {PedidoId}..."`), nunca interpolação — os valores viram campos pesquisáveis.
- **Health checks** em todos os serviços:
  - `/health/live` — processo vivo (sem dependências, para não reiniciar o container quando o banco oscila).
  - `/health/ready` — PostgreSQL (`AddDbContextCheck`), RabbitMQ (check do MassTransit) e Cassandra (Análises).
- Próximo passo: OpenTelemetry (traces distribuídos HTTP + RabbitMQ — o MassTransit já emite `Activity`).
