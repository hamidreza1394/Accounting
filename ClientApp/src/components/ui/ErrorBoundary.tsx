import { Component, createRef, type ErrorInfo, type ReactNode } from 'react';
import { Button } from './button';

type ErrorBoundaryProps = {
  children: ReactNode;
  /** Called when the user asks to retry, so the parent can recreate what failed (e.g. a lazy import). */
  onRetry?: () => void;
};

type ErrorBoundaryState = { failed: boolean };

/**
 * Catches a render or lazy-chunk-load failure below it and shows a Persian message with a retry
 * and a full reload action. The message is a `role="alert"` and takes focus when it appears.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  override state: ErrorBoundaryState = { failed: false };
  private readonly messageRef = createRef<HTMLDivElement>();

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { failed: true };
  }

  override componentDidCatch(error: unknown, info: ErrorInfo): void {
    // Only the component stack is logged; the error object is not echoed to the page.
    console.error('ErrorBoundary caught a failure', error instanceof Error ? error.name : 'unknown', info.componentStack);
  }

  override componentDidUpdate(_previous: ErrorBoundaryProps, previousState: ErrorBoundaryState): void {
    if (this.state.failed && !previousState.failed) this.messageRef.current?.focus();
  }

  private readonly retry = (): void => {
    this.props.onRetry?.();
    this.setState({ failed: false });
  };

  override render(): ReactNode {
    if (!this.state.failed) return this.props.children;
    return (
      <div className="mx-auto max-w-lg p-6">
        <div
          ref={this.messageRef}
          role="alert"
          tabIndex={-1}
          className="rounded-xl border border-danger/30 bg-danger-soft p-5 text-danger outline-none focus-visible:ring-2 focus-visible:ring-gold"
        >
          <p className="font-medium">بارگذاری این بخش انجام نشد. اتصال خود را بررسی کنید و دوباره تلاش کنید.</p>
          <div className="mt-3 flex flex-wrap gap-2">
            <Button type="button" variant="outline" size="lg" onClick={this.retry} className="h-auto min-h-11 bg-white px-4 py-2">
              تلاش دوباره
            </Button>
            <Button type="button" variant="outline" size="lg" onClick={() => window.location.reload()} className="h-auto min-h-11 bg-white px-4 py-2">
              بارگذاری مجدد صفحه
            </Button>
          </div>
        </div>
      </div>
    );
  }
}
