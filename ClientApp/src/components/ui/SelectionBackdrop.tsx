/**
 * The decorative depth of the selection screens, drawn as inline SVG (a few paths, well under
 * 2 KB): pale mountain ridges fading into the page, thin golden glow lines, and faint ascending
 * chart bars at the right edge. It is hidden from assistive technology, takes no pointer events,
 * does not animate and loads nothing from the network.
 */
export function SelectionBackdrop() {
  return (
    <div aria-hidden="true" className="pointer-events-none absolute inset-x-0 top-0 -z-10 h-[24rem] overflow-hidden">
      <svg
        viewBox="0 0 1710 380"
        preserveAspectRatio="xMidYMin slice"
        className="absolute inset-0 h-full w-full"
        focusable="false"
      >
        <defs>
          <linearGradient id="sb-ridge-back" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#ffffff" stopOpacity="0.95" />
            <stop offset="1" stopColor="#cfdff2" stopOpacity="0.35" />
          </linearGradient>
          <linearGradient id="sb-ridge-front" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0" stopColor="#f4f8fe" stopOpacity="0.95" />
            <stop offset="1" stopColor="#dbe7f5" stopOpacity="0.2" />
          </linearGradient>
          <linearGradient id="sb-glow" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0" stopColor="#f1c36f" stopOpacity="0" />
            <stop offset="0.5" stopColor="#f1c36f" stopOpacity="0.75" />
            <stop offset="1" stopColor="#f1c36f" stopOpacity="0" />
          </linearGradient>
          <linearGradient id="sb-fade" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0.55" stopColor="#eef3fa" stopOpacity="0" />
            <stop offset="1" stopColor="#eef3fa" stopOpacity="1" />
          </linearGradient>
        </defs>

        <path
          d="M0 250 L140 205 L300 238 L520 130 L640 175 L860 70 L1010 150 L1180 105 L1360 190 L1530 150 L1710 210 V380 H0 Z"
          fill="url(#sb-ridge-back)"
        />
        <path
          d="M0 300 L220 238 L420 275 L690 185 L860 235 L1080 160 L1290 245 L1500 205 L1710 262 V380 H0 Z"
          fill="url(#sb-ridge-front)"
        />
        <path d="M0 250 L140 205 L300 238 L520 130 L640 175 L860 70 L1010 150 L1180 105 L1360 190 L1530 150 L1710 210" fill="none" stroke="url(#sb-glow)" strokeWidth="1.2" />
        <path d="M0 300 L220 238 L420 275 L690 185 L860 235 L1080 160 L1290 245 L1500 205 L1710 262" fill="none" stroke="url(#sb-glow)" strokeWidth="1" opacity="0.7" />
        <path d="M300 330 Q860 120 1500 250" fill="none" stroke="url(#sb-glow)" strokeWidth="0.8" opacity="0.6" />

        <g fill="#8fb4de" opacity="0.18" className="max-lg:hidden">
          <rect x="1590" y="190" width="14" height="40" rx="2" />
          <rect x="1612" y="168" width="14" height="62" rx="2" />
          <rect x="1634" y="140" width="14" height="90" rx="2" />
          <rect x="1656" y="112" width="14" height="118" rx="2" />
        </g>
        <path d="M1580 232 L1612 214 L1640 192 L1672 150" fill="none" stroke="#f1c36f" strokeWidth="1.5" opacity="0.55" className="max-lg:hidden" />
        <rect width="1710" height="380" fill="url(#sb-fade)" />
      </svg>
    </div>
  );
}
