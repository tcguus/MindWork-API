namespace MindWork.Api.Models.Responses;

/// <summary>
/// Representa um link HATEOAS em uma resposta.
/// Ex.: self, next, previous, create, update, delete.
/// </summary>
public class Link
{
    public Link(string href, string rel, string method)
    {
        Href = href;
        Rel = rel;
        Method = method;
    }

    /// <summary>
    /// URL do recurso.
    /// </summary>
    public string Href { get; set; }

    /// <summary>
    /// Relação do link com o recurso.
    /// Exemplos: "self", "next", "previous", "update", "delete".
    /// </summary>
    public string Rel { get; set; }

    /// <summary>
    /// Verbo HTTP usado no link (GET, POST, PUT, DELETE).
    /// </summary>
    public string Method { get; set; }
}

/// <summary>
/// Resposta paginada com suporte a HATEOAS.
/// Será usada em listagens para cumprir os requisitos de:
/// - Paginação
/// - HATEOAS
/// na disciplina de .NET.
/// </summary>
public class PagedResponse<T>
{
    public PagedResponse(
        IEnumerable<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        Items = items.ToList();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Itens da página atual.
    /// </summary>
    public List<T> Items { get; }

    /// <summary>
    /// Número da página atual (1-based).
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Tamanho da página (quantidade de itens).
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Quantidade total de registros na base.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Quantidade total de páginas.
    /// </summary>
    public int TotalPages =>
        (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;

    /// <summary>
    /// Links HATEOAS relacionados à coleção.
    /// Ex.: self, next, previous.
    /// </summary>
    public List<Link> Links { get; } = new();

    /// <summary>
    /// Adiciona um link HATEOAS à resposta.
    /// </summary>
    public void AddLink(string href, string rel, string method)
    {
        Links.Add(new Link(href, rel, method));
    }
}
