# ADR 0005 — Saga orquestrada (state machine do MassTransit) para o fluxo do pedido

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
Criar um pedido envolve três serviços com bancos separados. Não existe transação distribuída (2PC não é opção prática com RabbitMQ e bancos independentes). Se uma etapa falha, as anteriores precisam ser **compensadas**.

## Decisão
Uma **saga orquestrada** (`PedidoStateMachine`) no serviço de Pedidos, com estado persistido na tabela `pedido_saga` (EF Core, lock pessimista por linha):

| Estado atual            | Evento               | Ação                                                   | Próximo estado          |
|-------------------------|----------------------|--------------------------------------------------------|-------------------------|
| (inicial)               | `PedidoCriado`       | publica `ReservarEstoque`                              | AguardandoEstoque       |
| AguardandoEstoque       | `EstoqueReservado`   | publica `GerarNotaFiscal`                              | AguardandoFaturamento   |
| AguardandoEstoque       | `EstoqueInsuficiente`| publica `PedidoCancelado`                              | Cancelado               |
| AguardandoFaturamento   | `NotaFiscalGerada`   | publica `PedidoConfirmado`                             | Confirmado              |
| AguardandoFaturamento   | `FaturamentoFalhou`  | **compensa**: `LiberarReservaEstoque` + `PedidoCancelado` | Cancelado            |

- Eventos duplicados ou atrasados são **ignorados explicitamente** (`Ignore(...)`) em cada estado.
- O agregado `Pedido` não conhece a saga: consumidores de `PedidoConfirmado`/`PedidoCancelado` chamam `pedido.Confirmar()`/`pedido.Cancelar()`.
- Para demonstrar a compensação, o Faturamento recusa notas acima de R$ 50.000 (`NotaFiscal.ValorMaximoEmissao`).
- Comandos da saga são enviados via `Publish` (cada comando tem um único consumidor). Simplifica o roteamento; `Send` para endereço explícito seria a alternativa mais "purista".

## Por que orquestração e não coreografia?
Na coreografia cada serviço reage aos eventos dos outros e o fluxo fica espalhado. Com 3+ passos e compensação, um orquestrador deixa o fluxo **explícito, testável em um lugar só** e fácil de observar (basta olhar a tabela da saga). O custo é um ponto central de lógica — aceitável porque ele só coordena, não contém regra de negócio.

## Consequências
- ✅ Testes da saga com o *test harness* em memória: caminho feliz, estoque insuficiente, falha no faturamento e evento duplicado.
- ⚠️ Não há *timeout* (ex.: estoque nunca responde). Próximo passo: `Schedule` do MassTransit para cancelar pedidos parados.

## Como defender
"Sem transação distribuída, uso saga com compensação. Escolhi orquestração porque o fluxo tem compensação e fica explícito numa state machine testável. Cada transição é idempotente e eventos fora de ordem são ignorados."
