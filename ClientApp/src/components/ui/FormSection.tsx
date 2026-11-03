import { useId, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

/** A titled group of fields; the section is named by its heading for assistive technology. */
export function FormSection({
  title,
  description,
  children,
  className,
  headingLevel = 3,
}: {
  title: string;
  description?: ReactNode;
  children: ReactNode;
  className?: string;
  /**
   * The heading level of the title. Keep the document outline continuous: a page whose title is
   * an `h1` and whose sections sit directly under it passes 2. Defaults to 3 (a section nested
   * below an `h2`).
   */
  headingLevel?: 2 | 3 | 4;
}) {
  const headingId = useId();
  const Heading = `h${headingLevel}` as const;
  return (
    <section aria-labelledby={headingId} className={cn('rounded-xl border border-line bg-white p-5', className)}>
      <Heading id={headingId} className="text-base font-bold text-ink">
        {title}
      </Heading>
      {description ? <p className="mt-1 text-sm leading-6 text-sub">{description}</p> : null}
      <div className="mt-4 grid gap-4 sm:grid-cols-2">{children}</div>
    </section>
  );
}
