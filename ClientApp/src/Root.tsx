import { Suspense, lazy, useMemo, useState, type ComponentType } from 'react';
import { App } from './App';
import { LoadingState } from './components/Feedback';
import { ErrorBoundary } from './components/ui/ErrorBoundary';

// The two provisioning pages are rarely used (first-run enrollment and package import), so they
// are separate chunks; the common path (the application) stays in the entry bundle.
const loadEnrollmentPage = () =>
  import('./components/ProvisioningEnrollmentPage').then((module) => ({ default: module.ProvisioningEnrollmentPage }));
const loadImportPage = () =>
  import('./components/CustomerProvisioningImportPage').then((module) => ({ default: module.CustomerProvisioningImportPage }));

/**
 * A lazily loaded page with a loading state and a failure state. `React.lazy` caches a rejected
 * import, so a retry builds a new lazy component (a fresh import) instead of re-rendering the old one.
 */
function LazyPage({ load }: { load: () => Promise<{ default: ComponentType }> }) {
  const [attempt, setAttempt] = useState(0);
  const Page = useMemo(() => lazy(load), [load, attempt]);
  return (
    <ErrorBoundary onRetry={() => setAttempt((value) => value + 1)}>
      <Suspense fallback={<LoadingState label="در حال بارگذاری…" />}>
        <Page />
      </Suspense>
    </ErrorBoundary>
  );
}

/** Chooses the top-level screen from the pathname (these screens are not part of the `/app` router). */
export function Root({ pathname, loadEnrollment = loadEnrollmentPage, loadImport = loadImportPage }: {
  pathname: string;
  /** Overridable loaders (tests simulate a failed chunk load). */
  loadEnrollment?: () => Promise<{ default: ComponentType }>;
  loadImport?: () => Promise<{ default: ComponentType }>;
}) {
  if (pathname === '/enroll') return <LazyPage load={loadEnrollment} />;
  if (pathname === '/provisioning/import') return <LazyPage load={loadImport} />;
  return <App />;
}
