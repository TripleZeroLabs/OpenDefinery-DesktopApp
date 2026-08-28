using System;

namespace OpenDefinery
{
    /// <summary>
    /// Page state for a paginated list endpoint.
    ///
    /// The v1 API pages by number and size (?page=&amp;page_size=) and returns an absolute
    /// `count`. That replaced Drupal's offset-based scheme, whose `pager` object reported
    /// totals relative to the request - which is why this class used to reach back into
    /// MainWindow to carry the real totals forward between calls. It no longer needs to,
    /// and no longer lives in the UI project.
    /// </summary>
    public class Pager
    {
        /// <summary>Zero-based page index, as the pager UI counts.</summary>
        public int CurrentPage { get; set; }

        /// <summary>One-based page number, as ?page= counts. Pass this to the API.</summary>
        public int Page => CurrentPage + 1;

        public int ItemsPerPage { get; set; } = 100;

        /// <summary>
        /// Item offset for the current page. The API is now page-based (see <see cref="Page"/>);
        /// this is kept for legacy offset-based call sites and maps to/from <see cref="CurrentPage"/>.
        /// </summary>
        public int Offset
        {
            get => CurrentPage * ItemsPerPage;
            set => CurrentPage = ItemsPerPage > 0 ? value / ItemsPerPage : 0;
        }

        public int TotalItems { get; set; }

        public int TotalPages { get; set; }

        public bool IsFirstPage { get; set; }

        public bool IsLastPage { get; set; }

        /// <summary>
        /// Record the totals from a response.
        ///
        /// `resetTotals` is kept for the call sites that pass it, but it no longer changes
        /// anything: `count` is the size of the whole result set, not of this page, so there
        /// is nothing to preserve across calls.
        /// </summary>
        public void Update<T>(Paginated<T> response, bool resetTotals = true)
        {
            if (response == null) return;

            TotalItems = response.Count;
            TotalPages = ItemsPerPage > 0
                ? (int)Math.Ceiling(response.Count / (double)ItemsPerPage)
                : 0;

            IsFirstPage = string.IsNullOrEmpty(response.Previous);
            IsLastPage = string.IsNullOrEmpty(response.Next);
        }

        public static Pager Reset(int itemsPerPage = 100)
        {
            return new Pager
            {
                CurrentPage = 0,
                ItemsPerPage = itemsPerPage,
                TotalItems = 0,
                TotalPages = 0,
                IsFirstPage = true,
                IsLastPage = false
            };
        }
    }
}
