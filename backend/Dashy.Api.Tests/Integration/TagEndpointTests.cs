using System.Net;
using System.Net.Http.Json;
using Dashy.Api.Application.Services;
using Dashy.Api.Controllers;
using Dashy.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Dashy.Api.Tests.Integration;

public class TagEndpointTests : IAsyncLifetime
{
    private readonly DashyWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitialiseDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task GetTags_ReturnsEmptyList_WhenNoTags()
    {
        var response = await _client.GetAsync("/api/v1/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagResponse>>();
        tags.Should().NotBeNull();
        tags.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTag_ReturnsCreated()
    {
        var request = new
        {
            name = "Production Errors",
            color = "#ef4444",
            filters = new { terms = new[] { "prod" }, levels = new[] { "error" }, eventTypes = new[] { "exception" } }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>();
        tag.Should().NotBeNull();
        tag!.Name.Should().Be("Production Errors");
        tag.Color.Should().Be("#ef4444");
        tag.Id.Should().NotBeEmpty();
        tag.Filters.Terms.Should().Contain("prod");
        tag.Filters.Levels.Should().Contain("error");
        tag.Filters.EventTypes.Should().Contain("exception");
    }

    [Fact]
    public async Task CreateTag_ReturnsValidationProblem_WhenNameEmpty()
    {
        var request = new { name = "", color = "#000" };

        var response = await _client.PostAsJsonAsync("/api/v1/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTag_UsesDefaultColor_WhenColorOmitted()
    {
        var request = new { name = "Debug Only" };

        var response = await _client.PostAsJsonAsync("/api/v1/tags", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var tag = await response.Content.ReadFromJsonAsync<TagResponse>();
        tag!.Color.Should().Be("#6366f1");
    }

    [Fact]
    public async Task UpdateTag_ReturnsUpdatedTag()
    {
        var create = await _client.PostAsJsonAsync("/api/v1/tags", new { name = "Original" });
        var created = await create.Content.ReadFromJsonAsync<TagResponse>();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/tags/{created!.Id}",
            new { name = "Updated", color = "#22c55e" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TagResponse>();
        updated!.Name.Should().Be("Updated");
        updated.Color.Should().Be("#22c55e");
    }

    [Fact]
    public async Task UpdateTag_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/tags/{Guid.NewGuid()}",
            new { name = "Nope" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTag_ReturnsNoContent()
    {
        var create = await _client.PostAsJsonAsync("/api/v1/tags", new { name = "To Delete" });
        var created = await create.Content.ReadFromJsonAsync<TagResponse>();

        var response = await _client.DeleteAsync($"/api/v1/tags/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync("/api/v1/tags");
        var tags = await getResponse.Content.ReadFromJsonAsync<List<TagResponse>>();
        tags.Should().NotContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task DeleteTag_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.DeleteAsync($"/api/v1/tags/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTags_ReturnsPreviouslyCreatedTags()
    {
        await _client.PostAsJsonAsync("/api/v1/tags", new { name = "Tag A" });
        await _client.PostAsJsonAsync("/api/v1/tags", new { name = "Tag B" });

        var response = await _client.GetAsync("/api/v1/tags");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tags = await response.Content.ReadFromJsonAsync<List<TagResponse>>();
        tags.Should().HaveCountGreaterThanOrEqualTo(2);
        tags.Should().Contain(t => t.Name == "Tag A");
        tags.Should().Contain(t => t.Name == "Tag B");
    }
}
