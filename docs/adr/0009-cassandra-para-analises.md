# ADR 0009 — Apache Cassandra para a projeção de Análises (ponte para o EconoMax)

- **Status:** aceita (como experimento)
- **Data:** 2026-11-07

## Contexto
O EconoMax precisa de análises sobre o histórico de vendas (vendas por produto por dia, risco de ruptura). É um padrão de **muita escrita** (cada item vendido vira uma linha), **consultas conhecidas de antemão** e volume que cresce sem parar (várias lojas × produtos × dias).

## Decisão
Serviço **Análises** consome `VendaRealizada` e `SaldoEstoqueAlterado` e grava **tabelas modeladas pela consulta** (query-first):

```sql
-- "vendas do produto X por dia, entre datas"
vendas_por_produto_dia (produto_codigo, dia, pedido_id, descricao, quantidade, valor)
  PRIMARY KEY ((produto_codigo), dia, pedido_id)   -- partição = produto; ordenado por dia DESC

-- "saldo atual de cada produto"
saldo_estoque (produto_codigo PRIMARY KEY, saldo, atualizado_em)
```

- **Idempotência pelo modelo**: `pedido_id` faz parte da chave, então reprocessar a mesma venda sobrescreve a mesma linha (upsert). Não precisa de Outbox/Inbox aqui.
- **Mensagens fora de ordem**: o saldo é gravado com `USING TIMESTAMP` = horário do evento. O Cassandra resolve conflitos por last-write-wins usando esse timestamp, então um evento antigo que chega atrasado não sobrescreve um mais novo.
- **Ruptura**: `dias de cobertura = saldo / média diária de venda na janela`; nível ALTO se menor que o lead time de reposição.
- Prepared statements (parse uma vez, sem injeção de CQL).

## Cassandra serve para esse caso?
**Serve bem para:** escrita massiva e contínua, séries temporais por chave (produto/loja/dia), escala horizontal linear e alta disponibilidade multi-datacenter. "Vendas por produto por dia" é o exemplo de livro.

**Não serve bem para:** consultas ad hoc, JOIN, agregações arbitrárias (`GROUP BY` livre), varreduras sem chave de partição. A consulta de ruptura **varre** `saldo_estoque` — aceitável com uma linha por produto, mas em escala o certo é manter uma tabela já calculada `produtos_em_risco` atualizada a cada evento. Contadores (`counter`) não são idempotentes, por isso não foram usados.

**Honestamente:** para o volume inicial do EconoMax (dezenas de lojas), **PostgreSQL com particionamento por data, ou um banco colunar (ClickHouse/TimescaleDB)**, atenderia com menos operação e com SQL livre para análises exploratórias. Cassandra passa a compensar quando o volume de escrita e a necessidade de disponibilidade crescem muito. Aqui ele demonstra a modelagem query-first e o raciocínio de trade-off.

## Como defender
"No Cassandra eu modelo pela consulta: a partição é o produto e o dia é a chave de ordenação, então 'vendas por dia' é uma leitura sequencial numa partição. Idempotência vem da chave e ordem vem do USING TIMESTAMP. Para análises exploratórias eu escolheria outra ferramenta."
