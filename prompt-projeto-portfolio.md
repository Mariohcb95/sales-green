# Prompt — Projeto de portfólio: microsserviços de varejo (.NET)

> Cole o texto abaixo no Claude Code, dentro de uma pasta vazia onde o repositório vai ficar.

---

Quero construir comigo, passo a passo, um projeto de portfólio em .NET. Sou desenvolvedor C# pleno, trabalho com .NET Framework / ASP.NET Web Forms e PostgreSQL num ERP (financeiro, PDV, e-commerce, NF-e). Este projeto serve para dois objetivos:

1. Demonstrar, para vagas de backend .NET, as práticas modernas que estou estudando.
2. Servir de base para o EconoMax, um sistema meu em que empresas (mercados, hortifrútis) cadastram ou importam produtos e vendas por planilha e recebem análises (ruptura, perdas, padrões de venda).

Eu preciso entender e conseguir defender em entrevista cada decisão técnica. Por isso, explique o "porquê" de cada padrão quando ele aparecer, de forma curta, e registre as decisões importantes em `docs/adr/` (Architecture Decision Records).

## Domínio

Sistema de varejo com quatro serviços:

- **Catálogo**: cadastro de produtos (código, descrição, categoria, unidade de medida, preço de custo, preço de venda, se é perecível e prazo de validade padrão). Permite **importar produtos por planilha (CSV e XLSX)**. A importação é assíncrona: a API recebe o arquivo, registra uma importação e publica um evento; um consumidor processa as linhas, valida, e grava um relatório com linhas aceitas e rejeitadas (e o motivo). Reimportar a mesma planilha não pode duplicar produtos (idempotência por código do produto).
- **Pedidos**: cria pedidos com itens de produtos do catálogo e publica `PedidoCriado`.
- **Estoque**: controla saldo por produto (e por lote/validade para perecíveis). Reserva itens quando um pedido é criado; se faltar saldo, publica `EstoqueInsuficiente`.
- **Faturamento**: quando o pedido é confirmado, calcula os impostos e gera uma nota fiscal **simulada** (não emite NF-e real). O cálculo de impostos fica isolado num motor de regras no domínio, preparado para receber depois as regras da reforma tributária (IBS/CBS).

Fluxo principal: Pedido criado → Estoque reserva → Faturamento gera a nota → Pedido confirmado. Se o estoque falhar, o pedido é cancelado (compensação via **Saga**).

## Stack e práticas obrigatórias

- .NET 8 ou mais recente, ASP.NET Core Web API, C# moderno.
- PostgreSQL com Entity Framework Core (um banco por serviço). Use Dapper onde fizer sentido para leituras (ex.: relatórios), e explique por quê.
- DDD: agregados, value objects (ex.: `Dinheiro`, `CodigoProduto`, `Quantidade`), domain events, bounded contexts por serviço.
- Clean Architecture em cada serviço (Domain, Application, Infrastructure, Api), com SOLID.
- Mensageria com RabbitMQ e MassTransit.
- **Outbox** (do MassTransit) para publicar eventos de forma consistente com o banco, **idempotência** nos consumidores, **retry** e **DLQ**.
- **Saga** (state machine do MassTransit) orquestrando o fluxo do pedido.
- Polly para resiliência em chamadas HTTP entre serviços, se houver.
- Testes: xUnit nos domínios e casos de uso; testes de integração com Testcontainers (PostgreSQL e RabbitMQ).
- Docker e Docker Compose: `docker compose up` deve subir tudo (serviços, bancos, RabbitMQ).
- CI no GitHub Actions: build, testes e análise no SonarCloud.
- Segurança básica pensando em OWASP Top 10: validação de entrada (inclusive do arquivo de planilha: tamanho, tipo, conteúdo), sem segredos no repositório, tratamento de erros com ProblemDetails.
- Logs estruturados e health checks em todos os serviços.

## Como quero trabalhar (em fases)

Não gere tudo de uma vez. Faça uma fase por vez, me mostre o resultado, rode os testes e espere eu confirmar antes de seguir.

- **Fase 1 — Estrutura**: solução, projetos por serviço e camada, docker-compose com PostgreSQL e RabbitMQ, README inicial.
- **Fase 2 — Catálogo**: CRUD de produtos com DDD e testes. Depois, importação de planilha assíncrona com relatório de erros e idempotência.
- **Fase 3 — Pedidos e Faturamento**: criação de pedido, evento via Outbox, consumidor no Faturamento com o motor de impostos e a nota simulada.
- **Fase 4 — Estoque e Saga**: reserva de estoque, compensação e a Saga orquestrando tudo, com testes do caminho feliz e do cancelamento.
- **Fase 5 — Qualidade e entrega**: testes de integração com Testcontainers, GitHub Actions com SonarCloud, health checks, logs estruturados.
- **Fase 6 (opcional) — Ponte para o EconoMax**: um serviço de **Análises** que consome os eventos de venda e grava uma projeção em um banco NoSQL (Apache Cassandra), com consultas como "produtos com risco de ruptura" e "vendas por produto por dia". Explique por que Cassandra serve (ou não) para esse caso.

## README final

O README deve ter: o problema que o projeto resolve, um diagrama da arquitetura (Mermaid), o fluxo de eventos, as tecnologias, como rodar localmente, como rodar os testes e uma seção "Decisões técnicas" resumindo os ADRs.

Comece pela Fase 1. Antes de escrever código, me apresente em poucas linhas a estrutura de pastas e os nomes dos projetos e me pergunte o nome do repositório.
