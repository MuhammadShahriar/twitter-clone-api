namespace TwitterClone.Application.Common.Models;

/// <summary>
/// One page of a cursor-paginated read. <see cref="Items"/> is the page (already ordered);
/// <see cref="NextCursor"/> is the opaque token to pass back for the following page, or <c>null</c>
/// when there are no more items. Serialises as <c>{ "items": [...], "nextCursor": "..." }</c>.
/// </summary>
public record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
