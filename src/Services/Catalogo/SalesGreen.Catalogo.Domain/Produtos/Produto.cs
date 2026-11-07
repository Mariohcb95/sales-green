using SalesGreen.SharedKernel;
using SalesGreen.SharedKernel.ValueObjects;

namespace SalesGreen.Catalogo.Domain.Produtos;

public enum UnidadeMedida
{
    UN = 1,
    KG = 2,
    G = 3,
    L = 4,
    ML = 5,
    CX = 6,
    PCT = 7,
    DZ = 8
}

/// <summary>Dados editáveis do produto (o código é imutável, por isso fica de fora).</summary>
public sealed record DadosProduto(
    string Descricao,
    string Categoria,
    UnidadeMedida Unidade,
    Dinheiro PrecoCusto,
    Dinheiro PrecoVenda,
    bool Perecivel,
    int? PrazoValidadeDias);

/// <summary>Agregado Produto. Todas as invariantes são verificadas aqui, nunca no controller.</summary>
public sealed class Produto : AggregateRoot
{
    public const int PrazoValidadeMaximoDias = 3650;

    public CodigoProduto Codigo { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public string Categoria { get; private set; } = string.Empty;
    public UnidadeMedida Unidade { get; private set; }
    public Dinheiro PrecoCusto { get; private set; }
    public Dinheiro PrecoVenda { get; private set; }
    public bool Perecivel { get; private set; }
    public int? PrazoValidadeDias { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    private Produto() { } // EF Core

    public static Produto Criar(CodigoProduto codigo, DadosProduto dados, DateTime agora)
    {
        var produto = new Produto { Codigo = codigo, CriadoEm = agora };
        produto.Aplicar(dados, agora);
        produto.Raise(new ProdutoCadastradoDomainEvent(produto));
        return produto;
    }

    public void Atualizar(DadosProduto dados, DateTime agora) => Aplicar(dados, agora);

    /// <summary>Margem bruta em percentual sobre o preço de venda.</summary>
    public decimal MargemPercentual =>
        PrecoVenda.Valor == 0 ? 0 : Math.Round((PrecoVenda.Valor - PrecoCusto.Valor) / PrecoVenda.Valor * 100, 2);

    private void Aplicar(DadosProduto dados, DateTime agora)
    {
        Descricao = TextoObrigatorio(dados.Descricao, "Descrição", 3, 120);
        Categoria = TextoObrigatorio(dados.Categoria, "Categoria", 2, 60);

        if (!Enum.IsDefined(dados.Unidade))
            throw new DomainException("Unidade de medida inválida.");
        if (dados.PrecoVenda <= Dinheiro.Zero)
            throw new DomainException("Preço de venda deve ser maior que zero.");

        if (dados.Perecivel)
        {
            if (dados.PrazoValidadeDias is null or <= 0 or > PrazoValidadeMaximoDias)
                throw new DomainException($"Produto perecível exige prazo de validade entre 1 e {PrazoValidadeMaximoDias} dias.");
        }
        else if (dados.PrazoValidadeDias is not null)
        {
            throw new DomainException("Prazo de validade só se aplica a produtos perecíveis.");
        }

        Unidade = dados.Unidade;
        PrecoCusto = dados.PrecoCusto;
        PrecoVenda = dados.PrecoVenda;
        Perecivel = dados.Perecivel;
        PrazoValidadeDias = dados.PrazoValidadeDias;
        AtualizadoEm = agora;
    }

    private static string TextoObrigatorio(string? valor, string campo, int minimo, int maximo)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length < minimo || texto.Length > maximo)
            throw new DomainException($"{campo} deve ter entre {minimo} e {maximo} caracteres.");
        // Defesa contra CSV/Formula Injection: o texto pode voltar para uma planilha em relatórios.
        if (texto[0] is '=' or '+' or '-' or '@')
            throw new DomainException($"{campo} não pode começar com '=', '+', '-' ou '@'.");
        return texto;
    }
}

public sealed record ProdutoCadastradoDomainEvent(Produto Produto) : IDomainEvent;
