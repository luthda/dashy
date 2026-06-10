namespace Dashy.Api.Domain.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;

    public string Query { get; set; } = "";
    public int Threshold { get; set; } = 1;
    public bool Enabled { get; set; } = true;

    public DateTime? LastCheckedAt { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Ok;
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<AlertFiring> Firings { get; set; } = [];
}

public enum AlertStatus
{
    Ok,
    Firing,
    Error,
}
