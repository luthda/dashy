namespace Dashy.Api.Application.Abstractions;

public interface IAlertBroadcaster
{
    Task BroadcastAsync(AlertFiredEvent evt, CancellationToken ct);
}

public record AlertFiredEvent(
    Guid AlertId,
    string AlertName,
    int ResultCount,
    DateTime FiredAt);
