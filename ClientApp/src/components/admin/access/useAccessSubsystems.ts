import { useCallback, useEffect, useState } from 'react';
import { ApiError, getAccessCatalogSubsystems } from '../../../api/client';
import type { AccessSubsystem } from '../../../api/types';

export type SubsystemCatalogState =
  | { kind: 'loading' }
  | { kind: 'ready'; subsystems: AccessSubsystem[] }
  /** The catalog could not be read (403 = not an installation administrator): the page degrades honestly. */
  | { kind: 'limited'; forbidden: boolean; message: string };

/**
 * The read-only D-075 subsystem catalog (`GET /admin/access/catalog/subsystems`, installation administrators only).
 * It is loaded once per page; a 403 or any failure yields the `limited` state instead of invented data.
 */
export function useAccessSubsystems(onUnauthenticated: (error: unknown) => boolean): { state: SubsystemCatalogState; retry: () => void } {
  const [state, setState] = useState<SubsystemCatalogState>({ kind: 'loading' });
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setState({ kind: 'loading' });
    getAccessCatalogSubsystems(controller.signal)
      .then((subsystems) => {
        if (!controller.signal.aborted) setState({ kind: 'ready', subsystems });
      })
      .catch((cause: unknown) => {
        if (controller.signal.aborted || onUnauthenticated(cause)) return;
        const forbidden = cause instanceof ApiError && cause.status === 403;
        setState({
          kind: 'limited',
          forbidden,
          message: forbidden
            ? 'کاتالوگ زیرسیستم‌ها فقط برای مدیر نصب در دسترس است؛ فهرست زیر فقط از روی پیشوند مجوزهای واقعی ساخته شده و عنوان فارسی و وضعیت لایسنس زیرسیستم‌ها نمایش داده نمی‌شود.'
            : 'دریافت کاتالوگ زیرسیستم‌ها انجام نشد؛ فهرست زیر فقط از روی پیشوند مجوزهای واقعی ساخته شده است.',
        });
      });
    return () => controller.abort();
  }, [attempt, onUnauthenticated]);
  const retry = useCallback(() => setAttempt((value) => value + 1), []);
  return { state, retry };
}
