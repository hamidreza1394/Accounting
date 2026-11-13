import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { clearProductFlash, peekProductFlash } from '../lib/productFlash';
import { freshRowVersion, problem, productDetail, productId, rowVersion } from '../test/tradeFixtures';
import { onUnauthenticated, readyAccess, renderTradePage, stubTradeApi } from '../test/tradeTestUtils';
import { ProductCreatePage } from './ProductCreatePage';
import { ProductEditPage } from './ProductEditPage';

const createdId = '3b1f5c70-9999-4000-8000-00000000b999';
const renderCreate = () =>
  renderTradePage(<ProductCreatePage onUnauthenticated={onUnauthenticated} />, '/trade/products/new', '/trade/products/new', readyAccess(['view', 'create'], 'ReadWrite', 'product'));
const renderEdit = () =>
  renderTradePage(<ProductEditPage onUnauthenticated={onUnauthenticated} />, `/trade/products/${productId}/edit`, '/trade/products/:id/edit', readyAccess(['view', 'update'], 'ReadWrite', 'product'));

const code = () => screen.getByLabelText(/^کد\*?$/);
const name = () => screen.getByLabelText(/^نام\*?$/);
const group = () => screen.getByLabelText(/^کد گروه کالا/);
const hs = () => screen.getByLabelText(/^کد تعرفه/);
const unit = () => screen.getByRole('combobox', { name: /واحد پایه/ });
const submit = () => screen.getByRole('button', { name: 'ثبت کالا' });
const save = () => screen.getByRole('button', { name: 'ذخیرهٔ تغییرات' });

async function chooseUnit(user: ReturnType<typeof userEvent.setup>, text = 'Kilogram') {
  await waitFor(() => expect(unit()).toBeEnabled());
  await user.click(unit());
  await user.click(await screen.findByRole('option', { name: new RegExp(text) }));
}

async function fillValid(user: ReturnType<typeof userEvent.setup>) {
  await user.type(code(), 'PRD-900');
  await user.type(name(), 'کالای تازه');
  await chooseUnit(user);
}

beforeEach(() => {
  onUnauthenticated.mockReset();
  onUnauthenticated.mockReturnValue(false);
  clearProductFlash();
});

describe('ProductCreatePage', () => {
  it('has labelled fields in three sections, required markers and the unit combobox', () => {
    stubTradeApi({});
    renderCreate();
    expect(screen.getByRole('heading', { level: 1, name: 'کالای جدید' })).toBeInTheDocument();
    for (const section of ['مشخصات', 'طبقه‌بندی', 'واحد']) expect(screen.getByRole('heading', { name: section })).toBeInTheDocument();
    expect(code()).toHaveAttribute('aria-required', 'true');
    expect(code()).toHaveAttribute('dir', 'ltr');
    expect(code()).toHaveAttribute('maxlength', '64');
    expect(name()).toHaveAttribute('aria-required', 'true');
    expect(group()).not.toHaveAttribute('aria-required');
    expect(hs()).not.toHaveAttribute('maxlength'); // validateText decides after trim
    expect(hs()).toHaveAttribute('dir', 'ltr');
    expect(unit()).toHaveAttribute('aria-required', 'true');
    // No active toggle: the activation state is not editable (D-083).
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
  });

  it('validates on the client without any request and moves focus to the first invalid field', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({});
    renderCreate();
    await user.click(submit());
    expect(await screen.findByText('کد الزامی است.')).toBeInTheDocument();
    expect(screen.getByText('نام الزامی است.')).toBeInTheDocument();
    expect(screen.getByText('واحد پایه الزامی است.')).toBeInTheDocument();
    expect(code()).toHaveAttribute('aria-invalid', 'true');
    expect(code()).toHaveAccessibleDescription(/کد الزامی است\./);
    await waitFor(() => expect(code()).toHaveFocus());
    expect(writes()).toHaveLength(0);
  });

  it('rejects a non-ASCII HS code on the client and clears the error when the field is edited', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({});
    renderCreate();
    await fillValid(user);
    await user.type(hs(), '۱۰۰۱');
    await user.click(submit());
    expect(await screen.findByText(/کد تعرفه فقط می‌تواند شامل نویسه‌های لاتین/)).toBeInTheDocument();
    expect(hs()).toHaveAttribute('aria-invalid', 'true');
    expect(writes()).toHaveLength(0);
    await user.type(hs(), '1');
    expect(screen.queryByText(/نویسه‌های لاتین/)).not.toBeInTheDocument();
  });

  it('accepts a pasted HS code with surrounding spaces (no maxLength truncation) and sends it trimmed', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({ 'POST /api/v1/trade/products': { status: 201, body: productDetail({ id: createdId }) } });
    renderCreate();
    await fillValid(user);
    await user.click(hs());
    await user.paste(' 0101.21  ');
    expect(hs()).toHaveValue(' 0101.21  ');
    await user.click(submit());
    await waitFor(() => expect(writes()).toHaveLength(1));
    expect(writes()[0]?.body).toMatchObject({ hsCode: '0101.21' });
  });

  it('creates the product with the exact body (HS code as text), then opens the details with a notice', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({ 'POST /api/v1/trade/products': { status: 201, body: productDetail({ id: createdId, code: 'PRD-900' }) } });
    renderCreate();
    await fillValid(user);
    await user.type(group(), '  GRAIN  ');
    await user.type(hs(), ' 0101.21 ');
    await user.click(submit());
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/products/${createdId}`));
    expect(writes()).toHaveLength(1);
    expect(writes()[0]?.body).toEqual({
      code: 'PRD-900',
      name: 'کالای تازه',
      productGroupCode: 'GRAIN',
      hsCode: '0101.21',
      baseUnitCode: 'KGM',
    });
    expect(peekProductFlash(createdId)).toBe('created');
  });

  it('sends null for empty optional fields and never an isActive or tenant field', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({ 'POST /api/v1/trade/products': { status: 201, body: productDetail({ id: createdId }) } });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    await waitFor(() => expect(writes()).toHaveLength(1));
    expect(writes()[0]?.body).toEqual({ code: 'PRD-900', name: 'کالای تازه', productGroupCode: null, hsCode: null, baseUnitCode: 'KGM' });
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
    (globalThis as { fetch: typeof fetch }).fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST' && String(input).endsWith('/trade/products')) {
        posts += 1;
        await gate;
        return new Response(JSON.stringify(productDetail({ id: createdId })), { status: 201, headers: { 'Content-Type': 'application/json' } });
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
    release();
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/products/${createdId}`));
    expect(posts).toBe(1);
  });

  it('maps a duplicate code (409) to the code field, keeps every entered value and focuses the field', async () => {
    const user = userEvent.setup();
    stubTradeApi({ 'POST /api/v1/trade/products': problem(409, 'trade_product_code_duplicate', 'کد کالا تکراری است.') });
    renderCreate();
    await fillValid(user);
    await user.type(hs(), '1001');
    await user.click(submit());
    expect(await screen.findByText(/این کد پیش‌تر برای کالای دیگری ثبت شده است/)).toBeInTheDocument();
    expect(code()).toHaveAttribute('aria-invalid', 'true');
    await waitFor(() => expect(code()).toHaveFocus());
    expect(code()).toHaveValue('PRD-900');
    expect(name()).toHaveValue('کالای تازه');
    expect(hs()).toHaveValue('1001');
    expect(unit()).toHaveValue('KGM — کیلوگرم');
    expect(submit()).toBeEnabled();
  });

  it('maps server field errors of a 400 ValidationProblem (camelCase keys) under their fields and keeps the data', async () => {
    const user = userEvent.setup();
    stubTradeApi({
      'POST /api/v1/trade/products': {
        status: 400,
        body: {
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: { name: ['نام الزامی است.'], hsCode: ['کد تعرفه نباید بیش از ۱۶ نویسه باشد.'], baseUnitCode: ['واحد پایه نامعتبر است.'] },
        },
      },
    });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByText('کد تعرفه نباید بیش از ۱۶ نویسه باشد.')).toBeInTheDocument();
    expect(screen.getByText('واحد پایه نامعتبر است.')).toBeInTheDocument();
    expect(name()).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('برخی فیلدها نامعتبرند');
    expect(code()).toHaveValue('PRD-900');
    expect(unit()).toHaveValue('KGM — کیلوگرم');
  });

  it.each([
    ['trade_context_required', 409, 'شرکت یا دورهٔ مالی کاری معتبر نیست'],
    ['trade_module_unavailable', 403, 'ماژول بازرگانی در دسترس نیست'],
    ['trade_forbidden', 403, 'شما مجوز انجام این عملیات را ندارید'],
    ['trade_request_too_large', 413, 'حجم اطلاعات ارسالی بیش از حد مجاز است'],
    ['trade_invalid_request_body', 400, 'درخواست قابل پردازش نبود'],
  ])('shows a clear Persian message for %s and keeps the form data', async (detail, status, message) => {
    const user = userEvent.setup();
    stubTradeApi({ 'POST /api/v1/trade/products': problem(status, detail) });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(code()).toHaveValue('PRD-900');
    expect(name()).toHaveValue('کالای تازه');
    expect(submit()).toBeEnabled();
    expect(window.location.pathname).toBe('/app/trade/products/new');
  });

  it('shows a server error (500) without losing data and allows a retry', async () => {
    const user = userEvent.setup();
    let attempt = 0;
    stubTradeApi({
      'POST /api/v1/trade/products': () => {
        attempt += 1;
        if (attempt === 1) return { status: 500 };
        return { status: 201, body: productDetail({ id: createdId }) };
      },
    });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(name()).toHaveValue('کالای تازه');
    await user.click(submit());
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/products/${createdId}`));
  });

  it('shows a network error without losing data', async () => {
    const user = userEvent.setup();
    stubTradeApi({
      'POST /api/v1/trade/products': () => {
        throw new TypeError('Failed to fetch');
      },
    });
    renderCreate();
    await fillValid(user);
    await user.click(submit());
    expect(await screen.findByText(/ارتباط با سرور برقرار نشد/)).toBeInTheDocument();
    expect(name()).toHaveValue('کالای تازه');
  });

  it('leaves a clean form without a dialog, and asks before discarding unsaved changes', async () => {
    const user = userEvent.setup();
    stubTradeApi({});
    const first = renderCreate();
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    expect(window.location.pathname).toBe('/app/trade/products');
    first.unmount();
    renderCreate();
    await user.type(code(), 'P-1');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'لغو تغییرات و بازگشت' });
    await user.click(within(dialog).getByRole('button', { name: 'ادامهٔ ویرایش' }));
    expect(code()).toHaveValue('P-1');
    expect(window.location.pathname).toBe('/app/trade/products/new');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'لغو تغییرات و بازگشت' }));
    expect(window.location.pathname).toBe('/app/trade/products');
  });

  it('chooses the unit by Persian name with the keyboard, then clears it', async () => {
    const user = userEvent.setup();
    stubTradeApi({});
    renderCreate();
    await waitFor(() => expect(unit()).toBeEnabled());
    await user.type(unit(), 'متر');
    await user.keyboard('{Enter}');
    expect(unit()).toHaveValue('MTR — متر');
    await user.click(screen.getByRole('button', { name: 'پاک‌کردن واحد' }));
    expect(unit()).toHaveValue('');
  });

  it('searches the unit by code and by English name', async () => {
    const user = userEvent.setup();
    stubTradeApi({});
    renderCreate();
    await waitFor(() => expect(unit()).toBeEnabled());
    await user.type(unit(), 'TNE');
    expect(await screen.findByRole('option', { name: /تن/ })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /کیلوگرم/ })).not.toBeInTheDocument();
  });

  it('keeps the form usable and offers a retry when the unit reference fails to load', async () => {
    const user = userEvent.setup();
    let attempt = 0;
    stubTradeApi({ 'GET /api/v1/trade/reference/units': () => (++attempt === 1 ? { status: 500 } : { status: 200, body: [{ code: 'KGM', nameFa: 'کیلوگرم', nameEn: 'Kilogram' }] }) });
    renderCreate();
    expect(await screen.findByText(/دریافت فهرست واحدها انجام نشد/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    await waitFor(() => expect(screen.queryByText(/دریافت فهرست واحدها انجام نشد/)).not.toBeInTheDocument());
    await chooseUnit(user);
    expect(unit()).toHaveValue('KGM — کیلوگرم');
  });
});

describe('ProductEditPage', () => {
  const detailRoute = { [`GET /api/v1/trade/products/${productId}`]: { status: 200, body: productDetail() } };

  it('loads the product into the form (including the unit) with a clean state', async () => {
    stubTradeApi(detailRoute);
    renderEdit();
    expect(await screen.findByRole('heading', { level: 1, name: 'ویرایش کالا' })).toBeInTheDocument();
    expect(code()).toHaveValue('PRD-001');
    expect(name()).toHaveValue('گندم دوروم');
    expect(group()).toHaveValue('GRAIN');
    expect(hs()).toHaveValue('1001.19');
    await waitFor(() => expect(unit()).toHaveValue('KGM — کیلوگرم'));
  });

  it('PUTs the full replacement with the rowVersion of the last read, then opens the details with a notice', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({ ...detailRoute, [`PUT /api/v1/trade/products/${productId}`]: { status: 200, body: productDetail({ rowVersion: freshRowVersion }) } });
    renderEdit();
    await screen.findByDisplayValue('PRD-001');
    await user.clear(name());
    await user.type(name(), 'نام جدید');
    await user.clear(hs());
    await user.click(save());
    await waitFor(() => expect(window.location.pathname).toBe(`/app/trade/products/${productId}`));
    expect(writes()[0]?.body).toEqual({
      code: 'PRD-001',
      name: 'نام جدید',
      productGroupCode: 'GRAIN',
      hsCode: null,
      baseUnitCode: 'KGM',
      rowVersion,
    });
    expect(peekProductFlash(productId)).toBe('updated');
  });

  function conflictApi(options: { refresh?: 'ok' | 'fail' } = {}) {
    let reads = 0;
    return stubTradeApi({
      [`GET /api/v1/trade/products/${productId}`]: () => {
        reads += 1;
        if (reads === 1) return { status: 200, body: productDetail() };
        return options.refresh === 'fail'
          ? { status: 500 }
          : { status: 200, body: productDetail({ name: 'نام تازهٔ سرور', rowVersion: freshRowVersion }) };
      },
      [`PUT /api/v1/trade/products/${productId}`]: problem(409, 'trade_concurrency_conflict'),
    });
  }

  async function causeConflict(user: ReturnType<typeof userEvent.setup>) {
    await screen.findByDisplayValue('PRD-001');
    await user.clear(name());
    await user.type(name(), 'ویرایش من');
    await user.click(save());
    return screen.findByRole('alert');
  }

  it('on a concurrency conflict (keep mine) the form stays mounted with every typed value and only the rowVersion moves on', async () => {
    const user = userEvent.setup();
    const { writes } = conflictApi();
    renderEdit();
    const alert = await causeConflict(user);
    expect(alert).toHaveTextContent('این کالا');
    expect(alert).toHaveTextContent('توسط کاربر دیگری تغییر کرده است');
    const formElement = name().closest('form');
    await user.click(within(alert).getByRole('button', { name: 'دریافت نسخهٔ تازه و حفظ ویرایش‌های من' }));
    expect(await screen.findByText(/مقادیر شما حفظ شده است؛ ذخیره‌کردن، تغییرات آن کاربر را بازنویسی می‌کند/)).toBeInTheDocument();
    expect(name().closest('form')).toBe(formElement);
    expect(name()).toHaveValue('ویرایش من');
    await user.click(save());
    await waitFor(() => expect(writes()).toHaveLength(2));
    expect(writes()[0]?.body).toMatchObject({ rowVersion });
    expect(writes()[1]?.body).toMatchObject({ rowVersion: freshRowVersion, name: 'ویرایش من' });
  });

  it('keeps the form and shows an inline alert when the refresh itself fails', async () => {
    const user = userEvent.setup();
    conflictApi({ refresh: 'fail' });
    renderEdit();
    const alert = await causeConflict(user);
    await user.click(within(alert).getByRole('button', { name: 'دریافت نسخهٔ تازه و حفظ ویرایش‌های من' }));
    expect(await screen.findByText(/دریافت نسخهٔ تازه انجام نشد؛ فرم و مقادیر شما حفظ شد/)).toBeInTheDocument();
    expect(name()).toHaveValue('ویرایش من');
    expect(save()).toBeEnabled();
  });

  it('shows the fresh values only after an explicit confirmation (discard mine); cancelling keeps the edits', async () => {
    const user = userEvent.setup();
    conflictApi();
    renderEdit();
    const alert = await causeConflict(user);
    await user.click(within(alert).getByRole('button', { name: 'نمایش مقادیر تازه' }));
    let dialog = await screen.findByRole('alertdialog', { name: 'نمایش مقادیر تازه' });
    await user.click(within(dialog).getByRole('button', { name: 'ادامهٔ ویرایش' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(name()).toHaveValue('ویرایش من');
    await user.click(within(screen.getByRole('alert')).getByRole('button', { name: 'نمایش مقادیر تازه' }));
    dialog = await screen.findByRole('alertdialog', { name: 'نمایش مقادیر تازه' });
    await user.click(within(dialog).getByRole('button', { name: 'کنار گذاشتن ویرایش‌های من' }));
    await waitFor(() => expect(name()).toHaveValue('نام تازهٔ سرور'));
    expect(screen.getByText(/مقادیر تازهٔ ثبت‌شده در سرور نمایش داده شد/)).toBeInTheDocument();
  });

  it('after "discard mine" the next save sends the fresh rowVersion with the fresh values', async () => {
    const user = userEvent.setup();
    const { writes } = conflictApi();
    renderEdit();
    const alert = await causeConflict(user);
    await user.click(within(alert).getByRole('button', { name: 'نمایش مقادیر تازه' }));
    const dialog = await screen.findByRole('alertdialog', { name: 'نمایش مقادیر تازه' });
    await user.click(within(dialog).getByRole('button', { name: 'کنار گذاشتن ویرایش‌های من' }));
    await waitFor(() => expect(name()).toHaveValue('نام تازهٔ سرور'));
    await user.click(save());
    await waitFor(() => expect(writes()).toHaveLength(2));
    expect(writes()[1]?.body).toMatchObject({ rowVersion: freshRowVersion, name: 'نام تازهٔ سرور' });
  });

  it('shows the invalid-unit message and sends nothing when the stored unit is missing from the units list', async () => {
    const user = userEvent.setup();
    const { writes } = stubTradeApi({ [`GET /api/v1/trade/products/${productId}`]: { status: 200, body: productDetail({ baseUnitCode: 'ZZZ' }) } });
    renderEdit();
    await screen.findByDisplayValue('PRD-001');
    await waitFor(() => expect(unit()).toBeEnabled());
    await user.click(save());
    expect(await screen.findByText('واحد پایه نامعتبر است.')).toBeInTheDocument();
    expect(writes()).toHaveLength(0);
  });

  it('maps a duplicate code on update to the code field and keeps the values', async () => {
    const user = userEvent.setup();
    stubTradeApi({ ...detailRoute, [`PUT /api/v1/trade/products/${productId}`]: problem(409, 'trade_product_code_duplicate') });
    renderEdit();
    await screen.findByDisplayValue('PRD-001');
    await user.clear(code());
    await user.type(code(), 'PRD-002');
    await user.click(save());
    expect(await screen.findByText(/این کد پیش‌تر برای کالای دیگری ثبت شده است/)).toBeInTheDocument();
    expect(code()).toHaveValue('PRD-002');
  });

  it('shows not-found for a missing product and the unavailable state for the module', async () => {
    stubTradeApi({ [`GET /api/v1/trade/products/${productId}`]: { status: 404 } });
    const missing = renderEdit();
    expect(await screen.findByRole('heading', { name: 'کالا پیدا نشد' })).toBeInTheDocument();
    missing.unmount();
    stubTradeApi({ [`GET /api/v1/trade/products/${productId}`]: problem(403, 'trade_module_unavailable') });
    renderEdit();
    expect(await screen.findByRole('heading', { name: 'ماژول بازرگانی در دسترس نیست' })).toBeInTheDocument();
  });

  it('cancels back to the details, asking first when something changed', async () => {
    const user = userEvent.setup();
    stubTradeApi(detailRoute);
    renderEdit();
    await screen.findByDisplayValue('PRD-001');
    await user.type(group(), 'x');
    await user.click(screen.getByRole('button', { name: 'انصراف' }));
    await user.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'لغو تغییرات و بازگشت' }));
    expect(window.location.pathname).toBe(`/app/trade/products/${productId}`);
  });
});
