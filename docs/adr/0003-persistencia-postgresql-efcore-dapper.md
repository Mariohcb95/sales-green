# ADR 0003 — PostgreSQL por serviço, EF Core para escrita e Dapper para relatórios

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
Cada serviço precisa ser dono dos seus dados (sem JOIN entre serviços). As escritas carregam regras (agregados); algumas leituras são relatórios simples.

## Decisão
- **Database per service**: bancos `catalogo`, `pedidos`, `estoque` e `faturamento`. No `docker-compose` eles ficam na **mesma instância** do PostgreSQL para economizar recursos locais — o isolamento é lógico (nenhum serviço conhece a connection string do outro). Em produção, cada um pode ir para um servidor próprio sem mudar código.
- **EF Core** nas escritas: mapeia agregados com campos privados, *value converters* para os value objects e **migrations versionadas** aplicadas na subida (com retry enquanto o banco inicia).
- **Dapper** no relatório de importação (`ImportacaoConsultas`): SQL explícito, sem change tracking e sem materializar o agregado (que carregaria o arquivo inteiro em memória). Leitura previsível e rápida.
- **Ids gerados no domínio** (`Guid`) com `ValueGeneratedNever()` — sem isso o EF trata uma entidade filha nova (ex.: novo lote de um item já existente) como existente e emite `UPDATE` em vez de `INSERT`. Bug encontrado pelos testes de integração.
- **Concorrência otimista** no `ItemEstoque` com uma coluna `Versao` (token de concorrência) incrementada a cada alteração do agregado. Duas reservas simultâneas do mesmo produto: a segunda recebe `DbUpdateConcurrencyException` e o *retry* do consumidor reprocessa com o saldo novo. (Tentei `xmin` primeiro, mas ele só é conferido quando a linha da raiz muda — o teste de integração pegou isso.)
- **Sequence do PostgreSQL** para numerar notas fiscais (nada de `SELECT MAX()+1`).
- Índices únicos como **última linha de defesa da idempotência**: `produtos.Codigo`, `reservas.PedidoId`, `notas_fiscais.PedidoId`.

## Consequências
- ✅ Regras no agregado + garantias no banco (defesa em profundidade).
- ⚠️ Relatórios que cruzam serviços exigem uma projeção própria (é o papel do serviço Análises).

## Como defender
"EF Core para o lado de escrita, onde preciso do agregado e do unit of work; Dapper onde é só consulta e quero controle do SQL. Cada serviço tem seu banco; dividir a instância no compose é só economia de ambiente local."
