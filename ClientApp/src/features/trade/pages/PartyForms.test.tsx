import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { clearPartyFlash, peekPartyFlash } from '../lib/partyFlash';
import { freshRowVersion, partyDetail, partyId, problem, rowVersion } from '../test/tradeFixtures';
import { onUnauthenticated, readyAccess, renderTradePage, stubTradeApi } from '../test/tradeTestUtils';
import { PartyCreatePage } from './PartyCreatePage';
import { PartyEditPage } from './PartyEditPage';

const createdId = '3b1f5c70-9999-4000-8000-00000000a999';
const renderCreate = () => renderTradePage(<PartyCreatePage onUnauthenticated={onUnauthenticated} />, '/trade/parties/new', '/trade/parties/new');
const renderEdit = () =>
  renderTradePage(<PartyEditPage onUnauthenticated={onUnauthenticated} />, `/trade/parties/${partyId}/edit`, '/trade/parties/:id/edit', readyAccess());

const code = () => screen.getByLabelText(/^کد\*?$/);
const legalName = () => screen.getByLabelText(/^نام حقوقی/);
const displayName = () => screen.getByLabelText(/^نام نمایشی/);
const identifier = () => screen.getByLabelText(/^شناسهٔ حقوقی/);
const submit = () => screen.getByRole('button', { name: 'ثبت طرف‌حساب' });
const role = (name: string) => screen.getByRole('checkbox', { name });

async function fillValid(user: ReturnType<typeof userEvent.setup>) {
  await user.type(code(), 'P-900');
  await user.type(legalName(), 'شرکت تازه');
  await user.click(role('فروشنده'));
}

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
  clearPartyFlash();
});

describe('PartyCreatePage', () => {
  it('has labelled fields in three sections, required markers and a named roles group', () => {
    stubTradeApi({});
    renderCreate();
    expect(screen.getByRole('heading', { level: 1, name: 'طرف‌حساب جدید' })).toBeInTheDocument();
    for (const section of ['هویت', 'نقش‌ها', 'کشور']) expect(screen.getByRole('heading', { name: section })).toBeInTheDocument();
    expect(code()).toHaveAttribute('aria-required', 'true');
    expect(code()).toHaveAttribute('dir', 'ltr');
    expect(code()).toHaveAttribute('maxlength', '64');
    expect(legalName()).toHaveAttribute('aria-required', 'true');
    expect(displayName()).not.toHaveAttribute('aria-required');
    expect(screen.getByRole('group', { name: /نقش‌های طرف‌حساب/ })).toBeInTheDocument();
    expect(screen.getAllByRole('checkbox')).toHaveLength(9);
    expect(role('کارگزار ارز')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'کشور' })).toBeInTheDocument();
  });

  it('validates on the client without any request and moves focus to the first invalid field', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({});
    renderCreate();
    await user.click(submit());
    expect(await screen.findByText('کد الزامی است.')).toBeInTheDocument();
    expect(screen.getByText('نام حقوقی الزامی است.')).toBeInTheDocument();
    expect(screen.getByText('دست‌کم یک نقش انتخاب کنید.')).toBeInTheDocument();
    expect(code()).toHaveAttribute('aria-invalid', 'true');
    expect(code()).toHaveAccessibleDescription(/کد الزامی است\./);
    await waitFor(() => expect(code()).toHaveFocus());
    expect(writes()).toHaveLength(0);
  });

  it('clears the error of a field as soon as the user edits it', async () => {
    const user = userEvent.setup();
    stubTradeApi({});
    renderCreate();
    await user.click(submit());
    await screen.findByText('کد الزامی است.');
    await user.type(code(), 'x');
    expect(screen.queryByText('کد الزامی است.')).not.toBeInTheDocument();
  });

  it('creates the party with the exact body, then opens the details with a success notice and refreshes the list', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({ 'POST /api/v1/trade/parties': { status: 201, body: partyDetail({ id: createdId, code: 'P-900' }) } });
    renderCreate();
    await fillValid(user);
    await user.type(displayName(), '  نام نمایشی  ');
    await user.type(identifier(), '123456');
    await user.click(role('واردکننده'));
    await user.click(screen.getByRole('combobox', { name: 'کشور' }));
    await user.click(await screen.findByRole('option', { name: /Germany/ }));
    await user.click(submit());
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/parties/${createdId}`));
    expect(writes()).toHaveLength(1);
    expect(writes()[0]?.body).toEqual({
      code: 'P-900',
      legalName: 'شرکت تازه',
      displayName: 'نام نمایشی',
      legalIdentifier: '123456',
      countryCode: 'DE',
      roles: ['Importer', 'Seller'],
    });
    expect(peekPartyFlash(createdId)).toBe('created');
    // The legal identifier never reaches the URL.
    expect(window.location.href).not.toContain('123456');
  });

  it('disables submit and the fields while saving and ignores a second submit', async () => {
    const user = userEvent.setup();
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    let posts = 0;
    stubTradeApi({});
    const original = globalThis.fetch;
    // A pending create: the POST resolves only when released.
    (globalThis as { fetch: typeof fetch }).fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST' && String(input).endsWith('/trade/parties')) {
        posts += 1;
        await gate;
        return new Response(JSON.stringify(partyDetail({ id: createdId })), { status: 201, headers: { 'Content-Type': 'application/json' } });
      }
      return original(input, init);
    }) as typeof fetch;
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    const busy = await screen.findByRole('button', { name: 'در حال ثبت…' });
    expect(busy).toBeDisabled();
    expect(code()).toBeDisabled();
    expect(screen.getByRole('button', { name: 'انصراف' })).toBeDisabled();
    expect(screen.getByText(/ذخیره در حال انجام است/)).toBeInTheDocument();
    release();
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/parties/${createdId}`));
    expect(posts).toBe(1);
  });

  it('maps a duplicate code (409) to the code field, keeps every entered value and focuses the field', async () => {
    const user = userEvent.setup();
    stubTradeApi({ 'POST /api/v1/trade/parties': problem(409, 'trade_party_code_duplicate', 'کد طرف‌حساب تکراری است.') });
    renderCreate();
    await fillValid(user);
    await user.type(displayName(), 'نام من');
    await user.click(submit());
    expect(await screen.findByText(/این کد پیش‌تر برای طرف‌حساب دیگری ثبت شده است/)).toBeInTheDocument();
    expect(code()).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(code()).toHaveFocus());
    expect(code()).toHaveValue('P-900');
    expect(legalName()).toHaveValue('شرکت تازه');
    expect(displayName()).toHaveValue('نام من');
    expect(role('فروشنده')).toBeChecked();
    expect(submit()).toBeEnabled();
  });

  it('maps server field errors of a 400 ValidationProblem (camelCase keys) under their fields', async () => {
    const user = userEvent.setup();
    stubTradeApi({
      'POST /api/v1/trade/parties': {
        status: 400,
        body: { title: 'One or more validation errors occurred.', status: 400, errors: { legalName: ['نام حقوقی الزامی است.'], countryCode: ['کد کشور نامعتبر است.'] } },
      },
    });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByText('کد کشور نامعتبر است.')).toBeInTheDocument();
    expect(legalName()).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('برخی فیلدها نامعتبرند');
    expect(code()).toHaveValue('P-900');
  });

  it.each([
    ['trade_period_read_only', 409, 'دورهٔ مالی جاری فقط‌خواندنی است'],
    ['trade_context_required', 409, 'شرکت یا دورهٔ مالی کاری معتبر نیست'],
    ['trade_module_unavailable', 403, 'ماژول بازرگانی در دسترس نیست'],
    ['trade_forbidden', 403, 'شما مجوز انجام این عملیات را ندارید'],
    ['trade_request_too_large', 413, 'حجم اطلاعات ارسالی بیش از حد مجاز است'],
    ['trade_invalid_request_body', 400, 'درخواست قابل پردازش نبود'],
  ])('shows a clear Persian message for %s and keeps the form data', async (detail, status, message) => {
    const user = userEvent.setup();
    stubTradeApi({ 'POST /api/v1/trade/parties': problem(status, detail) });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(code()).toHaveValue('P-900');
    expect(submit()).toBeEnabled();
    expect(window.location.pathname).toBe('/app/trade/parties/new');
  });

  it('shows a network error without losing data and allows a retry', async () => {
    const user = userEvent.setup();
    let attempt = 0;
    stubTradeApi({
      'POST /api/v1/trade/parties': () => {
        attempt += 1;
        if (attempt === 1) throw new TypeError('Failed to fetch');
        return { status: 201, body: partyDetail({ id: createdId }) };
      },
    });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByText(/ارتباط با سرور برقرار نشد/)).toBeInTheDocument();
    expect(legalName()).toHaveValue('شرکت تازه');
    await user.click(submit());
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/parties/${createdId}`));
  });

  it('leaves a clean form without a dialog', async () => {
    const user = userEvent.setup();
    stubTradeApi({});
    renderCreate();
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(window.location.pathname).toBe('/app/trade/parties');
  });

  it('asks before discarding unsaved changes: continue keeps the form, discard leaves', async () => {
    const user = userEvent.setup();
    stubTradeApi({});
    renderCreate();
    await user.type(code(), 'P-1');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'لغو تغییرات و بازگشت' });
    await user.click(within(dialog).getByRole('button', { name: 'ادامهٔ ویرایش' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(code()).toHaveValue('P-1');
    expect(window.location.pathname).toBe('/app/trade/parties/new');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'لغو تغییرات و بازگشت' }));
    expect(window.location.pathname).toBe('/app/trade/parties');
  });

  it('chooses and clears the country with the keyboard', async () => {
    const user = userEvent.setup();
    stubTradeApi({ 'POST /api/v1/trade/parties': { status: 201, body: partyDetail({ id: createdId }) } });
    renderCreate();
    await fillValid(user);
    const country = screen.getByRole('combobox', { name: 'کشور' });
    await waitFor(() => expect(country).toBeEnabled());
    await user.type(country, 'چین');
    await user.keyboard('{Enter}');
    expect(country).toHaveValue('CN — چین');
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن کشور' }));
    expect(country).toHaveValue('');
  });
});

describe('PartyEditPage', () => {
  const detailRoute = { [`GET /api/v1/trade/parties/${partyId}`]: { status: 200, body: partyDetail() } };

  it('loads the party into the form (including the identifier and the country) with a clean state', async () => {
    stubTradeApi(detailRoute);
    renderEdit();
    expect(await screen.findByRole('heading', { level: 1, name: 'ویرایش طرف‌حساب' })).toBeInTheDocument();
    expect(code()).toHaveValue('P-001');
    expect(legalName()).toHaveValue('شرکت بازرگانی نمونه');
    expect(identifier()).toHaveValue('14001234567');
    expect(role('واردکننده')).toBeChecked();
    expect(role('فروشنده')).toBeChecked();
    expect(role('بانک')).not.toBeChecked();
    await waitFor(() => expect(screen.getByRole('combobox', { name: 'کشور' })).toHaveValue('IR — ایران'));
  });

  it('PUTs the full replacement with the rowVersion of the last read, then opens the details with a notice', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({ ...detailRoute, [`PUT /api/v1/trade/parties/${partyId}`]: { status: 200, body: partyDetail({ rowVersion: freshRowVersion }) } });
    renderEdit();
    await screen.findByDisplayValue('P-001');
    await user.click(role('بانک'));
    await user.click(role('واردکننده'));
    await user.clear(legalName());
    await user.type(legalName(), 'نام حقوقی جدید');
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/parties/${partyId}`));
    expect(writes()[0]?.body).toEqual({
      code: 'P-001',
      legalName: 'نام حقوقی جدید',
      displayName: 'نمونه',
      legalIdentifier: '14001234567',
      countryCode: 'IR',
      roles: ['Seller', 'Bank'],
      rowVersion,
    });
    expect(peekPartyFlash(partyId)).toBe('updated');
  });

  function conflictApi(options: { refresh?: 'ok' | 'fail' } = {}) {
    let reads = 0;
    return stubTradeApi({
      [`GET /api/v1/trade/parties/${partyId}`]: () => {
        reads += 1;
        if (reads === 1) return { status: 200, body: partyDetail() };
        return options.refresh === 'fail'
          ? { status: 500 }
          : { status: 200, body: partyDetail({ legalName: 'نام تازهٔ سرور', rowVersion: freshRowVersion }) };
      },
      [`PUT /api/v1/trade/parties/${partyId}`]: problem(409, 'trade_concurrency_conflict'),
    });
  }

  async function causeConflict(user: ReturnType<typeof userEvent.setup>) {
    await screen.findByDisplayValue('P-001');
    await user.clear(legalName());
    await user.type(legalName(), 'ویرایش من');
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    return screen.findByRole('alert');
  }

  it('on a concurrency conflict the refresh keeps the form mounted and every typed value, and only the rowVersion moves on', async () => {
    const user = userEvent.setup();
    const { writes } = conflictApi();
    renderEdit();
    const alert = await causeConflict(user);
    expect(alert).toHaveTextContent('توسط کاربر دیگری تغییر کرده است');
    const formElement = legalName().closest('form');
    await user.click(within(alert).getByRole('button', { name: 'دریافت نسخهٔ تازه و حفظ ویرایش‌های من' }));
    expect(await screen.findByText(/مقادیر شما حفظ شده است؛ ذخیره‌کردن، تغییرات آن کاربر را بازنویسی می‌کند/)).toBeInTheDocument();
    // Same DOM node (never unmounted), same typed text, no loading state in between.
    expect(legalName().closest('form')).toBe(formElement);
    expect(legalName()).toHaveValue('ویرایش من');
    expect(screen.queryByText(/توسط کاربر دیگری تغییر کرده است\.\s*اطلاعات تازه/)).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    await waitFor(() => expect(writes()).toHaveLength(2));
    expect(writes()[0]?.body).toMatchObject({ rowVersion });
    expect(writes()[1]?.body).toMatchObject({ rowVersion: freshRowVersion, legalName: 'ویرایش من' });
  });

  it('keeps the form and shows an inline alert when the refresh itself fails', async () => {
    const user = userEvent.setup();
    conflictApi({ refresh: 'fail' });
    renderEdit();
    const alert = await causeConflict(user);
    await user.click(within(alert).getByRole('button', { name: 'دریافت نسخهٔ تازه و حفظ ویرایش‌های من' }));
    expect(await screen.findByText(/دریافت نسخهٔ تازه انجام نشد؛ فرم و مقادیر شما حفظ شد/)).toBeInTheDocument();
    expect(legalName()).toHaveValue('ویرایش من');
    expect(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' })).toBeEnabled();
    expect(screen.queryByText('در حال بارگذاری طرف‌حساب…')).not.toBeInTheDocument();
  });

  it('shows the fresh values only after an explicit confirmation; cancelling keeps the edits', async () => {
    const user = userEvent.setup();
    conflictApi();
    renderEdit();
    const alert = await causeConflict(user);
    await user.click(within(alert).getByRole('button', { name: 'نمایش مقادیر تازه' }));
    let dialog = await screen.findByRole('alertdialog', { name: 'نمایش مقادیر تازه' });
    await user.click(within(dialog).getByRole('button', { name: 'ادامهٔ ویرایش' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(legalName()).toHaveValue('ویرایش من');
    await user.click(within(screen.getByRole('alert')).getByRole('button', { name: 'نمایش مقادیر تازه' }));
    dialog = await screen.findByRole('alertdialog', { name: 'نمایش مقادیر تازه' });
    await user.click(within(dialog).getByRole('button', { name: 'کنار گذاشتن ویرایش‌های من' }));
    await waitFor(() => expect(legalName()).toHaveValue('نام تازهٔ سرور'));
    expect(screen.getByText(/مقادیر تازهٔ ثبت‌شده در سرور نمایش داده شد/)).toBeInTheDocument();
  });

  it('maps a duplicate code on update to the code field', async () => {
    const user = userEvent.setup();
    stubTradeApi({ ...detailRoute, [`PUT /api/v1/trade/parties/${partyId}`]: problem(409, 'trade_party_code_duplicate') });
    renderEdit();
    await screen.findByDisplayValue('P-001');
    await user.clear(code());
    await user.type(code(), 'P-002');
    await user.click(screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' }));
    expect(await screen.findByText(/این کد پیش‌تر برای طرف‌حساب دیگری ثبت شده است/)).toBeInTheDocument();
    expect(code()).toHaveValue('P-002');
  });

  it('shows not-found for a missing, foreign or deleted party and the unavailable state for the module', async () => {
    stubTradeApi({ [`GET /api/v1/trade/parties/${partyId}`]: { status: 404 } });
    const missing = renderEdit();
    expect(await screen.findByRole('heading', { name: 'طرف‌حساب پیدا نشد' })).toBeInTheDocument();
    missing.unmount();
    stubTradeApi({ [`GET /api/v1/trade/parties/${partyId}`]: problem(403, 'trade_module_unavailable') });
    renderEdit();
    expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
  });

  it('cancels back to the details, asking first when something changed', async () => {
    const user = userEvent.setup();
    stubTradeApi(detailRoute);
    renderEdit();
    await screen.findByDisplayValue('P-001');
    await user.type(displayName(), 'x');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'لغو تغییرات و بازگشت' }));
    expect(window.location.pathname).toBe(`/app/trade/parties/${partyId}`);
  });
});
