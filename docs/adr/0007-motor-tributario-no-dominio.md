# ADR 0007 — Motor de regras tributárias no domínio, preparado para IBS/CBS

- **Status:** aceita
- **Data:** 2026-11-07

## Contexto
A reforma tributária (EC 132/2023, LC 214/2025) substitui gradualmente PIS/COFINS/ICMS/ISS por CBS e IBS, com um período de convivência a partir de 2026. O cálculo precisa mudar por cenário sem reescrever o faturamento. **O projeto não emite NF-e real**: a nota é simulada e as alíquotas são ilustrativas.

## Decisão
- `IRegraTributaria`: **uma regra por tributo** (`RegraIcms`, `RegraPis`, `RegraCofins`, `RegraCbs`, `RegraIbs`), cada uma decide se se aplica ao item (`SeAplica`) e calcula o valor.
- `MotorTributario`: aplica as regras a cada item e consolida por tributo. Não conhece tributos específicos (**Open/Closed**: um imposto novo — ex.: Imposto Seletivo — é uma classe nova; há um teste provando isso).
- `CenarioTributario`: `RegimeAtual`, `Transicao2026` (regime atual + CBS 0,9% e IBS 0,1% de teste) e `ReformaPlena` (só CBS/IBS). O cenário vem de configuração (`Faturamento:CenarioTributario`).
- Tudo no **Domain**, sem dependências: testável com testes de unidade puros. Arredondamento por item via `Dinheiro`.
- Endpoint `POST /api/tributos/simulacoes` compara cenários sem emitir nota.

## Consequências
- ✅ Trocar de cenário é configuração; acrescentar regra é uma classe.
- ⚠️ Simplificações: alíquota única por tributo, sem NCM/CST/CFOP, sem créditos, sem diferenças por UF. A próxima evolução natural é a regra receber um contexto (NCM, UF de origem/destino, regime do emitente) e as alíquotas virem de tabela.

## Como defender
"O cálculo de impostos muda por lei; deixei cada tributo como uma regra isolada (Strategy) e o motor só compõe. Para a reforma, CBS e IBS já são regras e o cenário de transição de 2026 é só uma combinação diferente."
