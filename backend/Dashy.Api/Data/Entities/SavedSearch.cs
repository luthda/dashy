namespace Dashy.Api.Data.Entities;

public class SavedSearch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;

    public string Query { get; set; } = "";

    /// <summary>JSON array of tag UUIDs.</summary>
    public string TagIds { get; set; } = "[]";

    /// <summary>JSON: { type: 'relative'|'absolute', value: string, from?: string, to?: string }</summary>
    public string TimeRange { get; set; } = """{"type":"relative","value":"1h"}""";

    public int? RefreshIntervalSeconds { get; set; }

    public bool IsBroken { get; set; }

    public DateTime CreatedAt { get; set; }
}
