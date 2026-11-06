import { ChevronDown, ChevronLeft } from 'lucide-react';
import { useState } from 'react';
import { cn } from '@/lib/utils';
import { Link } from '@/lib/router';
import { Badge } from '@/components/ui/Badge';

export type HrTreeNodeView = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  /** Active assignments under the unit (subtree data); omitted in the full-tree view. */
  activeAssignmentCount?: number;
  children: readonly HrTreeNodeView[];
};

type HrUnitTreeProps = {
  nodes: readonly HrTreeNodeView[];
  /** App-relative detail-route builder, e.g. `(id) => `/hr/org/units/${id}``. */
  detailHref: (id: string) => string;
  emptyLabel?: string;
};

const persianNumber = new Intl.NumberFormat('fa-IR');

function TreeBranch({ node, depth, detailHref }: { node: HrTreeNodeView; depth: number; detailHref: (id: string) => string }) {
  const [open, setOpen] = useState(depth < 1);
  const hasChildren = node.children.length > 0;

  return (
    <li className="min-w-0">
      <div
        className={cn(
          'group flex min-h-11 items-center gap-1.5 rounded-xl border border-transparent px-2 transition-colors hover:bg-cool/70',
          !node.isActive && 'opacity-70',
        )}
      >
        {hasChildren ? (
          <button
            type="button"
            onClick={() => setOpen((current) => !current)}
            aria-expanded={open}
            className="inline-flex size-7 shrink-0 items-center justify-center rounded-lg text-sub outline-none hover:bg-white focus-visible:ring-2 focus-visible:ring-gold"
          >
            <span className="sr-only">
              {open ? 'بستن زیرمجموعه‌های' : 'نمایش زیرمجموعه‌های'} {node.name}
            </span>
            {open ? <ChevronDown size={15} aria-hidden="true" /> : <ChevronLeft size={15} aria-hidden="true" />}
          </button>
        ) : (
          <span aria-hidden="true" className="size-7 shrink-0" />
        )}
        <Link
          to={detailHref(node.id)}
          className="flex min-w-0 flex-1 flex-col justify-center gap-0.5 rounded-lg px-1.5 py-1.5 outline-none focus-visible:ring-2 focus-visible:ring-gold"
        >
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <span className="text-sm font-semibold text-ink">{node.name}</span>
            <code dir="ltr" className="text-[11px] text-sub">
              {node.code}
            </code>
            {!node.isActive ? (
              <Badge tone="neutral" icon={null}>
                غیرفعال
              </Badge>
            ) : null}
            {typeof node.activeAssignmentCount === 'number' ? (
              <Badge tone={node.activeAssignmentCount > 0 ? 'info' : 'neutral'} icon={null}>
                {persianNumber.format(node.activeAssignmentCount)} انتساب فعال
              </Badge>
            ) : null}
          </span>
        </Link>
      </div>
      {hasChildren && open ? (
        <ul className="ms-4 border-s border-line ps-2">
          {node.children.map((child) => (
            <TreeBranch key={child.id} node={child} depth={depth + 1} detailHref={detailHref} />
          ))}
        </ul>
      ) : null}
    </li>
  );
}

/**
 * The organizational hierarchy as a keyboard-accessible nested list. Nodes link to their detail
 * page; expanders are real buttons with `aria-expanded`. No visualization library: the structures
 * are small (the backend bounds the depth at 32) and a native list stays RTL-correct and fast.
 */
export function HrUnitTree({ nodes, detailHref, emptyLabel = 'واحد سازمانی ثبت نشده است' }: HrUnitTreeProps) {
  if (nodes.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-line bg-white p-6 text-center">
        <p className="font-semibold text-ink">{emptyLabel}</p>
      </div>
    );
  }
  return (
    <ul className="space-y-0.5">
      {nodes.map((node) => (
        <TreeBranch key={node.id} node={node} depth={0} detailHref={detailHref} />
      ))}
    </ul>
  );
}
