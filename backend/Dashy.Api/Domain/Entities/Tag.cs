namespace Dashy.Api.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#6366f1";

    /// <summary>
    /// JSON: { terms: string[], levels: string[], eventTypes: string[] }
    /// Stored as a plain JSON text column.
    /// </summary>
    public string Filters { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
}
