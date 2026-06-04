namespace Dashy.Api.Data.Entities;

public class AlertFiring
{
    public Guid Id { get; set; }

    public Guid AlertId { get; set; }
    public Alert Alert { get; set; } = null!;

    public DateTime FiredAt { get; set; }
    public int ResultCount { get; set; }
}
