// The official Faranou mark (cut out of the official logo; never redrawn) and the product name.
// The image is a local static file; its size is explicit so the page does not shift while it loads.
const MARK_WIDTH = 432;
const MARK_HEIGHT = 416;

type FaranouLogoProps = {
  /** Height of the mark in pixels. */
  size?: number;
  /** Shows the name and the tagline next to the mark. */
  label?: boolean;
  /** `dark` is for the navy header: the mark sits on a white tile and the text is white. */
  tone?: 'light' | 'dark';
};

export function FaranouLogo({ size = 40, label = true, tone = 'light' }: FaranouLogoProps) {
  const dark = tone === 'dark';
  const mark = (
    <img
      src="/images/faranou-brand-mark.webp"
      width={Math.round((size * MARK_WIDTH) / MARK_HEIGHT)}
      height={size}
      alt="نشان فرانو"
      decoding="async"
      className="shrink-0"
    />
  );

  return (
    <span className="inline-flex items-center gap-3">
      {dark ? <span className="inline-flex rounded-lg bg-white p-1">{mark}</span> : mark}
      {label ? (
        <span className="flex flex-col leading-tight">
          <strong className={`text-lg font-extrabold ${dark ? 'text-white' : 'text-navy'}`}>فرانو</strong>
          <span className={`text-xs ${dark ? 'text-white/75' : 'text-sub'}`}>نرم‌افزار حسابداری هوشمند</span>
        </span>
      ) : null}
    </span>
  );
}
