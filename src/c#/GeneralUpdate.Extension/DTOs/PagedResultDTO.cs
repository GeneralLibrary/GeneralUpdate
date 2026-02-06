using System.Collections.Generic;
using System.Linq;

namespace GeneralUpdate.Extension.DTOs
{
    /// <summary>
    /// Paginated result data transfer object
    /// </summary>
    /// <typeparam name="T">Item type</typeparam>
    public class PagedResultDTO<T>
    {
        /// <summary>
        /// Current page number
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of items
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Items in current page
        /// </summary>
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        public bool HasPrevious => PageNumber > 1;

        /// <summary>
        /// Whether there is a next page
        /// </summary>
        public bool HasNext => PageNumber < TotalPages;
    }
}
