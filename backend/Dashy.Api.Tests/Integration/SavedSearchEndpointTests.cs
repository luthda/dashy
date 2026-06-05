using System.Net;
using System.Net.Http.Json;
using Dashy.Api.Application.Services;
using Dashy.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Dashy.Api.Tests.Integration;

public class SavedSearchEndpointTests : IAsyncLifetime
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
    public async Task GetSavedSearches_ReturnsEmptyList_WhenNone()
    {
        var response = await _client.GetAsync("/api/v1/saved-searches");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var searches = await response.Content.ReadFromJsonAsync<List<SavedSearchResponse>>();
        searches.Should().NotBeNull();
        searches.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateSavedSearch_ReturnsCreated()
    {
        var request = new { name = "WinFAP errors", query = "winfap error" };

        var response = await _client.PostAsJsonAsync("/api/v1/saved-searches", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var search = await response.Content.ReadFromJsonAsync<SavedSearchResponse>();
        search.Should().NotBeNull();
        search!.Name.Should().Be("WinFAP errors");
        search.Query.Should().Be("winfap error");
        search.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateSavedSearch_ReturnsValidationProblem_WhenNameEmpty()
    {
        var request = new { name = "", query = "anything" };

        var response = await _client.PostAsJsonAsync("/api/v1/saved-searches", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSavedSearch_AllowsEmptyQuery()
    {
        var request = new { name = "Everything" };

        var response = await _client.PostAsJsonAsync("/api/v1/saved-searches", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var search = await response.Content.ReadFromJsonAsync<SavedSearchResponse>();
        search!.Query.Should().Be("");
    }

    [Fact]
    public async Task DeleteSavedSearch_ReturnsNoContent_AndRemovesIt()
    {
        var create = await _client.PostAsJsonAsync("/api/v1/saved-searches", new { name = "To delete", query = "x" });
        var created = await create.Content.ReadFromJsonAsync<SavedSearchResponse>();

        var response = await _client.DeleteAsync($"/api/v1/saved-searches/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync("/api/v1/saved-searches");
        var searches = await getResponse.Content.ReadFromJsonAsync<List<SavedSearchResponse>>();
        searches.Should().NotContain(s => s.Id == created.Id);
    }

    [Fact]
    public async Task DeleteSavedSearch_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.DeleteAsync($"/api/v1/saved-searches/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSavedSearches_ReturnsNewestFirst()
    {
        await _client.PostAsJsonAsync("/api/v1/saved-searches", new { name = "First", query = "a" });
        await _client.PostAsJsonAsync("/api/v1/saved-searches", new { name = "Second", query = "b" });

        var response = await _client.GetAsync("/api/v1/saved-searches");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var searches = await response.Content.ReadFromJsonAsync<List<SavedSearchResponse>>();
        searches.Should().HaveCountGreaterThanOrEqualTo(2);
        searches!.Select(s => s.Name).Should().Contain(["First", "Second"]);
    }
}
