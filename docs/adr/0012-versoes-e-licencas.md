# ADR 0012 — .NET 10 e escolha de bibliotecas pela licença

- **Status:** aceita
- **Data:** 2026-11-07

## Decisão
- **.NET 10 (LTS)** — o requisito era ".NET 8 ou mais recente"; a 10 é a LTS atual.
- **Central Package Management** (`Directory.Packages.props`): versões em um lugar só.
- Bibliotecas que mudaram para licença comercial foram evitadas ou fixadas na última versão livre:
  - **MassTransit 8.x** (Apache 2.0). A v9 exige licença comercial.
  - **Sem MediatR** (v13+ comercial): casos de uso são classes simples e o despachante de domain events é próprio (~20 linhas).
  - **FluentAssertions 7.x** (Apache 2.0). A v8 é comercial para uso não open source.
- ClosedXML (MIT) para XLSX; CSV com leitor próprio pequeno.
- `InvariantGlobalization` ligado (imagem menor, sem ICU); formatos pt-BR tratados explicitamente onde importa (decimais da planilha).

## Como defender
"Antes de adotar uma lib eu olho a licença: MassTransit e FluentAssertions ficaram nas últimas versões livres e troquei MediatR por classes de caso de uso — que, honestamente, deixam o fluxo mais explícito."
