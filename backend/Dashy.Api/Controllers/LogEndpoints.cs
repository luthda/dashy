using Dashy.Api.Application.Exceptions;
using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Models;

namespace Dashy.Api.Controllers;

public static class LogEndpoints
{
    public static RouteGroupBuilder MapLogEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/query", Query);
        return group;
    }

    // POST /api/v1/logs/query
    private static async Task<IResult> Query(
        LogQueryRequest request,
        LogQueryService svc,
        CancellationToken ct)
    {
        if (request.SourceId == Guid.Empty)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "sourceId", ["sourceId is required"] }
            });
        }

        try
        {
            var result = await svc.QueryAsync(request, ct);
            return Results.Ok(new LogQueryResponse(result.Entries, result.HasMore));
        }
        catch (SourceNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (LogSourceQueryException ex) when (ex.StatusCode == 400)
        {
            return Results.BadRequest(new { error = $"Invalid query: {ex.Body}" });
        }
        catch (LogSourceQueryException ex) when (ex.StatusCode == 403)
        {
            return Results.Problem(
                detail: "Check your Application ID and API key (Read telemetry permission required).",
                statusCode: 403);
        }
        catch (LogSourceQueryException ex) when (ex.StatusCode == 429)
        {
            return Results.Problem(detail: "Rate limited. Try again shortly.", statusCode: 429);
        }
    }
}

public record LogQueryResponse(List<LogEntry> Entries, bool HasMore);
