import type { ReactNode } from 'react';

export function LoadingState({ label }: { label: string }) {
  return (
    <div role="status" aria-live="polite" className="flex items-center gap-3 rounded-xl border border-line bg-white p-5 text-sub">
      <span aria-hidden="true" className="size-4 animate-spin rounded-full border-2 border-line border-t-teal-deep motion-reduce:animate-none" />
      <span>{label}</span>
    </div>
  );
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div role="alert" className="rounded-xl border border-danger/30 bg-danger-soft p-5 text-danger">
      <p className="font-medium">{message}</p>
      {onRetry ? (
        <button
          type="button"
          onClick={onRetry}
          className="mt-3 rounded-lg border border-danger/40 bg-white px-4 py-2 text-sm font-medium text-danger hover:bg-danger-soft"
        >
          تلاش دوباره
        </button>
      ) : null}
    </div>
  );
}

export function EmptyState({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="rounded-xl border border-dashed border-line bg-white p-6 text-center">
      <p className="font-semibold text-ink">{title}</p>
      {children ? <div className="mt-2 text-sm text-muted">{children}</div> : null}
    </div>
  );
}

function SkeletonBlock({ className }: { className: string }) {
  return <div aria-hidden="true" className={`rounded-xl border border-line bg-white/80 ${className}`} />;
}

/**
 * The full-screen loading state: a status message above a static skeleton with the same two
 * panels as the selector, so the page keeps its shape (no jump) when the choices arrive.
 * The skeleton does not animate.
 */
export function LoadingScreen({ label }: { label: string }) {
  return (
    <div className="flex flex-col gap-6">
      <LoadingState label={label} />
      <div className="grid gap-6 lg:grid-cols-[26.25rem_minmax(0,1fr)]">
        <div className="flex flex-col gap-2.5">
          <SkeletonBlock className="h-20" />
          <SkeletonBlock className="h-20" />
        </div>
        <div className="flex flex-col gap-2.5">
          <SkeletonBlock className="h-16" />
          <SkeletonBlock className="h-16" />
          <SkeletonBlock className="h-16" />
        </div>
      </div>
    </div>
  );
}
