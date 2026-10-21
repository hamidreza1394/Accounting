import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { WorkContext } from '../api/types';
import { antiforgeryRoute, company, contextBody, period, selected, session, stubApi } from '../test/api';
import { WorkContextSelector } from './WorkContextSelector';

function renderSelector(context: WorkContext) {
  return render(
    <WorkContextSelector
      session={session}
      context={context}
      current={null}
      onSelected={vi.fn()}
      onLogout={vi.fn()}
      onRefresh={vi.fn()}
      onUnauthenticated={() => false}
    />,
  );
}

const many = (count: number) =>
  Array.from({ length: count }, (_, index) => company(`c${index}`, `شرکت شماره ${index + 1}`, [period(`p${index}`, 'دوره')]));

describe('WorkContextSelector', () => {
  it('always offers the search and filters by name', async () => {
    renderSelector(contextBody({ companies: many(9) }) as WorkContext);

    await userEvent.type(screen.getByRole('searchbox', { name: 'جست‌وجوی شرکت بر اساس نام یا شناسه ملی' }), 'شماره 8');

    expect(screen.getAllByRole('radio', { name: /شرکت شماره/ })).toHaveLength(1);
  });

  it('finds a company by national id typed with Persian digits and extra spaces', async () => {
    renderSelector(
      contextBody({
        companies: [company('a', 'شرکت الف', [period('p1', 'دوره')], '10101010101'), company('b', 'شرکت ب', [period('p2', 'دوره')], '20202020202')],
      }) as WorkContext,
    );

    await userEvent.type(screen.getByRole('searchbox'), '  ۱۰۱۰۱  ');

    expect(screen.getAllByRole('radio', { name: /شرکت/ })).toHaveLength(1);
    expect(screen.getByRole('radio', { name: /شرکت الف/ })).toBeInTheDocument();
  });

  it('says so when the search finds nothing', async () => {
    renderSelector(contextBody({ companies: many(3) }) as WorkContext);

    await userEvent.type(screen.getByRole('searchbox'), 'ناموجود');

    expect(screen.getByText('شرکتی با این مشخصات پیدا نشد.')).toBeInTheDocument();
  });

  it('disables a company without an open period and explains why', () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [period('p1', 'دوره', { status: 'Closed', isSelectable: false })]),
          company('b', 'شرکت ب', [period('p2', 'دوره')]),
        ],
      }) as WorkContext,
    );

    const disabled = screen.getByRole('radio', { name: /شرکت الف/ });
    expect(disabled).toBeDisabled();
    expect(disabled).toHaveAccessibleDescription(/یک دوره مالی باز برای این شرکت لازم است/);
    expect(screen.getByRole('radio', { name: /شرکت ب/ })).toBeEnabled();
  });

  it('shows the status as text, not only as colour', () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [
            period('p1', 'باز'),
            period('p2', 'سال قدیمی', { status: 'Closed', isSelectable: false }),
            period('p3', 'سال بایگانی', { status: 'Archived', isSelectable: false }),
          ]),
        ],
      }) as WorkContext,
    );

    expect(screen.getAllByText('باز').length).toBeGreaterThan(0);
    expect(screen.getByText('بسته')).toBeInTheDocument();
    expect(screen.getByText('بایگانی‌شده')).toBeInTheDocument();
  });

  it('shows Persian dates and keeps the national id left-to-right', () => {
    renderSelector(contextBody({ companies: [company('a', 'شرکت الف', [period('p1', 'دوره')], '10101010101')] }) as WorkContext);

    expect(screen.getByText(/۱۴۰۵\/۰۱\/۰۱ تا ۱۴۰۵\/۱۲\/۲۹/)).toBeInTheDocument();
    expect(screen.getByText('10101010101').closest('bdi')).toHaveAttribute('dir', 'ltr');
  });

  it('preselects the current choice when the user came to change it', () => {
    const companyA = company('a', 'شرکت الف', [period('p1', 'اول'), period('p2', 'دوم')]);
    render(
      <WorkContextSelector
        session={session}
        context={contextBody({ companies: [companyA] }) as WorkContext}
        current={selected(companyA, companyA.fiscalPeriods[1]) as never}
        onSelected={vi.fn()}
        onCancel={vi.fn()}
        onLogout={vi.fn()}
        onRefresh={vi.fn()}
        onUnauthenticated={() => false}
      />,
    );

    expect(screen.getByRole('radio', { name: /دوم/ })).toBeChecked();
  });

  it('shows a generic message and no details when the server fails', async () => {
    stubApi({
      ...antiforgeryRoute,
      'POST /api/v1/context': { status: 500, body: { title: 'SqlException: secret' } },
    });
    renderSelector(contextBody({ companies: [company('a', 'شرکت الف', [period('p1', 'دوره')])] }) as WorkContext);

    await userEvent.click(screen.getByRole('button', { name: /ورود به محیط کاری/ }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('خطای غیرمنتظره');
    expect(alert).not.toHaveTextContent('Sql');
    // A server failure is retried with the same button; the stale-list action is not offered.
    expect(screen.queryByRole('button', { name: 'به‌روزرسانی فهرست' })).not.toBeInTheDocument();
  });

  it('shows only data the server provides: no licence, user count or subsystem fields', () => {
    renderSelector(contextBody({ companies: [company('a', 'شرکت الف', [period('p1', 'دوره')], '10101010101')] }) as WorkContext);

    expect(screen.queryByText(/مجوز/)).not.toBeInTheDocument();
    expect(screen.queryByText(/زیرسیستم/)).not.toBeInTheDocument();
    expect(screen.queryByText(/کاربر$/)).not.toBeInTheDocument();
  });

  it('shows the fiscal periods of the chosen company only, and clears the period when the company changes', async () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [period('p1', 'دوره الف ۱'), period('p2', 'دوره الف ۲')]),
          company('b', 'شرکت ب', [period('p3', 'دوره ب ۱'), period('p4', 'دوره ب ۲')]),
        ],
      }) as WorkContext,
    );

    expect(screen.getByText('ابتدا یک شرکت انتخاب کنید.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('radio', { name: /شرکت الف/ }));
    await userEvent.click(screen.getByRole('radio', { name: /دوره الف ۲/ }));
    expect(screen.getByRole('button', { name: /ورود به محیط کاری/ })).not.toHaveAttribute('aria-disabled');

    await userEvent.click(screen.getByRole('radio', { name: /شرکت ب/ }));

    expect(screen.queryByRole('radio', { name: /دوره الف/ })).not.toBeInTheDocument();
    expect(screen.getByRole('radio', { name: /دوره ب ۱/ })).not.toBeChecked();
    expect(screen.getByRole('button', { name: /ورود به محیط کاری/ })).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByText('دوره‌های مالی متعلق به شرکت ب')).toBeInTheDocument();
  });

  it('the cancel button leaves the screen: it goes back when changing, and signs out on first entry', async () => {
    const onCancel = vi.fn();
    const onLogout = vi.fn();
    const ctx = contextBody({ companies: [company('a', 'شرکت الف', [period('p1', 'دوره')])] }) as WorkContext;
    const { unmount } = render(
      <WorkContextSelector session={session} context={ctx} current={null} onSelected={vi.fn()} onCancel={onCancel} onLogout={onLogout} onRefresh={vi.fn()} onUnauthenticated={() => false} />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onLogout).not.toHaveBeenCalled();
    unmount();

    const logout = vi.fn();
    render(
      <WorkContextSelector session={session} context={ctx} current={null} onSelected={vi.fn()} onLogout={logout} onRefresh={vi.fn()} onUnauthenticated={() => false} />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(logout).toHaveBeenCalledOnce();
  });
});
