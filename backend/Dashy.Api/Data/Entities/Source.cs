namespace Dashy.Api.Data.Entities;

public class Source
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public SourceType Type { get; set; }

    /// <summary>
    /// Encrypted JSON blob — shape depends on Type.
    /// App Insights: { appId, apiKey }
    /// Loki: { baseUrl, orgId?, authToken? }
    /// </summary>
    public string Config { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}

public enum SourceType
{
    AppInsights,
    Loki,
}
