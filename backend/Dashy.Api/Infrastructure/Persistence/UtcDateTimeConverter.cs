using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dashy.Api.Infrastructure.Persistence;

/// <summary>
/// SQLite stores DateTime as text without an offset, so EF Core reads values back with
/// Kind=Unspecified. All DateTimes in this app are written as UTC (DateTime.UtcNow), so this
/// converter restamps Kind=Utc on read — without it, System.Text.Json serializes the value
/// without a "Z" suffix and browsers misinterpret it as local time.
/// </summary>
public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
