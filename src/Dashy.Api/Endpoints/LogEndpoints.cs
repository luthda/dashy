using Dashy.Api.Infrastructure;
using Dashy.Api.Services;

namespace Dashy.Api.Endpoints;

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
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "sourceId", ["sourceId is required"] }
            });

        try
        {
            var entries = await svc.QueryAsync(request, ct);
            return Results.Ok(entries);
        }
        catch (SourceNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (AppInsightsQueryException ex) when (ex.StatusCode == 400)
        {
            return Results.BadRequest(new { error = $"Invalid query: {ex.Body}" });
        }
        catch (AppInsightsQueryException ex) when (ex.StatusCode == 403)
        {
            return Results.Problem(
                detail: "Check your Application ID and API key (Read telemetry permission required).",
                statusCode: 403);
        }
        catch (AppInsightsQueryException ex) when (ex.StatusCode == 429)
        {
            return Results.Problem(detail: "Rate limited by App Insights. Try again shortly.", statusCode: 429);
        }
        catch (LokiQueryException ex) when (ex.StatusCode == 400)
        {
            return Results.BadRequest(new { error = $"Invalid LogQL: {ex.Body}" });
        }
    }
}
