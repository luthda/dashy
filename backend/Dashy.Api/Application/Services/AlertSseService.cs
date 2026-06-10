using System.Collections.Concurrent;
using System.Text.Json;
using Dashy.Api.Application.Abstractions;

namespace Dashy.Api.Application.Services;

/// <summary>
/// Singleton registry of connected SSE clients. The stream endpoint registers a
/// client per connection; the polling service broadcasts firing events to all of
/// them. Writes to a single response are serialized through a per-client lock so
/// heartbeat pings and broadcasts never interleave.
/// </summary>
public class AlertSseService(ILogger<AlertSseService> logger) : IAlertBroadcaster
{
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();

    public int ClientCount => _clients.Count;

    public string AddClient(HttpResponse response)
    {
        var id = Guid.NewGuid().ToString();
        _clients.TryAdd(id, new SseClient(response, new SemaphoreSlim(1, 1)));
        logger.LogInformation("SSE client connected ({Id}), total={Count}", id, _clients.Count);
        return id;
    }

    public void RemoveClient(string id)
    {
        if (_clients.TryRemove(id, out _))
        {
            logger.LogInformation("SSE client disconnected ({Id}), total={Count}", id, _clients.Count);
        }
    }

    /// <summary>Writes a comment-line heartbeat. Returns false when the client is gone.</summary>
    public async Task<bool> WritePingAsync(string id, CancellationToken ct)
    {
        if (!_clients.TryGetValue(id, out var client))
        {
            return false;
        }

        return await TryWriteAsync(id, client, ": ping\n\n", ct);
    }

    public async Task BroadcastAsync(AlertFiredEvent evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, JsonSerializerOptions.Web);
        var message = $"event: alert-fired\ndata: {json}\n\n";

        foreach (var (id, client) in _clients)
        {
            await TryWriteAsync(id, client, message, ct);
        }
    }

    private async Task<bool> TryWriteAsync(string id, SseClient client, string message, CancellationToken ct)
    {
        await client.WriteLock.WaitAsync(ct);
        try
        {
            await client.Response.WriteAsync(message, ct);
            await client.Response.Body.FlushAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "SSE write failed for client {Id}, removing", id);
            RemoveClient(id);
            return false;
        }
        finally
        {
            client.WriteLock.Release();
        }
    }

    private sealed record SseClient(HttpResponse Response, SemaphoreSlim WriteLock);
}
