# ADR 0008 — Resiliência na chamada HTTP Pedidos → Catálogo com Polly

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
Ao criar o pedido, o serviço de Pedidos busca descrição e **preço vigente** no Catálogo. O preço nunca vem do cliente (evita manipulação de valores — OWASP *Business Logic*). Essa é a única dependência síncrona entre serviços, e o Catálogo pode estar lento ou fora.

## Decisão
`HttpClient` tipado (`CatalogoHttpClient`) com pipeline **Polly v8** via `Microsoft.Extensions.Http.Resilience`, de fora para dentro:
1. **Timeout total** de 10 s (inclui as tentativas).
2. **Retry**: 3 tentativas com backoff exponencial **com jitter** (evita que várias instâncias tentem no mesmo instante). Só re-tenta erros transitórios (5xx, 408, 429, falhas de rede) — **404 não**.
3. **Circuit breaker**: com ≥ 50% de falhas em 30 s (mínimo 5 chamadas), abre por 15 s e falha rápido em vez de empilhar requisições num serviço já doente.
4. **Timeout por tentativa** de 2 s.

Falhas finais viram `ServicoIndisponivelException` → **HTTP 503** em ProblemDetails.

## Alternativas
Manter uma cópia local dos preços no Pedidos alimentada por eventos (`ProdutoCadastrado`/`PrecoAlterado`) eliminaria a chamada síncrona — melhor para disponibilidade, ao custo de consistência eventual no preço. Fica como evolução.

## Como defender
"Retry só para erro transitório e com jitter; circuit breaker para não derrubar o Catálogo de vez; timeouts para não prender threads. Se o circuito abrir, devolvo 503 claro em vez de 500."
