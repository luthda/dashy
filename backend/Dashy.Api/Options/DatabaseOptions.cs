namespace Dashy.Api.Options;

public class DatabaseOptions
{
    public const string Section = "Database";

    public string ConnectionString { get; init; } = "Data Source=/data/dashy.db";
}
