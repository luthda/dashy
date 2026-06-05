namespace Dashy.Api.Domain.Entities;

/// <summary>
/// A named full-text search string the user can recall later (Phase 4, issue #11).
/// Only the search string is persisted — no source link, tags, time range, or refresh interval.
/// </summary>
public class SavedSearch
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>The full-text search string.</summary>
    public string Query { get; set; } = "";

    public DateTime CreatedAt { get; set; }
}
