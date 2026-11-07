#!/usr/bin/env bash
# Demonstração ponta a ponta com o ambiente do docker compose no ar.
#   ./scripts/demo.sh
# Requer: curl. (jq é opcional, só para deixar a saída bonita.)
# Os textos dos JSON são ASCII de propósito: alguns terminais do Windows não enviam acentos em UTF-8.
set -euo pipefail

CATALOGO=${CATALOGO:-http://localhost:5001}
PEDIDOS=${PEDIDOS:-http://localhost:5002}
ESTOQUE=${ESTOQUE:-http://localhost:5003}
FATURAMENTO=${FATURAMENTO:-http://localhost:5004}
ANALISES=${ANALISES:-http://localhost:5005}

fmt() { if command -v jq >/dev/null 2>&1; then jq .; else cat; echo; fi; }
json() { curl -s -H 'Content-Type: application/json' "$@"; }
# Extrai um campo do JSON sem depender de jq (texto entre aspas ou número).
campo() {
  local j; j=$(cat)
  { echo "$j" | sed -n "s/.*\"$1\":\"\([^\"]*\)\".*/\1/p"
    echo "$j" | sed -n "s/.*\"$1\":\([0-9.-]*\).*/\1/p"; } | grep -v '^$' | head -1
}

esperar_ready() {
  for url in "$CATALOGO" "$PEDIDOS" "$ESTOQUE" "$FATURAMENTO" "$ANALISES"; do
    printf 'Aguardando %s/health/ready ' "$url"
    until curl -sf "$url/health/ready" >/dev/null; do printf '.'; sleep 2; done
    echo ' ok'
  done
}

status_pedido() {
  local id=$1 status=Pendente
  for _ in $(seq 1 30); do
    status=$(curl -s "$PEDIDOS/api/pedidos/$id" | campo status)
    [ "$status" != "Pendente" ] && break
    sleep 1
  done
  echo "$status"
}

esperar_ready
SUFIXO=$RANDOM

echo; echo "== 1. Importa produtos por planilha (assíncrono) =="
ARQUIVO="produtos-demo-$SUFIXO.csv" # caminho relativo: funciona também com o curl nativo do Windows
trap 'rm -f "$ARQUIVO"' EXIT
cat > "$ARQUIVO" <<EOF
codigo;descricao;categoria;unidade;preco_custo;preco_venda;perecivel;validade_dias
ARROZ-$SUFIXO;Arroz Tipo 1 5kg;Mercearia;UN;18,50;27,90;nao;
TOMATE-$SUFIXO;Tomate Italiano;Hortifruti;KG;4,20;8,99;sim;7
TV-$SUFIXO;Smart TV 75;Eletro;UN;9000;12000;nao;
INVALIDO-$SUFIXO;=HYPERLINK("http://malicioso");Mercearia;UN;1;2;nao;
EOF
LOCATION=$(curl -s -D - -o /dev/null -F "arquivo=@$ARQUIVO;type=text/csv" "$CATALOGO/api/produtos/importacoes" \
  | tr -d '\r' | sed -n 's/^[Ll]ocation: //p')
sleep 3
curl -s "$LOCATION" | fmt

echo; echo "== 2. Entrada de estoque (o Estoque criou os itens ao receber ProdutoCadastrado) =="
sleep 2
json -X POST "$ESTOQUE/api/estoque/entradas" -d "{\"produtoCodigo\":\"ARROZ-$SUFIXO\",\"quantidade\":50}" > /dev/null
json -X POST "$ESTOQUE/api/estoque/entradas" -d "{\"produtoCodigo\":\"TOMATE-$SUFIXO\",\"quantidade\":5,\"lote\":\"L1\",\"validade\":\"$(date -d '+3 days' +%F 2>/dev/null || date -v+3d +%F)\"}" > /dev/null
json -X POST "$ESTOQUE/api/estoque/entradas" -d "{\"produtoCodigo\":\"TOMATE-$SUFIXO\",\"quantidade\":20,\"lote\":\"L2\",\"validade\":\"$(date -d '+10 days' +%F 2>/dev/null || date -v+10d +%F)\"}" > /dev/null
json -X POST "$ESTOQUE/api/estoque/entradas" -d "{\"produtoCodigo\":\"TV-$SUFIXO\",\"quantidade\":10}" > /dev/null
curl -s "$ESTOQUE/api/estoque/TOMATE-$SUFIXO" | fmt

echo; echo "== 3. Caminho feliz: Pedido → Estoque reserva (FEFO) → Nota fiscal → Confirmado =="
P1=$(json -X POST "$PEDIDOS/api/pedidos" -d "{\"cliente\":\"Mercado Bom Preco\",\"itens\":[{\"produtoCodigo\":\"ARROZ-$SUFIXO\",\"quantidade\":2},{\"produtoCodigo\":\"TOMATE-$SUFIXO\",\"quantidade\":7.5}]}" | campo id)
echo "Pedido $P1 → $(status_pedido "$P1")"
curl -s "$FATURAMENTO/api/notas-fiscais/pedido/$P1" | fmt

echo; echo "== 4. Estoque insuficiente → Saga cancela o pedido =="
P2=$(json -X POST "$PEDIDOS/api/pedidos" -d "{\"cliente\":\"Hortifruti Central\",\"itens\":[{\"produtoCodigo\":\"ARROZ-$SUFIXO\",\"quantidade\":1000}]}" | campo id)
echo "Pedido $P2 → $(status_pedido "$P2")"
curl -s "$PEDIDOS/api/pedidos/$P2" | campo motivoCancelamento

echo; echo "== 5. Faturamento falha (valor > limite) → Saga compensa: libera o estoque e cancela =="
P3=$(json -X POST "$PEDIDOS/api/pedidos" -d "{\"cliente\":\"Atacado XPTO\",\"itens\":[{\"produtoCodigo\":\"TV-$SUFIXO\",\"quantidade\":5}]}" | campo id)
echo "Pedido $P3 → $(status_pedido "$P3")"
sleep 2
echo "Saldo da TV após a compensação (deve voltar a 10): $(curl -s "$ESTOQUE/api/estoque/TV-$SUFIXO" | campo saldoDisponivel)"

echo; echo "== 6. Análises (Cassandra) =="
sleep 2
curl -s "$ANALISES/api/analises/vendas/TOMATE-$SUFIXO" | fmt
curl -s "$ANALISES/api/analises/ruptura?janelaDias=1&leadTimeDias=3" | fmt

echo; echo "== 7. Simulação tributária: regime atual x reforma =="
for c in RegimeAtual Transicao2026 ReformaPlena; do
  printf '%-14s total de tributos: ' "$c"
  json -X POST "$FATURAMENTO/api/tributos/simulacoes" -d "{\"cenario\":\"$c\",\"itens\":[{\"produtoCodigo\":\"X\",\"valorTotal\":1000}]}" | campo valorTotalTributos
done
