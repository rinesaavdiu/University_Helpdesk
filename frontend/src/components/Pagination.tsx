import { Link } from 'react-router'

type PaginationProps = {
  page: number
  pageSize: number
  totalCount: number
  /** Returns the link target for a page, for example `?page=2`. */
  pageHref: (page: number) => string
}

export function Pagination({ page, pageSize, totalCount, pageHref }: PaginationProps) {
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize))
  if (pageCount <= 1) {
    return null
  }

  return (
    <nav className="pagination" aria-label="Pages">
      {page > 1 ? (
        <Link to={pageHref(page - 1)} className="button button--secondary" rel="prev">
          Previous
        </Link>
      ) : (
        <span className="button button--secondary" aria-disabled="true">
          Previous
        </span>
      )}
      <span className="pagination__position">
        Page {page} of {pageCount}
      </span>
      {page < pageCount ? (
        <Link to={pageHref(page + 1)} className="button button--secondary" rel="next">
          Next
        </Link>
      ) : (
        <span className="button button--secondary" aria-disabled="true">
          Next
        </span>
      )}
    </nav>
  )
}
