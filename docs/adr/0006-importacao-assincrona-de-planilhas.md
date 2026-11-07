# ADR 0006 — Importação assíncrona de produtos por planilha (CSV/XLSX)

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
Mercados e hortifrútis mantêm o cadastro em planilhas. Arquivos podem ter milhares de linhas e erros pontuais; processar tudo dentro da requisição HTTP gera timeout e uma experiência ruim. Upload de arquivo também é um vetor clássico de ataque.

## Decisão
1. `POST /api/produtos/importacoes` **valida o arquivo**, grava a `Importacao` (com o conteúdo) e responde **202 Accepted** com o link do relatório. A gravação dispara um domain event → `ImportacaoProdutosSolicitada` via **Outbox**.
2. O consumidor lê as linhas, valida **linha a linha** e faz **upsert por código** (carregando os produtos existentes de uma vez, sem N+1). Linhas inválidas viram `LinhaRejeitada` (número, código e motivo); as válidas são gravadas. Ao final o arquivo é descartado do banco.
3. `GET /api/produtos/importacoes/{id}` devolve o relatório (Dapper).

**Idempotência:** o código do produto é a chave natural (índice único). Reimportar a mesma planilha atualiza em vez de duplicar; código repetido dentro da mesma planilha é rejeitado; uma importação já processada ignora mensagens repetidas.

**Segurança do upload (OWASP):**
- Limite de 5 MB aplicado pelo Kestrel (`RequestSizeLimit`) **e** pela aplicação; máximo de 10.000 linhas.
- Extensão permitida (`.csv`/`.xlsx`) + Content-Type coerente + **assinatura binária** (XLSX começa com `PK\x03\x04`; CSV não pode conter byte nulo nem ser ZIP). O nome do arquivo é reduzido a `Path.GetFileName`.
- XLSX lido com ClosedXML usando **valores**, nunca executando fórmulas; arquivo corrompido vira erro de negócio.
- **CSV/Formula Injection**: textos começando com `=`, `+`, `-`, `@` são rejeitados no agregado `Produto`.

## Alternativas consideradas
- Guardar o arquivo em blob storage (S3/Azure Blob) em vez do banco: melhor para arquivos grandes; para 5 MB o banco simplifica o ambiente local.
- Processar em background com `IHostedService`/canal em memória: perderia o trabalho se o processo cair. A fila dá durabilidade, retry e DLQ.

## Como defender
"Upload responde 202 e processa por fila: a API não trava e o trabalho sobrevive a quedas. Valido o arquivo pelo conteúdo, não pelo nome. O código do produto é a chave de idempotência, garantida também por índice único."
