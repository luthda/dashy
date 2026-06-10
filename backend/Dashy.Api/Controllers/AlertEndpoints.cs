using Dashy.Api.Application.Exceptions;
using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Entities;

namespace Dashy.Api.Controllers;

public static class AlertEndpoints
{
    public static RouteGroupBuilder MapAlertEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/resolve", Resolve);
        group.MapGet("/{id:guid}/firings", GetFirings);
        group.MapGet("/stream", Stream);

        return group;
    }

    private static async Task Stream(HttpContext context, AlertSseService sse)
    {
        var response = context.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";

        var ct = context.RequestAborted;

        await response.WriteAsync("retry: 3000\n\n", ct);
        await response.Body.FlushAsync(ct);

        var clientId = sse.AddClient(response);
        try
        {
            // Heartbeat keeps proxies and load balancers from closing the idle
            // connection; broadcasts are pushed by the polling service in between.
            using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await heartbeat.WaitForNextTickAsync(ct))
            {
                if (!await sse.WritePingAsync(clientId, ct))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected.
        }
        finally
        {
            sse.RemoveClient(clientId);
        }
    }

    private static async Task<IResult> GetAll(AlertService svc, CancellationToken ct)
    {
        var alerts = await svc.GetAllAsync(ct);
        return Results.Ok(alerts.Select(ToResponse));
    }

    private static async Task<IResult> Create(CreateAlertRequest request, AlertService svc, CancellationToken ct)
    {
        var errors = Validate(request.Name, request.Query, request.TagId, request.Threshold);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            var alert = await svc.CreateAsync(request, ct);
            return Results.Created($"/api/v1/alerts/{alert.Id}", ToResponse(alert));
        }
        catch (SourceNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (TagNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (UnsupportedAlertSourceException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> Update(Guid id, UpdateAlertRequest request, AlertService svc, CancellationToken ct)
    {
        var errors = Validate(request.Name, request.Query, request.TagId, request.Threshold);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            var alert = await svc.UpdateAsync(id, request, ct);
            return alert is null
                ? Results.NotFound()
                : Results.Ok(ToResponse(alert));
        }
        catch (SourceNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (TagNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (UnsupportedAlertSourceException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<IResult> Delete(Guid id, AlertService svc, CancellationToken ct)
    {
        var deleted = await svc.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> Resolve(Guid id, AlertService svc, CancellationToken ct)
    {
        var alert = await svc.ResolveAsync(id, ct);
        return alert is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(alert));
    }

    private static async Task<IResult> GetFirings(Guid id, AlertService svc, CancellationToken ct)
    {
        var firings = await svc.GetFiringsAsync(id, ct);
        return firings is null
            ? Results.NotFound()
            : Results.Ok(firings.Select(f => new AlertFiringResponse(f.Id, f.FiredAt, f.ResultCount)));
    }

    private static Dictionary<string, string[]> Validate(
        string? name, string? query, Guid? tagId, int threshold)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required"];
        }

        if (string.IsNullOrWhiteSpace(query) && tagId is null)
        {
            errors["query"] = ["Either query or tagId is required"];
        }

        if (threshold < 1)
        {
            errors["threshold"] = ["Threshold must be at least 1"];
        }

        return errors;
    }

    private static AlertResponse ToResponse(Alert a) => new(
        a.Id,
        a.Name,
        a.SourceId,
        a.Source?.Name ?? "",
        a.Query,
        a.Threshold,
        a.Enabled,
        a.LastCheckedAt,
        a.Status,
        a.ResolvedAt,
        a.CreatedAt);
}

public record AlertResponse(
    Guid Id,
    string Name,
    Guid SourceId,
    string SourceName,
    string Query,
    int Threshold,
    bool Enabled,
    DateTime? LastCheckedAt,
    AlertStatus Status,
    DateTime? ResolvedAt,
    DateTime CreatedAt);

public record AlertFiringResponse(
    Guid Id,
    DateTime FiredAt,
    int ResultCount);
