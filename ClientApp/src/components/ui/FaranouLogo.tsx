import { useId } from 'react';

/**
 * The Faranou mark (three rising bars and a gold arrow on a blue tile) with the product name.
 * Inline SVG: no image request, no external asset. The name is real text so it is read aloud
 * and scales with the font.
 */
export function FaranouLogo({ size = 42, label = true }: { size?: number; label?: boolean }) {
  const titleId = useId();

  return (
    <span className="inline-flex items-center gap-3">
      <svg width={size} height={size} viewBox="0 0 64 64" fill="none" role="img" aria-labelledby={titleId}>
        <title id={titleId}>نشان فرانو</title>
        <rect width="64" height="64" rx="14" fill="#2468D8" />
        <rect x="14" y="37" width="8" height="13" rx="2" fill="#fff" />
        <rect x="26" y="29" width="8" height="21" rx="2" fill="#fff" />
        <rect x="38" y="20" width="8" height="30" rx="2" fill="#fff" />
        <path d="M13 30c9-1 17-6 24-16" stroke="#F2C45A" strokeWidth="4" strokeLinecap="round" />
        <path d="M30 12.5l8-.6-1.8 7.6" stroke="#F2C45A" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      {label ? <span className="text-[1.7rem] font-bold leading-none">فرانو</span> : null}
    </span>
  );
}
