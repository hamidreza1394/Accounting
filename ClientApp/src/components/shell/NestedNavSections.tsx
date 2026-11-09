import { useEffect, useState, type ReactNode } from 'react';
import { ChevronDown, type LucideIcon } from 'lucide-react';
import { Link } from '@/lib/router';

function isActivePath(pathname: string, to: string): boolean {
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}

export type NestedNavLeaf = {
  label: string;
  icon: LucideIcon;
  tone: string;
  to?: string;
  section?: string;
};

type ToneClass = Record<string, string>;

function NavIcon({
  icon: Icon,
  tone,
  toneClass,
}: {
  icon: LucideIcon;
  tone: string;
  toneClass: ToneClass;
}) {
  return (
    <span className={'faranou-nav-icon ' + (toneClass[tone] ?? '') + ' faranou-nav-icon--sm'} aria-hidden="true">
      <Icon size={14} strokeWidth={2.2} />
    </span>
  );
}

/**
 * Renders leaves of a sidebar group with collapsible Persian section parents
 * (e.g. «ساختار سازمانی و کارکنان» owning کارکنان / واحدها / سمت‌ها).
 */
export function NestedNavSections({
  groupLabel,
  leaves,
  pathname,
  onNavigate,
  toneClass,
  DisabledLeaf,
}: {
  groupLabel: string;
  leaves: readonly NestedNavLeaf[];
  pathname: string;
  onNavigate: () => void;
  toneClass: ToneClass;
  DisabledLeaf: (props: { leaf: NestedNavLeaf; nested?: boolean }) => ReactNode;
}) {
  const [openSections, setOpenSections] = useState<Record<string, boolean>>({});

  useEffect(() => {
    const sectionsToOpen: Record<string, boolean> = {};
    let currentSection: string | undefined;
    for (const leaf of leaves) {
      if (leaf.section) currentSection = leaf.section;
      if (currentSection && leaf.to && isActivePath(pathname, leaf.to)) {
        sectionsToOpen[`${groupLabel}::${currentSection}`] = true;
      }
    }
    if (Object.keys(sectionsToOpen).length === 0) return;
    setOpenSections((previous) => ({ ...previous, ...sectionsToOpen }));
  }, [pathname, groupLabel, leaves]);

  const nodes: ReactNode[] = [];
  let sectionName: string | undefined;
  let sectionLeaves: NestedNavLeaf[] = [];

  const flushSection = () => {
    if (!sectionName) return;
    const key = `${groupLabel}::${sectionName}`;
    const sectionOpen =
      openSections[key] !== false ||
      sectionLeaves.some((l) => l.to !== undefined && isActivePath(pathname, l.to));
    const sectionId = `nav-section-${key.replace(/[^\w\u0600-\u06FF]+/g, '-')}`;
    const name = sectionName;
    const batch = sectionLeaves;
    nodes.push(
      <li key={key} className="space-y-1">
        <button
          type="button"
          className="faranou-nav-section-toggle"
          aria-expanded={sectionOpen}
          aria-controls={sectionId}
          onClick={() =>
            setOpenSections((previous) => {
              const currentlyOpen =
                previous[key] !== false ||
                batch.some((l) => l.to !== undefined && isActivePath(pathname, l.to));
              return { ...previous, [key]: !currentlyOpen };
            })
          }
        >
          <span>{name}</span>
          <ChevronDown size={14} className="faranou-nav-section-chevron" aria-hidden="true" />
        </button>
        {sectionOpen ? (
          <ul id={sectionId} className="space-y-1 faranou-nav-leaf--nested">
            {batch.map((leaf) =>
              leaf.to ? (
                <li key={leaf.label}>
                  <Link
                    to={leaf.to}
                    onClick={onNavigate}
                    aria-current={isActivePath(pathname, leaf.to) ? 'page' : undefined}
                    className={
                      'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' +
                      (isActivePath(pathname, leaf.to)
                        ? 'bg-pick/20 text-white ring-1 ring-pick/30'
                        : 'text-on-navy/80 hover:bg-white/10 hover:text-white')
                    }
                  >
                    <NavIcon icon={leaf.icon} tone={leaf.tone} toneClass={toneClass} />
                    <span className="min-w-0 flex-1">{leaf.label}</span>
                  </Link>
                </li>
              ) : (
                <li key={leaf.label}>{DisabledLeaf({ leaf, nested: true })}</li>
              ),
            )}
          </ul>
        ) : null}
      </li>,
    );
    sectionName = undefined;
    sectionLeaves = [];
  };

  for (const leaf of leaves) {
    if (leaf.section) {
      flushSection();
      sectionName = leaf.section;
      sectionLeaves = [leaf];
    } else if (sectionName) {
      sectionLeaves.push(leaf);
    } else if (leaf.to) {
      nodes.push(
        <li key={leaf.label}>
          <Link
            to={leaf.to}
            onClick={onNavigate}
            aria-current={isActivePath(pathname, leaf.to) ? 'page' : undefined}
            className={
              'flex min-h-10 w-full items-center gap-2.5 rounded-xl px-3 text-start text-[12px] transition ' +
              (isActivePath(pathname, leaf.to)
                ? 'bg-pick/20 text-white ring-1 ring-pick/30'
                : 'text-on-navy/80 hover:bg-white/10 hover:text-white')
            }
          >
            <NavIcon icon={leaf.icon} tone={leaf.tone} toneClass={toneClass} />
            <span className="min-w-0 flex-1">{leaf.label}</span>
          </Link>
        </li>,
      );
    } else {
      nodes.push(<li key={leaf.label}>{DisabledLeaf({ leaf, nested: true })}</li>);
    }
  }
  flushSection();
  return <>{nodes}</>;
}
