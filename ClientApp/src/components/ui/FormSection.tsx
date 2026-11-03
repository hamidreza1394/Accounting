import { useId, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

/** A titled group of fields; the section is named by its heading for assistive technology. */
export function FormSection({
  title,
  description,
  children,
  className,
}: {
  title: string;
  description?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  const headingId = useId();
  return (
    <section aria-labelledby={headingId} className={cn('rounded-xl border border-line bg-white p-5', className)}>
      <h3 id={headingId} className="text-base font-bold text-ink">
        {title}
      </h3>
      {description ? <p className="mt-1 text-sm leading-6 text-sub">{description}</p> : null}
      <div className="mt-4 grid gap-4 sm:grid-cols-2">{children}</div>
    </section>
  );
}
