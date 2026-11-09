# ADR 0013 — Entrega: Docker, Docker Compose e CI com SonarCloud

- **Status:** aceita
- **Data:** 2026-11-09

## Contexto
São cinco APIs, PostgreSQL, RabbitMQ e Cassandra. Quem avalia o portfólio (ou chega no time) precisa subir tudo sem instalar nada além do Docker. Além disso, cada mudança precisa provar que compila, passa nos testes (inclusive os de integração, que dependem de infraestrutura real) e não piora a qualidade do código.

## Decisão
**Imagens**
- **Um `Dockerfile` único e parametrizado** (`SERVICE_PATH`, `ASSEMBLY`) para todos os serviços, em vez de um por serviço: as cinco APIs têm a mesma forma, e uma correção na imagem vale para todas.
- **Multi-stage**: a imagem do SDK só compila; a imagem final usa o `aspnet` runtime (bem menor, com menos superfície de ataque).
- Roda com o **usuário não-root** (`$APP_UID`) que já vem nas imagens oficiais do .NET.

**Ambiente local**
- **`docker compose up --build`** sobe infraestrutura e serviços. As APIs só iniciam depois que PostgreSQL e RabbitMQ passam no **healthcheck** (`depends_on: condition: service_healthy`), e não apenas depois que o contêiner foi criado.
- Senhas com **valores padrão só para desenvolvimento**, que podem ser sobrescritos por um `.env` não versionado (`.env.example` documenta as variáveis).

**CI (GitHub Actions)**
- Pipeline: restore → build → testes de unidade **e** integração com cobertura → SonarCloud → `docker compose build`.
- Os testes de integração rodam no próprio runner porque o `ubuntu-latest` já tem Docker, que é o que o Testcontainers precisa. Não há serviços fixos no workflow.
- Cobertura no formato **OpenCover**, que é o formato lido pelo Sonar para C#. Migrations, `Program.cs` e projetos de teste ficam fora da métrica.
- **SonarCloud** para análise estática, duplicação, cobertura e *quality gate* em "new code" (`fetch-depth: 0`, porque ele precisa do histórico).
- **Sem o secret `SONAR_TOKEN`, a análise é pulada** e o resto do pipeline roda normalmente. Um fork, ou um PR vindo de fora, não quebra por falta de credencial.
- Permissões mínimas no workflow (`contents: read`, `pull-requests: read`).
- O job de imagens depende do job de testes: não se gasta build de imagem com código quebrado.

## Alternativas
- **Kubernetes/Helm local (kind, minikube):** é mais próximo de produção, mas cobra um custo de setup que não combina com "clonar e rodar". Compose resolve o objetivo do projeto.
- **.NET Aspire:** orquestra e dá *dashboard* de telemetria, mas exige o SDK do .NET na máquina de quem avalia. Compose só exige Docker. Fica como evolução.
- **Só os analyzers do Roslyn (sem Sonar):** pegam problemas de código, mas não dão histórico, *quality gate* em PR nem visão de cobertura e duplicação. Os dois se complementam.
- **Publicar imagens num registry (GHCR):** não há ambiente de deploy ainda; o CI só valida que as imagens constroem.

## Consequências
- Um comando sobe o sistema inteiro; o mesmo `Dockerfile` serve para local e CI.
- O Compose não é ambiente de produção: um único PostgreSQL hospeda os bancos de cada serviço (ver ADR 0003), não há réplicas nem gestão de segredos de verdade.
- O CI fica mais lento por causa do Testcontainers, em troca de testar contra PostgreSQL e RabbitMQ reais.

## Como defender
"Um `docker compose up` sobe tudo, com healthcheck para as APIs não subirem antes do banco e da fila. A imagem é multi-stage e roda sem root. No CI, todo PR roda testes de unidade e de integração com Testcontainers e passa pelo *quality gate* do SonarCloud; sem token, o Sonar é pulado e o resto não quebra."
