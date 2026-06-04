using Dashy.Api.Data.Entities;
using Dashy.Api.Services;

namespace Dashy.Api.Endpoints;

public static class SourceEndpoints
{
    public static RouteGroupBuilder MapSourceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/",          GetAll);
        group.MapPost("/",         Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/test", Test);

        return group;
    }

    // GET /api/v1/sources
    private static async Task<IResult> GetAll(SourceService svc, CancellationToken ct)
    {
        var sources = await svc.GetAllAsync(ct);
        return Results.Ok(sources.Select(ToResponse));
    }

    // POST /api/v1/sources
    private static async Task<IResult> Create(CreateSourceRequest request, SourceService svc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "name", ["Name is required"] }
            });

        if (request.Config is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "config", ["Config is required"] }
            });

        var source = await svc.CreateAsync(request, ct);
        return Results.Created($"/api/v1/sources/{source.Id}", ToResponse(source));
    }

    // PUT /api/v1/sources/{id}
    private static async Task<IResult> Update(Guid id, UpdateSourceRequest request, SourceService svc, CancellationToken ct)
    {
        var source = await svc.UpdateAsync(id, request, ct);
        return source is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(source));
    }

    // DELETE /api/v1/sources/{id}
    private static async Task<IResult> Delete(Guid id, SourceService svc, CancellationToken ct)
    {
        var deleted = await svc.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    // POST /api/v1/sources/{id}/test
    private static async Task<IResult> Test(Guid id, SourceService svc, CancellationToken ct)
    {
        var result = await svc.TestConnectionAsync(id, ct);
        return Results.Ok(result);
    }

    // Mask credentials in responses
    private static SourceResponse ToResponse(Source s) => new(
        Id:        s.Id,
        Name:      s.Name,
        Type:      s.Type.ToString(),
        CreatedAt: s.CreatedAt
    );
}

public record SourceResponse(
    Guid Id,
    string Name,
    string Type,
    DateTime CreatedAt);
