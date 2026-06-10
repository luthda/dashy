namespace Dashy.Api.Application.Exceptions;

public class SourceNotFoundException(Guid id)
    : Exception($"Source {id} was not found");

public class LogSourceQueryException(int statusCode, string body, Exception? inner = null)
    : Exception($"Log source query failed ({statusCode}): {body}", inner)
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

public class TagNotFoundException(Guid id)
    : Exception($"Tag {id} was not found");

public class UnsupportedAlertSourceException(string sourceType)
    : Exception($"Alerts are not supported for source type '{sourceType}'");
