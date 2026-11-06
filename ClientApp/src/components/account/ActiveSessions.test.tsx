import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { json, stubApi, type ApiCall } from '../../test/adminUsers';
import { ActiveSessions } from './ActiveSessions';

const onUnauthenticated = vi.fn((): boolean => false);
const onSignedOut = vi.fn();
const sessionsPath = '/api/v1/me/sessions';
const revokePath = (id: string) => `/api/v1/me/sessions/${id}/revoke`;
const othersPath = '/api/v1/me/sessions/revoke-others';

const row = (id: string, isCurrent: boolean, n: number) => ({
  sessionId: id,
  isCurrent,
  createdAtUtc: `2026-11-0${n}T08:00:00Z`,
  lastSeenAtUtc: `2026-11-0${n}T09:30:00Z`,
});
const three = [row('s-other-1', false, 2), row('s-current', true, 3), row('s-other-2', false, 1)];

type Handlers = { list?: () => Response; revoke?: (call: ApiCall) => Response; others?: () => Response };

function api({ list, revoke, others }: Handlers = {}) {
  return stubApi((call) => {
    if (call.method === 'GET' && call.path === sessionsPath) return list ? list() : json(three);
    if (call.method === 'POST' && call.path === othersPath) return others ? others() : json({ revokedCount: 2 });
    if (call.method === 'POST' && call.path.endsWith('/revoke')) return revoke ? revoke(call) : new Response(null, { status: 204 });
    return new Response(null, { status: 404 });
  });
}

const posts = (calls: ApiCall[]) => calls.filter((call) => call.method === 'POST');
const setup = () => render(<ActiveSessions onUnauthenticated={onUnauthenticated} onSignedOut={onSignedOut} />);

async function confirmDialog(name: string) {
  const dialog = await screen.findByRole('alertdialog');
  await userEvent.click(within(dialog).getByRole('button', { name }));
}

describe('ActiveSessions', () => {
  beforeEach(() => {
    onUnauthenticated.mockReset();
    onUnauthenticated.mockReturnValue(false);
    onSignedOut.mockReset();
  });

  it('lists the sessions, current first, marks this device and never shows an identifier', async () => {
    api();
    setup();
    const list = await screen.findByRole('list', { name: 'نشست‌های فعال' });
    const items = within(list).getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(within(items[0]!).getByText('این دستگاه')).toBeInTheDocument();
    expect(within(items[0]!).queryByRole('button', { name: /پایان نشست دیگر/ })).toBeNull();
    expect(screen.getAllByText('این دستگاه')).toHaveLength(1);
    expect(document.body.textContent).not.toContain('s-current');
    expect(document.body.textContent).not.toContain('s-other-1');
  });

  it('shows created and last-seen times from the server', async () => {
    api();
    setup();
    const items = within(await screen.findByRole('list', { name: 'نشست‌های فعال' })).getAllByRole('listitem');
    expect(within(items[0]!).getByText(/ایجاد:/)).toBeInTheDocument();
    expect(within(items[0]!).getByText(/آخرین فعالیت:/)).toBeInTheDocument();
  });

  it('asks for confirmation and then ends one other session, refreshing the list', async () => {
    let revoked = false;
    const { calls } = api({
      list: () => json(revoked ? three.filter((s) => s.sessionId !== 's-other-1') : three),
      revoke: () => {
        revoked = true;
        return new Response(null, { status: 204 });
      },
    });
    setup();
    const buttons = await screen.findAllByRole('button', { name: /پایان نشست دیگر/ });
    await userEvent.click(buttons[0]!);
    expect(posts(calls)).toHaveLength(0);
    await confirmDialog('پایان نشست');
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]!.path).toBe(revokePath('s-other-1'));
    await waitFor(() => expect(within(screen.getByRole('list', { name: 'نشست‌های فعال' })).getAllByRole('listitem')).toHaveLength(2));
    expect(screen.getByRole('status')).toHaveTextContent('نشست پایان یافت');
    expect(onSignedOut).not.toHaveBeenCalled();
  });

  it('cancelling the confirmation sends nothing', async () => {
    const { calls } = api();
    setup();
    await userEvent.click((await screen.findAllByRole('button', { name: /پایان نشست دیگر/ }))[0]!);
    await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'انصراف' }));
    expect(posts(calls)).toHaveLength(0);
  });

  it('treats 404 on a single revoke as already ended and reloads the list', async () => {
    const { calls } = api({ revoke: () => json({ error: 'x' }, 404) });
    setup();
    await userEvent.click((await screen.findAllByRole('button', { name: /پایان نشست دیگر/ }))[0]!);
    await confirmDialog('پایان نشست');
    expect(await screen.findByRole('status')).toHaveTextContent('قبلاً پایان یافته');
    await waitFor(() => expect(calls.filter((c) => c.method === 'GET' && c.path === sessionsPath).length).toBeGreaterThanOrEqual(2));
  });

  it('ends all other sessions and reports how many', async () => {
    const { calls } = api({ list: vi.fn().mockReturnValueOnce(json(three)).mockReturnValue(json([row('s-current', true, 3)])) });
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'پایان همهٔ نشست‌های دیگر' }));
    await confirmDialog('پایان نشست‌های دیگر');
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]!.path).toBe(othersPath);
    expect(await screen.findByRole('status')).toHaveTextContent('۲ نشست دیگر پایان یافت');
    expect(onSignedOut).not.toHaveBeenCalled();
  });

  it('hides the end-all button when this is the only session', async () => {
    api({ list: () => json([row('s-current', true, 3)]) });
    setup();
    expect(await screen.findByText('نشست دیگری فعال نیست.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'پایان همهٔ نشست‌های دیگر' })).toBeNull();
  });

  it('signs this browser out after ending the current session', async () => {
    const { calls } = api();
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'خروج از این دستگاه' }));
    await confirmDialog('خروج از این دستگاه');
    await waitFor(() => expect(posts(calls)).toHaveLength(1));
    expect(posts(calls)[0]!.path).toBe(revokePath('s-current'));
    await waitFor(() => expect(onSignedOut).toHaveBeenCalledTimes(1));
  });

  it('does not sign out when ending the current session was refused', async () => {
    api({ revoke: () => json({ error: 'نشد' }, 409) });
    setup();
    await userEvent.click(await screen.findByRole('button', { name: 'خروج از این دستگاه' }));
    await confirmDialog('خروج از این دستگاه');
    expect(await screen.findByRole('alert')).toHaveTextContent('نشد');
    expect(onSignedOut).not.toHaveBeenCalled();
  });

  it('hands a 401 to the session handler (expired or revoked elsewhere) without a message', async () => {
    onUnauthenticated.mockReturnValue(true);
    api({ list: () => new Response(null, { status: 401 }) });
    setup();
    await waitFor(() => expect(onUnauthenticated).toHaveBeenCalled());
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('shows a retryable error when loading fails', async () => {
    let fail = true;
    api({ list: () => (fail ? new Response(null, { status: 500 }) : json(three)) });
    setup();
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    fail = false;
    await userEvent.click(screen.getByRole('button', { name: 'تلاش دوباره' }));
    expect(await screen.findByRole('list', { name: 'نشست‌های فعال' })).toBeInTheDocument();
  });

  it('explains a 429 without leaking details', async () => {
    api({ revoke: () => new Response(null, { status: 429 }) });
    setup();
    await userEvent.click((await screen.findAllByRole('button', { name: /پایان نشست دیگر/ }))[0]!);
    await confirmDialog('پایان نشست');
    expect(await screen.findByRole('alert')).toHaveTextContent('تعداد درخواست‌ها زیاد است');
  });

  it('never sends two requests for a double click on the confirm button', async () => {
    const { calls } = api();
    setup();
    await userEvent.click((await screen.findAllByRole('button', { name: /پایان نشست دیگر/ }))[0]!);
    const confirm = within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'پایان نشست' });
    await userEvent.dblClick(confirm);
    await waitFor(() => expect(posts(calls).length).toBeGreaterThanOrEqual(1));
    expect(posts(calls)).toHaveLength(1);
  });

  it('keeps nothing in browser storage', async () => {
    api();
    setup();
    await screen.findByRole('list', { name: 'نشست‌های فعال' });
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });
});
