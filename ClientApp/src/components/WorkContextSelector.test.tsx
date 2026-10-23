import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { CompanyChoice, SelectedContext, WorkContext } from '../api/types';
import { antiforgeryRoute, company, contextBody, period, selected, session, stubApi } from '../test/api';
import { WorkContextSelector } from './WorkContextSelector';

const y1405 = { startDate: '2026-03-21', endDate: '2027-03-20' };
const y1404 = { startDate: '2025-03-21', endDate: '2026-03-20' };
const archived = { status: 'Archived', isSelectable: false, accessMode: null };
const closed = { status: 'Closed', accessMode: 'readOnly' };

type Props = Partial<{
  current: SelectedContext | null;
  onSelected: () => void;
  onCancel: () => void;
  onLogout: () => void;
  onRefresh: () => void;
  onUnauthenticated: (error: unknown) => boolean;
}>;

function renderSelector(context: unknown, props: Props = {}) {
  return render(
    <WorkContextSelector
      session={session}
      context={context as WorkContext}
      current={props.current ?? null}
      onSelected={props.onSelected ?? vi.fn()}
      onCancel={props.onCancel}
      onLogout={props.onLogout ?? vi.fn()}
      onRefresh={props.onRefresh ?? vi.fn()}
      onUnauthenticated={props.onUnauthenticated ?? (() => false)}
    />,
  );
}

const enterButton = () => screen.getByRole('button', { name: /ورود به محیط کاری/ });
const many = (count: number) =>
  Array.from({ length: count }, (_, index) => company(`c${index}`, `شرکت شماره ${index + 1}`, [period(`p${index}`, 'سال ۱۴۰۵', y1405)]));

const twoYears = () =>
  contextBody({
    companies: [
      company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405), period('a4', 'سال ۱۴۰۴', { ...y1404, ...closed })], '10101010101'),
      company('b', 'شرکت ب', [period('b5', 'سال ۱۴۰۵', y1405), period('b4', 'سال ۱۴۰۴', { ...y1404, ...archived })]),
    ],
  });

describe('WorkContextSelector', () => {
  it('shows the title, the guidance and real counts, and claims no group relation', () => {
    renderSelector(twoYears());

    expect(screen.getByRole('heading', { level: 1, name: 'فرانو - دوره مالی' })).toBeInTheDocument();
    expect(screen.getByText('برای ورود به محیط کاری، ابتدا دوره مالی و سپس شرکت موردنظر را انتخاب کنید.')).toBeInTheDocument();
    expect(screen.getByText('شما به ۲ شرکت دسترسی دارید')).toBeInTheDocument();
    expect(screen.getByText('۲ دوره مالی')).toBeInTheDocument();
    expect(screen.queryByText(/همگروه|هلدینگ|زیرمجموعه|شرکت مادر/)).not.toBeInTheDocument();
  });

  it('has a header with the user, one real logout control and no decorative controls', async () => {
    const onLogout = vi.fn();
    renderSelector(twoYears(), { onLogout });

    const banner = screen.getByRole('banner');
    expect(within(banner).getByText('آلیس احمدی')).toBeInTheDocument();
    expect(within(banner).getByText('alice')).toBeInTheDocument();
    expect(within(banner).getAllByRole('button')).toHaveLength(1);
    expect(screen.queryByText(/فرانو - داشبورد/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/داشبورد/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /اعلان|تنظیمات|خانه/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
    await userEvent.click(within(banner).getByRole('button', { name: 'خروج' }));
    expect(onLogout).toHaveBeenCalledOnce();
  });

  it('lists the years newest first in the first panel and asks for a year before showing companies', () => {
    renderSelector(twoYears());

    const years = screen.getByRole('radiogroup', { name: 'دوره‌های مالی' });
    const radios = within(years).getAllByRole('radio');
    expect(radios[0]).toHaveAccessibleName(/سال ۱۴۰۵/);
    expect(radios[1]).toHaveAccessibleName(/سال ۱۴۰۴/);
    expect(screen.getByText('ابتدا یک دوره مالی انتخاب کنید.')).toBeInTheDocument();
    expect(screen.queryByRole('radiogroup', { name: 'شرکت‌ها' })).not.toBeInTheDocument();
    expect(screen.getByText('۱۴۰۵/۰۱/۰۱')).toBeInTheDocument();
    expect(screen.getByText('۱۴۰۵/۱۲/۲۹')).toBeInTheDocument();
    expect(screen.getByText('۲ شرکت')).toBeInTheDocument();
    expect(screen.getByText('۲ شرکت · ۱ قابل ورود')).toBeInTheDocument();
  });

  it('badges each year by its real status: active, closed or archived', () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [
            period('a5', 'سال ۱۴۰۵', y1405),
            period('a4', 'سال ۱۴۰۴', { ...y1404, ...closed }),
            period('a3', 'سال ۱۴۰۳', { startDate: '2024-03-20', endDate: '2025-03-20', ...archived }),
          ]),
        ],
      }),
    );

    const years = within(screen.getByRole('radiogroup', { name: 'دوره‌های مالی' }));
    expect(years.getByRole('radio', { name: /سال ۱۴۰۵/ })).toHaveAccessibleName(/فعال/);
    expect(years.getByRole('radio', { name: /سال ۱۴۰۴/ })).toHaveAccessibleName(/بسته شده/);
    expect(years.getByRole('radio', { name: /سال ۱۴۰۳/ })).toHaveAccessibleName(/بایگانی/);
    expect(years.getByRole('radio', { name: /سال ۱۴۰۳/ })).toBeDisabled();
  });

  it('filters the years by title or Jalali date, accepting Persian digits', async () => {
    renderSelector(twoYears());
    const search = screen.getByRole('searchbox', { name: 'جست‌وجوی دوره مالی بر اساس عنوان یا تاریخ' });
    expect(search).toHaveAttribute('placeholder', 'جستجو در دوره‌های مالی...');
    const years = () => within(screen.getByRole('radiogroup', { name: 'دوره‌های مالی' })).getAllByRole('radio');

    await userEvent.type(search, 'سال ۱۴۰۴');
    expect(years()).toHaveLength(1);
    expect(years()[0]).toHaveAccessibleName(/سال ۱۴۰۴/);

    await userEvent.clear(search);
    await userEvent.type(search, '1405/01/01');
    expect(years()).toHaveLength(1);
    expect(years()[0]).toHaveAccessibleName(/سال ۱۴۰۵/);

    await userEvent.clear(search);
    await userEvent.type(search, 'ناموجود');
    expect(screen.getByText('دوره مالی با این مشخصات پیدا نشد.')).toBeInTheDocument();
  });

  it('shows the companies of the chosen year with their own status, and a disabled one with the reason', async () => {
    renderSelector(twoYears());

    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۴/ }));

    const companies = screen.getByRole('radiogroup', { name: 'شرکت‌ها' });
    const a = within(companies).getByRole('radio', { name: /شرکت الف/ });
    expect(a).toBeEnabled();
    expect(within(companies).getByText('فقط مشاهده و گزارش‌گیری')).toBeInTheDocument();
    const b = within(companies).getByRole('radio', { name: /شرکت ب/ });
    expect(b).toBeDisabled();
    expect(b).toHaveAccessibleDescription('ورود به این دوره امکان‌پذیر نیست.');
    expect(within(companies).getByText('بایگانی‌شده')).toBeInTheDocument();
  });

  it('shows the national id left-to-right only when it exists, and the role', async () => {
    renderSelector(twoYears());
    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۵/ }));

    expect(screen.getByText('10101010101').closest('bdi')).toHaveAttribute('dir', 'ltr');
    expect(screen.getAllByText(/کد ملی:/)).toHaveLength(1);
    expect(screen.getAllByText('حسابدار')).toHaveLength(2);
  });

  it('tells same-named companies apart by national id', async () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'آریا', [period('a1', 'سال', y1405)], '11111111111'),
          company('b', 'آریا', [period('b1', 'سال', y1405)], '22222222222'),
        ],
      }),
    );

    const rows = within(screen.getByRole('radiogroup', { name: 'شرکت‌ها' })).getAllByRole('radio');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveAccessibleName(/11111111111/);
    expect(rows[1]).toHaveAccessibleName(/22222222222/);
  });

  it('disables a year without any enterable company and does not select it', async () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405), period('a4', 'سال ۱۴۰۴', { ...y1404, ...archived })]),
          company('b', 'شرکت ب', [period('b5', 'سال ۱۴۰۵', y1405)]),
        ],
      }),
    );

    const old = screen.getByRole('radio', { name: /سال ۱۴۰۴/ });
    expect(old).toBeDisabled();
    expect(old).toHaveAccessibleDescription('ورود به این دوره امکان‌پذیر نیست.');
  });

  it('preselects nothing when several years and companies are possible, and keeps the button disabled', () => {
    renderSelector(twoYears());

    screen.getAllByRole('radio').forEach((radio) => expect(radio).not.toBeChecked());
    expect(enterButton()).toHaveAttribute('aria-disabled', 'true');
  });

  it('preselects the year and the company when there is exactly one enterable choice', () => {
    renderSelector(contextBody({ companies: [company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)])] }));

    expect(screen.getByRole('radio', { name: /سال ۱۴۰۵/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: /شرکت الف/ })).toBeChecked();
    expect(enterButton()).not.toHaveAttribute('aria-disabled');
    expect(screen.getByText('انتخاب شما:').parentElement).toHaveTextContent(/سال ۱۴۰۵ \(فعال\) · شرکت الف/);
  });

  it('preselects only the year when it is the single one but has several companies', () => {
    renderSelector(contextBody({ companies: many(3) }));

    expect(screen.getByRole('radio', { name: /سال ۱۴۰۵/ })).toBeChecked();
    screen.getAllByRole('radio', { name: /شرکت شماره/ }).forEach((radio) => expect(radio).not.toBeChecked());
    expect(enterButton()).toHaveAttribute('aria-disabled', 'true');
  });

  it('starts at the current choice when the user came to change it', () => {
    const context = twoYears() as unknown as WorkContext;
    const a = context.companies[0] as CompanyChoice;
    renderSelector(context, { current: selected(a, a.fiscalPeriods[1]) as unknown as SelectedContext, onCancel: vi.fn() });

    expect(screen.getByRole('radio', { name: /سال ۱۴۰۴/ })).toBeChecked();
    expect(screen.getByRole('radio', { name: /شرکت الف/ })).toBeChecked();
  });

  it('keeps the company when the new year allows it, and clears it otherwise', async () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405), period('a4', 'سال ۱۴۰۴', y1404)]),
          company('b', 'شرکت ب', [period('b5', 'سال ۱۴۰۵', y1405), period('b4', 'سال ۱۴۰۴', y1404)]),
          company('c', 'شرکت ج', [period('c5', 'سال ۱۴۰۵', y1405), period('c4', 'سال ۱۴۰۴', { ...y1404, ...archived })]),
        ],
      }),
    );

    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۵/ }));
    await userEvent.click(screen.getByRole('radio', { name: /شرکت الف/ }));
    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۴/ }));
    expect(screen.getByRole('radio', { name: /شرکت الف/ })).toBeChecked();

    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۵/ }));
    await userEvent.click(screen.getByRole('radio', { name: /شرکت ج/ }));
    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۴/ }));
    within(screen.getByRole('radiogroup', { name: 'شرکت‌ها' }))
      .getAllByRole('radio')
      .forEach((radio) => expect(radio).not.toBeChecked());
    expect(enterButton()).toHaveAttribute('aria-disabled', 'true');
  });

  it('ends in the state of the last choice after quick successive year changes', async () => {
    renderSelector(twoYears());
    const y5 = screen.getByRole('radio', { name: /سال ۱۴۰۵/ });
    const y4 = screen.getByRole('radio', { name: /سال ۱۴۰۴/ });

    await userEvent.click(y5);
    await userEvent.click(y4);
    await userEvent.click(y5);
    await userEvent.click(y4);
    await userEvent.click(y5);

    expect(y5).toBeChecked();
    expect(y4).not.toBeChecked();
    expect(within(screen.getByRole('radiogroup', { name: 'شرکت‌ها' })).getByRole('radio', { name: /شرکت ب/ })).toBeEnabled();
  });

  it('filters the companies of the year by name or national id, with Persian digits', async () => {
    renderSelector(
      contextBody({
        companies: [...many(3), company('x', 'شرکت ویژه', [period('px', 'سال ۱۴۰۵', y1405)], '10101010101'), company('y', 'شرکت دیگر', [period('py', 'سال ۱۴۰۵', y1405)])],
      }),
    );
    const search = screen.getByRole('searchbox', { name: 'جست‌وجوی شرکت بر اساس نام یا کد ملی' });
    const list = () => within(screen.getByRole('radiogroup', { name: 'شرکت‌ها' }));
    expect(list().getAllByRole('radio')).toHaveLength(5);

    await userEvent.type(search, '  ۱۰۱۰۱  ');
    expect(list().getAllByRole('radio')).toHaveLength(1);
    await userEvent.clear(search);
    await userEvent.type(search, 'ویژه');
    expect(list().getAllByRole('radio')).toHaveLength(1);
    await userEvent.clear(search);
    await userEvent.type(search, 'ناموجود');
    expect(screen.getByText('شرکتی با این مشخصات پیدا نشد.')).toBeInTheDocument();
  });

  it('searches only the companies of the selected year and is disabled before a year is chosen', async () => {
    renderSelector(twoYears());
    const search = screen.getByRole('searchbox', { name: 'جست‌وجوی شرکت بر اساس نام یا کد ملی' });
    expect(search).toBeDisabled();

    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۴/ }));
    expect(search).toBeEnabled();
    await userEvent.type(search, 'الف');
    expect(within(screen.getByRole('radiogroup', { name: 'شرکت‌ها' })).getAllByRole('radio')).toHaveLength(1);
    // Changing the year clears the company query.
    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۵/ }));
    expect(search).toHaveValue('');
  });

  it('draws one full-width row per company (a list, never a grid) with the real role as a chip', async () => {
    renderSelector(
      contextBody({
        companies: [
          { ...company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)], '10101010101'), roleName: 'مدیر مالی' },
          { ...company('b', 'شرکت ب', [period('b5', 'سال ۱۴۰۵', y1405)]), roleName: '' },
        ],
      }),
    );

    const group = screen.getByRole('radiogroup', { name: 'شرکت‌ها' });
    expect(group.className).toContain('flex-col');
    expect(group.className).not.toMatch(/grid|columns/);
    expect(within(group).getAllByRole('radio')).toHaveLength(2);
    expect(within(group).getByText('مدیر مالی')).toBeInTheDocument();
    // No role, no chip: the row shows nothing instead of an invented label.
    expect(within(group).getAllByText(/نقش:/)).toHaveLength(1);
    // The count chip and the national id label.
    expect(within(screen.getByRole('region', { name: 'شرکت‌ها' })).getByText('۲ شرکت')).toBeInTheDocument();
    expect(within(group).getAllByText(/کد ملی:/)).toHaveLength(1);
  });

  it('notes companies that cannot be entered in any year, only when some year can be entered', () => {
    const { unmount } = renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)]),
          company('z', 'شرکت بدون دوره', []),
        ],
      }),
    );
    expect(screen.getByText(/شرکت بدون دوره/, { selector: 'span' })).toBeInTheDocument();
    unmount();

    renderSelector(contextBody({ companies: [company('z', 'شرکت بدون دوره', [])] }));
    expect(screen.queryByText(/دوره مالی قابل ورود برای این شرکت‌ها وجود ندارد/)).not.toBeInTheDocument();
  });

  it('sends the chosen pair once, even when the button is clicked twice quickly, and shows the entering state', async () => {
    const onSelected = vi.fn();
    let posts = 0;
    const fetchMock = stubApi({
      ...antiforgeryRoute,
      'POST /api/v1/context': () => {
        posts += 1;
        return { status: 200, body: { company: { id: 'a', name: 'شرکت الف', nationalId: null, roleName: 'حسابدار' }, fiscalPeriod: period('a5', 'سال ۱۴۰۵', y1405) } };
      },
    });
    renderSelector(twoYears(), { onSelected });

    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۵/ }));
    await userEvent.click(screen.getByRole('radio', { name: /شرکت الف/ }));
    await userEvent.dblClick(enterButton());

    await waitFor(() => expect(onSelected).toHaveBeenCalledOnce());
    expect(posts).toBe(1);
    const post = fetchMock.mock.calls.find((call) => (call[1] as RequestInit | undefined)?.method === 'POST');
    expect(JSON.parse((post?.[1] as RequestInit).body as string)).toEqual({ companyId: 'a', fiscalPeriodId: 'a5' });
    expect(screen.getByRole('button', { name: /در حال ورود/ })).toHaveAttribute('aria-busy', 'true');
  });

  it('changing the year makes no network request', async () => {
    const fetchMock = stubApi({ ...antiforgeryRoute });
    renderSelector(twoYears());

    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۵/ }));
    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۴/ }));

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it.each([404, 409])('shows a message and a refresh action when the server answers %i', async (status) => {
    const onRefresh = vi.fn();
    stubApi({
      ...antiforgeryRoute,
      'POST /api/v1/context': { status, body: { title: 'این دوره مالی برای ورود در دسترس نیست.' } },
    });
    renderSelector(contextBody({ companies: [company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)])] }), { onRefresh });

    await userEvent.click(enterButton());

    const alert = await screen.findByRole('alert');
    await waitFor(() => expect(alert).toHaveFocus());
    expect(alert).toHaveTextContent(status === 404 ? 'دسترسی شما به این شرکت یا دوره مالی تغییر کرده است' : 'این دوره مالی برای ورود در دسترس نیست.');
    await userEvent.click(screen.getByRole('button', { name: 'به‌روزرسانی فهرست' }));
    expect(onRefresh).toHaveBeenCalledOnce();
    // The user may try again after a refused attempt.
    expect(enterButton()).not.toHaveAttribute('aria-disabled');
  });

  it('hands a 401 to the application and shows no message', async () => {
    stubApi({ ...antiforgeryRoute, 'POST /api/v1/context': { status: 401 } });
    const onUnauthenticated = vi.fn(() => true);
    renderSelector(contextBody({ companies: [company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)])] }), { onUnauthenticated });

    await userEvent.click(enterButton());

    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows a generic message and no details when the server fails', async () => {
    stubApi({ ...antiforgeryRoute, 'POST /api/v1/context': { status: 500, body: { title: 'SqlException: secret' } } });
    renderSelector(contextBody({ companies: [company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)])] }));

    await userEvent.click(enterButton());

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('خطای غیرمنتظره');
    expect(alert).not.toHaveTextContent('Sql');
    expect(screen.queryByRole('button', { name: 'به‌روزرسانی فهرست' })).not.toBeInTheDocument();
  });

  it('shows the cancel button only when there is something to go back to, and the header button signs out', async () => {
    const onCancel = vi.fn();
    const onLogout = vi.fn();
    const context = contextBody({ companies: [company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)])] });
    const { unmount } = renderSelector(context, { onCancel, onLogout });

    await userEvent.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(onCancel).toHaveBeenCalledOnce();
    expect(onLogout).not.toHaveBeenCalled();
    unmount();

    renderSelector(context, { onLogout });
    expect(screen.queryByRole('button', { name: 'انصراف' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'خروج' }));
    expect(onLogout).toHaveBeenCalledOnce();
  });

  it('keeps the enter button looking and acting disabled until a valid year and company are chosen', async () => {
    renderSelector(twoYears());

    expect(enterButton()).toHaveAttribute('aria-disabled', 'true');
    await userEvent.click(screen.getByRole('radio', { name: /سال ۱۴۰۵/ }));
    expect(enterButton()).toHaveAttribute('aria-disabled', 'true');
    await userEvent.click(screen.getByRole('radio', { name: /شرکت الف/ }));
    expect(enterButton()).not.toHaveAttribute('aria-disabled');
    const live = screen.getByText('انتخاب شما:').parentElement;
    expect(live).toHaveAttribute('aria-live', 'polite');
    expect(live).toHaveTextContent(/سال ۱۴۰۵ \(فعال\) · شرکت الف/);
  });

  it('shows the official brand mark and no licence, user-count or subsystem data', () => {
    renderSelector(twoYears());

    const mark = screen.getByRole('img', { name: 'نشان فرانو' });
    expect(mark).toHaveAttribute('src', '/images/faranou-brand-mark.webp');
    expect(mark).toHaveAttribute('width');
    expect(mark).toHaveAttribute('height');
    expect(screen.queryByText(/مجوز/)).not.toBeInTheDocument();
    expect(screen.queryByText(/زیرسیستم/)).not.toBeInTheDocument();
  });

  it('shows status as text with the real period status', async () => {
    renderSelector(
      contextBody({
        companies: [
          company('a', 'شرکت الف', [period('a5', 'سال ۱۴۰۵', y1405)]),
          company('b', 'شرکت ب', [period('b5', 'سال ۱۴۰۵', { ...y1405, ...closed })]),
          company('c', 'شرکت ج', [period('c5', 'سال ۱۴۰۵', { ...y1405, ...archived })]),
        ],
      }),
    );

    // An open period is the normal case and needs no badge; the others say what they are.
    expect(screen.queryByText('باز', { selector: 'span' })).not.toBeInTheDocument();
    expect(screen.getByText('فقط مشاهده و گزارش‌گیری')).toBeInTheDocument();
    expect(screen.getByText('بایگانی‌شده')).toBeInTheDocument();
  });
});
