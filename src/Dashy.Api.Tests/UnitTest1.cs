using Dashy.Api.Infrastructure;
using Dashy.Api.Options;
using Dashy.Api.Services;
using Dashy.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Dashy.Api.Tests;

// ── Smoke test ─────────────────────────────────────────────────────────────────

public class SmokeTests : IAsyncLifetime
{
    private readonly DashyWebApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.InitialiseDatabaseAsync();
    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Healthz_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}

// ── Encryption tests ───────────────────────────────────────────────────────────

public class AesGcmEncryptionServiceTests
{
    private static IEncryptionService MakeSvc(byte[]? key = null)
    {
        var k = key ?? new byte[32];
        var options = MsOptions.Create(new EncryptionOptions
        {
            Key = Convert.ToBase64String(k)
        });
        return new AesGcmEncryptionService(options);
    }

    [Fact]
    public void RoundTrip_PlaintextRestored()
    {
        var svc = MakeSvc();
        const string plaintext = """{"appId":"app123","apiKey":"secret-key"}""";

        var encrypted = svc.Encrypt(plaintext);
        var decrypted = svc.Decrypt(encrypted);

        decrypted.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachCall()
    {
        var svc = MakeSvc();
        const string plaintext = "same-input";

        var c1 = svc.Encrypt(plaintext);
        var c2 = svc.Encrypt(plaintext);

        c1.Should().NotBe(c2, "random nonce ensures different output each call");
    }

    [Fact]
    public void Decrypt_ThrowsWithWrongKey()
    {
        var svc1 = MakeSvc(new byte[32]);     // all zeros
        var svc2 = MakeSvc(Enumerable.Repeat((byte)1, 32).ToArray()); // all ones

        var encrypted = svc1.Encrypt("secret");

        var act = () => svc2.Decrypt(encrypted);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Constructor_ThrowsWhenKeyNotSet()
    {
        var options = MsOptions.Create(new EncryptionOptions { Key = "" });
        var act = () => new AesGcmEncryptionService(options);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ENCRYPTION_KEY*");
    }
}

// ── KQL builder tests ──────────────────────────────────────────────────────────

public class KqlBuilderTests
{
    [Fact]
    public void BuildKql_FreeTextOnly_ContainsContainsClause()
    {
        var kql = LogQueryService.BuildKql("winfap", null, TagFilters.Empty, 100);
        kql.Should().Contain("message contains \"winfap\"");
    }

    [Fact]
    public void BuildKql_EventTypeFilter_AddedToWhere()
    {
        var tags = new TagFilters([], [], ["Exception"]);
        var kql = LogQueryService.BuildKql(null, null, tags, 100);
        kql.Should().Contain("customDimensions[\"EventType\"] == \"Exception\"");
    }

    [Fact]
    public void BuildKql_LevelFilter_MapsToSeverityLevel()
    {
        var tags = new TagFilters([], ["error"], []);
        var kql = LogQueryService.BuildKql(null, null, tags, 100);
        kql.Should().Contain("severityLevel in (3)");
    }

    [Fact]
    public void BuildKql_Combined_AllClausesPresent()
    {
        var tags = new TagFilters([], ["error"], ["Exception"]);
        var kql = LogQueryService.BuildKql("winfap", null, tags, 500);
        kql.Should().Contain("message contains \"winfap\"");
        kql.Should().Contain("severityLevel in (3)");
        kql.Should().Contain("customDimensions[\"EventType\"] == \"Exception\"");
        kql.Should().Contain("limit 500");
    }
}
