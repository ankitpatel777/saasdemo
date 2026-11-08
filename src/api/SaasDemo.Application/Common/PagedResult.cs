namespace SaasDemo.Application.Common;

/// <summary>One page of results. <see cref="Page"/> is 1-based.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
