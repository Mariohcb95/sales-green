# ADR 0004 — RabbitMQ + MassTransit com Outbox, Inbox (idempotência), retry e DLQ

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
O problema clássico do *dual write*: gravar no banco e publicar no broker são duas operações. Se o processo cair entre elas, ou o evento se perde (pedido criado, saga nunca começa) ou é publicado algo que não foi gravado. Além disso, brokers entregam **pelo menos uma vez** — mensagens podem chegar duplicadas.

## Decisão
- **MassTransit 8** sobre **RabbitMQ** (convenções de filas/exchanges, retry, saga e test harness prontos). Versão 8.x por ser Apache 2.0 (a v9 é comercial — ADR 0012).
- **Transactional Outbox do MassTransit (EF Core)**:
  - *Bus outbox* nas APIs: `Publish` grava em `OutboxMessage` na **mesma transação** do `SaveChanges`; um serviço em background entrega ao RabbitMQ depois do commit.
  - *Consumer outbox* nos consumidores: mensagens publicadas dentro do consumidor também só saem após o commit.
- **Idempotência em duas camadas**:
  1. **Inbox** do MassTransit (`InboxState`): a mesma mensagem (MessageId) não é processada duas vezes pelo mesmo consumidor (janela de 30 min).
  2. **Idempotência de negócio**: reprocessar não muda o resultado — importação já processada é ignorada; reserva/nota já existente para o pedido só reenvia a resposta; upsert por código do produto; `Pedido.Confirmar` repetido é no-op; no Cassandra a chave inclui o `pedido_id`.
- **Retry** por endpoint: 200 ms → 1 s → 5 s. `DomainException` **não** é re-tentada (erro de regra não melhora com retry).
- **DLQ**: esgotadas as tentativas, o MassTransit move a mensagem para a fila `<fila>_error` com o stack trace nos headers. Dá para inspecionar no painel do RabbitMQ (http://localhost:15672) e reenviar.
- Domain events → eventos de integração: handlers na Application traduzem eventos internos (`PedidoCriadoDomainEvent`) em contratos públicos (`PedidoCriado`).

## Consequências
- ✅ Nunca há evento "fantasma" nem evento perdido por queda entre commit e publish.
- ⚠️ Latência de ~1 s (intervalo de polling do outbox) e tabelas extras em cada banco.
- ⚠️ Ordem global não é garantida — por isso as transições da saga ignoram eventos fora de hora e o Análises usa `USING TIMESTAMP` (ADR 0009).

## Como defender
"Uso Outbox para resolver o dual write: o evento é gravado na mesma transação do dado e entregue depois. Como a entrega é at-least-once, todo consumidor é idempotente — pelo Inbox do MassTransit e também pelas chaves de negócio. Erros transitórios têm retry; o que não se resolve vai para a fila _error, que é a DLQ."
