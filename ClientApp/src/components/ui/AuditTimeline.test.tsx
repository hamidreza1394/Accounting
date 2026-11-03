import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { AuditTimeline, type AuditTimelineEvent } from './AuditTimeline';

const events: AuditTimelineEvent[] = [
  { id: 'a', label: 'ورود موفق', tone: 'success', icon: <svg data-testid="icon-a" />, occurredAt: '2026-11-02T08:00:00Z', detail: '203.0.113.7' },
  { id: 'b', label: 'ورود ناموفق', tone: 'danger', icon: <svg />, occurredAt: '2026-11-01T08:00:00Z', detail: null },
];

describe('AuditTimeline', () => {
  it('renders a named ordered list with one item per event', () => {
    render(<AuditTimeline label="سوابق" events={events} detailLabel="نشانی IP" detailFallback="IP ثبت نشده" />);
    const list = screen.getByRole('list', { name: 'سوابق' });
    expect(within(list).getAllByRole('listitem')).toHaveLength(2);
  });

  it('puts the meaning in text, hides the icon, and shows the IP left-to-right', () => {
    render(<AuditTimeline label="سوابق" events={events} detailLabel="نشانی IP" detailFallback="IP ثبت نشده" />);
    const [first, second] = screen.getAllByRole('listitem');
    expect(first).toHaveTextContent('ورود موفق');
    expect(screen.getByTestId('icon-a').closest('[aria-hidden="true"]')).not.toBeNull();
    expect(within(first!).getByText('203.0.113.7')).toHaveAttribute('dir', 'ltr');
    expect(first).toHaveTextContent('نشانی IP');
    expect(second).toHaveTextContent('IP ثبت نشده');
  });

  it('formats the date as a machine-readable time element in Jalali', () => {
    render(<AuditTimeline label="سوابق" events={events} detailLabel="نشانی IP" detailFallback="IP ثبت نشده" />);
    const time = screen.getAllByRole('listitem')[0]!.querySelector('time');
    expect(time).toHaveAttribute('datetime', '2026-11-02T08:00:00Z');
    expect(time?.textContent).toMatch(/^۱۴۰۵\/۰۸\/۱۱ /);
  });

  it('renders nothing but an empty list when there are no events', () => {
    render(<AuditTimeline label="سوابق" events={[]} detailLabel="نشانی IP" detailFallback="-" />);
    expect(screen.queryAllByRole('listitem')).toHaveLength(0);
  });
});
