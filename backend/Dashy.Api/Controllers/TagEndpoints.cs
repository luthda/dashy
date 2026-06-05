using System.Text.Json;
using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Entities;

namespace Dashy.Api.Controllers;

public static class TagEndpoints
{
    public static RouteGroupBuilder MapTagEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return group;
    }

    private static async Task<IResult> GetAll(TagService svc, CancellationToken ct)
    {
        var tags = await svc.GetAllAsync(ct);
        return Results.Ok(tags.Select(ToResponse));
    }

    private static async Task<IResult> Create(CreateTagRequest request, TagService svc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "name", ["Name is required"] }
            });
        }

        var tag = await svc.CreateAsync(request, ct);
        return Results.Created($"/api/v1/tags/{tag.Id}", ToResponse(tag));
    }

    private static async Task<IResult> Update(Guid id, UpdateTagRequest request, TagService svc, CancellationToken ct)
    {
        var tag = await svc.UpdateAsync(id, request, ct);
        return tag is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(tag));
    }

    private static async Task<IResult> Delete(Guid id, TagService svc, CancellationToken ct)
    {
        var deleted = await svc.DeleteAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static TagResponse ToResponse(Tag t) => new(
        Id: t.Id,
        Name: t.Name,
        Color: t.Color,
        Filters: JsonSerializer.Deserialize<TagFiltersDto>(t.Filters) ?? new TagFiltersDto(),
        CreatedAt: t.CreatedAt
    );
}

public record TagResponse(
    Guid Id,
    string Name,
    string Color,
    TagFiltersDto Filters,
    DateTime CreatedAt);
