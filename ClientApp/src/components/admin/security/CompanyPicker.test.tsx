import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { json, stubApi, type ApiCall } from '../../../test/adminUsers';
import { adminCompanyItem, adminCompanyPage } from '../../../test/securityAudit';
import { CompanyPicker } from './CompanyPicker';

const onUnauthenticated = vi.fn((): boolean => false);
const companiesPath = '/api/v1/admin/companies';

const setup = (value: Parameters<typeof CompanyPicker>[0]['value'] = null, onChange = vi.fn()) => {
  render(<CompanyPicker value={value} onChange={onChange} onUnauthenticated={onUnauthenticated} />);
  return { onChange };
};

/** The picker input; it has no field label of its own, the page names it. */
const comboInput = () => screen.getByRole('combobox');

const companyUrls = (stub: ReturnType<typeof stubApi>) =>
  stub.mock.mock.calls.map((call) => new URL(String(call[0]), 'http://localhost')).filter((url) => url.pathname === companiesPath);

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
});
afterEach(() => vi.unstubAllEnvs());

describe('CompanyPicker', () => {
  it('loads every company of the installation from the admin endpoint on open, inactive ones marked', async () => {
    const user = userEvent.setup();
    const stub = stubApi((call: ApiCall) =>
      call.path === companiesPath
        ? json(adminCompanyPage([adminCompanyItem('a', 'شرکت الف'), adminCompanyItem('b', 'شرکت ب'), adminCompanyItem('c', 'شرکت قدیمی', false)], { totalCount: 3 }))
        : new Response(null, { status: 404 }),
    );
    setup();
    await user.click(comboInput());
    expect(await screen.findByRole('option', { name: 'شرکت الف' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /شرکت قدیمی/ })).toHaveTextContent('(غیرفعال)');
    expect(companyUrls(stub)[0]?.searchParams.get('pageSize')).toBe('20');
  });

  it('searches on the server as the user types', async () => {
    const user = userEvent.setup();
    const stub = stubApi((call: ApiCall) =>
      call.path === companiesPath ? json(adminCompanyPage([adminCompanyItem('b', 'شرکت ب')])) : new Response(null, { status: 404 }),
    );
    setup();
    const combo = comboInput();
    await user.click(combo);
    await screen.findByRole('option', { name: 'شرکت ب' });
    await user.type(combo, 'ب');
    await waitFor(
      () => {
        const searched = companyUrls(stub).filter((url) => url.searchParams.has('search'));
        expect(searched.at(-1)?.searchParams.get('search')).toBe('ب');
      },
      { timeout: 2000 },
    );
  });

  it('chooses a company and can remove it again', async () => {
    const user = userEvent.setup();
    stubApi((call: ApiCall) => (call.path === companiesPath ? json(adminCompanyPage([adminCompanyItem('b', 'شرکت ب')])) : new Response(null, { status: 404 })));
    const onChange = vi.fn();
    setup(null, onChange);
    const combo = comboInput();
    await user.click(combo);
    await user.click(await screen.findByRole('option', { name: 'شرکت ب' }));
    expect(onChange).toHaveBeenCalledWith({ id: 'b', label: 'شرکت ب' });

    setup({ id: 'b', label: 'شرکت ب' }, onChange);
    await user.click(screen.getByRole('button', { name: 'حذف شرکت انتخاب‌شده شرکت ب' }));
    expect(onChange).toHaveBeenLastCalledWith(null);
  });

  it('shows a search error for a non-administrator (403) without disabling the field', async () => {
    const user = userEvent.setup();
    stubApi((call: ApiCall) => (call.path === companiesPath ? new Response(null, { status: 403 }) : new Response(null, { status: 404 })));
    setup();
    const combo = comboInput();
    await user.click(combo);
    expect(await screen.findByText('فهرست همهٔ شرکت‌های نصب فقط برای مدیر نصب در دسترس است.')).toBeInTheDocument();
    expect(combo).toBeEnabled();
  });

  it('hands a 401 to the unauthenticated flow', async () => {
    const user = userEvent.setup();
    stubApi((call: ApiCall) => (call.path === companiesPath ? new Response(null, { status: 401 }) : new Response(null, { status: 404 })));
    setup();
    await user.click(comboInput());
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
  });
});
