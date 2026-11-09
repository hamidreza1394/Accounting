import { useState, type ReactNode } from 'react';
import { ChevronDown, type LucideIcon } from 'lucide-react';
import { Link } from '@/lib/router';

function isActivePath(pathname: string, to: string): boolean {
  if (to === '/') return pathname === '/';
  return pathname === to || pathname.startsWith(`${to}/`);
}

export type NestedNavLeaf = {
  label: string;
  icon: LucideIcon;
  to?: string;
  /** Consecutive leaves with the same section name form one collapsible section. */
  section?: string;
  sectionIcon?: LucideIcon;
};

/** Sub-menu icon: one uniform, slightly smaller glyph (no per-item colour). */
export function SubNavIcon({ icon: Icon }: { icon: LucideIcon }) {
  return (
    <span className="faranou-nav-icon faranou-nav-icon--sub" aria-hidden="true">
      <Icon size={15} strokeWidth={2} />
    </span>
  );
}

function SubLink({ leaf, pathname, onNavigate }: { leaf: NestedNavLeaf & { to: string }; pathname: string; onNavigate: () => void }) {
  const active = isActivePath(pathname, leaf.to);
  return (
    <Link
      to={leaf.to}
      onClick={onNavigate}
      aria-current={active ? 'page' : undefined}
      className={'faranou-nav-leaf faranou-nav-leaf--sub ' + (active ? 'faranou-nav-active' : 'faranou-nav-link')}
    >
      <SubNavIcon icon={leaf.icon} />
      <span className="min-w-0 flex-1 whitespace-normal leading-snug">{leaf.label}</span>
    </Link>
  );
}

type Block = { section?: string; icon?: LucideIcon; leaves: NestedNavLeaf[] };

/** Splits the leaves into plain leaves and runs of leaves sharing a section name. */
function toBlocks(leaves: readonly NestedNavLeaf[]): Block[] {
  const blocks: Block[] = [];
  for (const leaf of leaves) {
    const last = blocks[blocks.length - 1];
    if (leaf.section && last?.section === leaf.section) {
      last.leaves.push(leaf);
      if (!last.icon && leaf.sectionIcon) last.icon = leaf.sectionIcon;
    } else if (leaf.section) {
      blocks.push({ section: leaf.section, ...(leaf.sectionIcon ? { icon: leaf.sectionIcon } : {}), leaves: [leaf] });
    } else {
      blocks.push({ leaves: [leaf] });
    }
  }
  return blocks;
}

/**
 * Renders the leaves of a sidebar group, with collapsible Persian sections
 * (e.g. «ساختار سازمانی» owning واحدها / سمت‌ها / کارکنان). Sections start open once their
 * group is open and can be folded by hand; navigation never changes their state.
 */
export function NestedNavSections({
  groupLabel,
  leaves,
  pathname,
  onNavigate,
  DisabledLeaf,
}: {
  groupLabel: string;
  leaves: readonly NestedNavLeaf[];
  pathname: string;
  onNavigate: () => void;
  DisabledLeaf: (props: { leaf: NestedNavLeaf; nested?: boolean }) => ReactNode;
}) {
  // `false` means the user folded the section by hand; navigation never changes it, so the
  // active route (highlight only) stays independent of what is expanded.
  const [openSections, setOpenSections] = useState<Record<string, boolean>>({});

  const renderLeaf = (leaf: NestedNavLeaf) =>
    leaf.to ? (
      <li key={leaf.label}>
        <SubLink leaf={{ ...leaf, to: leaf.to }} pathname={pathname} onNavigate={onNavigate} />
      </li>
    ) : (
      <li key={leaf.label}>{DisabledLeaf({ leaf, nested: true })}</li>
    );

  return (
    <>
      {toBlocks(leaves).map((block) => {
        if (!block.section) return block.leaves.map(renderLeaf);
        const key = `${groupLabel}::${block.section}`;
        const open = openSections[key] !== false;
        const sectionId = `nav-section-${key.replace(/[^\w؀-ۿ]+/g, '-')}`;
        const SectionIcon = block.icon;
        const active = block.leaves.some((leaf) => leaf.to !== undefined && isActivePath(pathname, leaf.to));
        const disabled = block.leaves.every((leaf) => leaf.to === undefined);
        return (
          <li key={key} className="faranou-nav-section-item">
            {disabled ? (
              <div aria-disabled="true" className="faranou-nav-disabled faranou-nav-leaf faranou-nav-leaf--sub">
                {SectionIcon ? <SubNavIcon icon={SectionIcon} /> : null}
                <span className="min-w-0 flex-1 text-start">{block.section}</span>
                <span className="faranou-nav-soon-badge">به‌زودی</span>
              </div>
            ) : (
              <>
                <button
                  type="button"
                  className="faranou-nav-section-toggle"
                  aria-expanded={open}
                  aria-controls={sectionId}
                  onClick={() => setOpenSections((previous) => ({ ...previous, [key]: previous[key] === false }))}
                >
                  {SectionIcon ? <SubNavIcon icon={SectionIcon} /> : null}
                  <span className="min-w-0 flex-1 text-start">{block.section}</span>
                  {active && !open ? <span className="faranou-nav-group-dot" aria-hidden="true" /> : null}
                  <ChevronDown size={14} className={'faranou-nav-chevron' + (open ? ' faranou-nav-chevron--open' : '')} aria-hidden="true" />
                </button>
                {open ? (
                  <ul id={sectionId} className="faranou-nav-leaf-list">
                    {block.leaves.map(renderLeaf)}
                  </ul>
                ) : null}
              </>
            )}
          </li>
        );
      })}
    </>
  );
}
