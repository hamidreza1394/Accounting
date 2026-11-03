import type { FiscalPeriodStatus } from '../api/types';

/**
 * The single place that names a fiscal period status for display. The wording differs on purpose
 * by context (existing, tested behaviour), but the three tables live together so a new status
 * or a wording change is made once.
 */

/** A fiscal year in the selection lists and footer ("the year is active / closed"). */
export const yearStatusLabel: Record<FiscalPeriodStatus, string> = {
  Open: 'فعال',
  Closed: 'بسته شده',
  Archived: 'بایگانی',
};

/** One period in the company overview table ("the period is open / closed"). */
export const periodStatusLabel: Record<FiscalPeriodStatus, string> = {
  Open: 'باز',
  Closed: 'بسته',
  Archived: 'بایگانی',
};

/** Why a period cannot be entered: shown on a company card in the selector. */
export const blockedPeriodLabel: Record<FiscalPeriodStatus, string> = {
  Open: 'باز',
  Closed: 'بسته',
  Archived: 'بایگانی‌شده',
};

/** Tailwind colour classes of the status chip on the period list (tokens from styles/index.css). */
export const yearBadgeClass: Record<FiscalPeriodStatus, string> = {
  Open: 'bg-mint text-success',
  Closed: 'bg-chip text-sub',
  Archived: 'bg-chip text-sub',
};

/** Tailwind classes of the pill in the company overview. */
export const periodPillClass: Record<FiscalPeriodStatus, string> = {
  Open: 'bg-success-soft text-success',
  Closed: 'bg-warning-soft text-warning',
  Archived: 'bg-cool text-muted',
};

/** The small status dot in the footer summary on the dark bar. */
export const statusDotClass: Record<FiscalPeriodStatus, string> = {
  Open: 'bg-dot-open',
  Closed: 'bg-line-strong',
  Archived: 'bg-faint',
};
