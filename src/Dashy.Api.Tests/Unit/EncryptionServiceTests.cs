using Dashy.Api.Infrastructure;
using Dashy.Api.Options;
using FluentAssertions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Dashy.Api.Tests.Unit;

public class EncryptionServiceTests
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
        var svc1 = MakeSvc(new byte[32]);
        var svc2 = MakeSvc(Enumerable.Repeat((byte)1, 32).ToArray());

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
