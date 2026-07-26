using System.Collections.Generic;

namespace MVC_EF_Start_8.Models.Api
{
    /// <summary>
    /// Standard paged-list envelope so every list endpoint in the API has
    /// the same shape (items + enough info for a client to page through
    /// the rest) instead of just returning a bare array and leaving
    /// pagination as an afterthought.
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
