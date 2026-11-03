import { Fragment } from 'react';
import { ChevronLeft } from 'lucide-react';
import { Link } from '@/lib/router';

export type BreadcrumbItem = { label: string; /** App-relative path; omit for the current page. */ to?: string };

/** The path to the current page. The last item is the current page (`aria-current="page"`). */
export function Breadcrumb({ items }: { items: readonly BreadcrumbItem[] }) {
  return (
    <nav aria-label="مسیر صفحه">
      <ol className="flex flex-wrap items-center gap-1.5 text-sm text-sub">
        {items.map((item, index) => {
          const last = index === items.length - 1;
          return (
            <Fragment key={`${index}-${item.label}`}>
              <li>
                {item.to && !last ? (
                  <Link to={item.to} className="rounded font-medium text-teal-deep underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-gold">
                    {item.label}
                  </Link>
                ) : (
                  <span aria-current={last ? 'page' : undefined} className={last ? 'font-semibold text-ink' : undefined}>
                    {item.label}
                  </span>
                )}
              </li>
              {last ? null : (
                <li aria-hidden="true" className="text-muted">
                  <ChevronLeft size={14} />
                </li>
              )}
            </Fragment>
          );
        })}
      </ol>
    </nav>
  );
}
