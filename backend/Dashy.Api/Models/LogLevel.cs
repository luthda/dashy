using System.Text.Json.Serialization;

namespace Dashy.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter<LogLevel>))]
public enum LogLevel
{
    [JsonStringEnumMemberName("trace")]
    Trace,

    [JsonStringEnumMemberName("debug")]
    Debug,

    [JsonStringEnumMemberName("info")]
    Info,

    [JsonStringEnumMemberName("warn")]
    Warn,

    [JsonStringEnumMemberName("error")]
    Error,
}
