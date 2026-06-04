using System.Security.Cryptography;
using System.Text;
using Dashy.Api.Options;
using Microsoft.Extensions.Options;

namespace Dashy.Api.Infrastructure;

public interface IEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

/// <summary>
/// AES-256-GCM encryption for sensitive values stored in SQLite.
/// Output format: Base64(nonce [12 bytes] + tag [16 bytes] + ciphertext).
/// </summary>
public sealed class AesGcmEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesGcmEncryptionService(IOptions<EncryptionOptions> options)
    {
        var raw = options.Value.Key;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "ENCRYPTION_KEY is not set. Set the Encryption:Key config value or the ENCRYPTION__KEY environment variable.");

        _key = Convert.FromBase64String(raw);
        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"ENCRYPTION_KEY must be a 32-byte Base64 value (got {_key.Length} bytes).");
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];   // 12 bytes
        var tag   = new byte[AesGcm.TagByteSizes.MaxSize];     // 16 bytes
        var ciphertext = new byte[plaintextBytes.Length];

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Pack: nonce (12) + tag (16) + ciphertext
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce,       0, result, 0,                          nonce.Length);
        Buffer.BlockCopy(tag,         0, result, nonce.Length,               tag.Length);
        Buffer.BlockCopy(ciphertext,  0, result, nonce.Length + tag.Length,  ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        var raw = Convert.FromBase64String(ciphertext);

        const int nonceLen = 12;
        const int tagLen   = 16;

        if (raw.Length < nonceLen + tagLen)
            throw new CryptographicException("Ciphertext is too short.");

        var nonce      = raw[..nonceLen];
        var tag        = raw[nonceLen..(nonceLen + tagLen)];
        var encrypted  = raw[(nonceLen + tagLen)..];
        var plaintext  = new byte[encrypted.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
