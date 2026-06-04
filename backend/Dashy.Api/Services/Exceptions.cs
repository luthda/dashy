namespace Dashy.Api.Services;

public class SourceNotFoundException(Guid id)
    : Exception($"Source {id} was not found");

public class LogSourceQueryException(int statusCode, string body, Exception? inner = null)
    : Exception($"Log source query failed ({statusCode}): {body}", inner)
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}
