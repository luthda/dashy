using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Entities;

namespace Dashy.Api.Controllers;

public static class SavedSearchEndpoints
{
    public static RouteGroupBuilder MapSavedSearchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapDelete("/{id:guid}", Delete);

        return group;
    }

    private static async Task<IResult> GetAll(SavedSearchService svc, CancellationToken ct)
    {
        var searches = await svc.GetAllAsync(ct);
        return Results.Ok(searches.Select(ToResponse));
    }

    private static async Task<IResult> Create(CreateSavedSearchRequest request, SavedSearchService svc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "name", ["Name is required"] }
            });
        }

        var search = await svc.CreateAsync(request, ct);
        return Results.Created($"/api/v1/saved-searches/{search.Id}", ToResponse(search));
    }

    private static async Task<IResult> Delete(Guid id, SavedSearchService svc, CancellationToken ct)
    {
        var deleted = await svc.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static SavedSearchResponse ToResponse(SavedSearch s) =>
        new(s.Id, s.Name, s.Query, s.CreatedAt);
}
