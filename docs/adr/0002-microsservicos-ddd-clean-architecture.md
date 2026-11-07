# ADR 0002 — Microsserviços por bounded context, DDD e Clean Architecture

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
O domínio de varejo tem áreas com linguagem e ritmo de mudança diferentes: **Catálogo** (cadastro), **Pedidos** (venda), **Estoque** (saldo/lotes) e **Faturamento** (tributos/nota). "Produto" significa coisas diferentes em cada uma (no Estoque importa lote e validade; no Faturamento, base de cálculo).

## Decisão
- **Um serviço por bounded context**, cada um com seu banco e seu modelo. A comunicação entre eles é por **eventos/comandos** (RabbitMQ); a única chamada síncrona é Pedidos → Catálogo (preço vigente).
- **Clean Architecture em cada serviço**, com dependências sempre apontando para dentro:
  `Api → Infrastructure → Application → Domain` (o Domain não referencia nada além do Shared Kernel).
- **DDD tático**:
  - *Agregados* (`Produto`, `Importacao`, `Pedido`, `ItemEstoque`, `Reserva`, `NotaFiscal`): só a raiz é alterada de fora, por métodos que validam invariantes. Setters privados.
  - *Value objects* (`Dinheiro`, `Quantidade`, `CodigoProduto`): imutáveis, igualdade por valor, validação no construtor.
  - *Domain events* (`ProdutoCadastrado`, `PedidoCriado`, `SaldoAlterado`...) despachados no `SaveChanges` e traduzidos em eventos de integração.
- **Shared Kernel** pequeno (`SalesGreen.SharedKernel`): só os value objects e tipos-base realmente comuns. **Contracts** separados (`SalesGreen.Contracts`): mensagens com tipos primitivos, nunca entidades.
- **Sem MediatR**: casos de uso são classes de aplicação simples (`ProdutosAppService` etc.) e o despachante de domain events tem ~20 linhas (ver ADR 0012).
- O serviço **Análises** é só leitura (projeção), sem regras de domínio: um único projeto organizado em pastas.

## Consequências
- ✅ Regras de negócio testáveis sem banco/HTTP (81 testes de unidade rodam em segundos).
- ✅ Cada serviço pode evoluir/escalar/deployar sozinho.
- ⚠️ Mais projetos e mais cerimônia do que um monólito. Para um sistema deste tamanho, um **monólito modular** seria uma escolha legítima — aqui os microsserviços existem para demonstrar mensageria, saga e consistência eventual.
- ⚠️ Consistência eventual entre serviços: o pedido nasce "Pendente" e só depois vira "Confirmado" ou "Cancelado".

## Como defender
"Separei por bounded context porque cada área tem modelo e linguagem próprios. Dentro do serviço uso Clean Architecture para isolar o domínio de framework: o agregado garante as invariantes, a Application orquestra, a Infrastructure implementa as portas. Sei que é mais cerimônia; num produto real eu começaria por um monólito modular com essas mesmas fronteiras."
