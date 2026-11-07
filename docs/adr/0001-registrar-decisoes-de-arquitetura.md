# ADR 0001 — Registrar decisões de arquitetura (ADR)

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
O projeto é um portfólio: cada escolha técnica precisa ser explicável numa entrevista, inclusive as alternativas descartadas.

## Decisão
Registrar cada decisão relevante em `docs/adr/NNNN-titulo.md` no formato curto de Michael Nygard: **Contexto → Decisão → Consequências**, com uma seção extra **"Como defender"** (a resposta de 30 segundos para a entrevista).

## Consequências
- O "porquê" fica versionado junto do código e pode ser revisado em PR.
- Uma decisão nunca é apagada: se mudar, cria-se um novo ADR que a substitui (status "substituída por ADR X").

## Como defender
"Código mostra o *como*; o ADR guarda o *porquê* e o que foi descartado. Isso evita rediscutir decisões e ajuda quem chega no time."
