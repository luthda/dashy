namespace Dashy.Api.Options;

public class EncryptionOptions
{
    public const string Section = "Encryption";

    /// <summary>
    /// Base64-encoded 32-byte AES-256 key. Set via ENCRYPTION_KEY environment variable.
    /// </summary>
    public string Key { get; set; } = "";
}
