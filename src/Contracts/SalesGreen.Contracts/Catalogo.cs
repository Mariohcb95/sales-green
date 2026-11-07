namespace SalesGreen.Contracts.Catalogo;

/// <summary>Publicado quando um produto passa a existir no catálogo (cadastro manual ou importação).</summary>
public record ProdutoCadastrado(Guid ProdutoId, string Codigo, string Descricao, string UnidadeMedida, bool Perecivel, int? PrazoValidadeDias);

/// <summary>Mensagem interna do Catálogo: dispara o processamento assíncrono de uma planilha.</summary>
public record ImportacaoProdutosSolicitada(Guid ImportacaoId);
