import { act, renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useCompanyPermissions } from './useCompanyPermissions';

const onUnauthenticated = vi.fn(() => false);
const response = (companyId: string, codes: string[], isInstallationAdministrator = false) =>
  new Response(JSON.stringify({ isInstallationAdministrator, company: { id: companyId, permissionCodes: codes } }), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  });

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('company permission hints', () => {
  it('clears the previous company immediately and ignores a late response', async () => {
    let resolveFirst!: (value: Response) => void;
    const first = new Promise<Response>((resolve) => { resolveFirst = resolve; });
    vi.stubGlobal('fetch', vi.fn().mockReturnValueOnce(first).mockResolvedValueOnce(response('b', ['users.list'])));
    const { result, rerender } = renderHook(({ company }) => useCompanyPermissions(company, onUnauthenticated), { initialProps: { company: 'a' } });
    rerender({ company: 'b' });
    await waitFor(() => expect(result.current.codes).toEqual(['users.list']));
    await act(async () => resolveFirst(response('a', ['users.membership.approve'])));
    expect(result.current.codes).toEqual(['users.list']);
    rerender({ company: 'c' });
    expect(result.current.codes).toEqual([]);
    await waitFor(() => expect(result.current.status).toBe('error'));
  });

  it('fails closed on a mismatched company and allows retry', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response('other', ['users.list'])).mockResolvedValueOnce(response('a', ['users.membership.approve'])));
    const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.codes).toEqual([]);
    expect(result.current.access.status).toBe('error');
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.codes).toEqual(['users.membership.approve']));
  });

  it('passes session expiry to the sign-in handler', async () => {
    const handleExpiry = vi.fn(() => true);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
    const { result } = renderHook(() => useCompanyPermissions('a', handleExpiry));
    await waitFor(() => expect(handleExpiry).toHaveBeenCalledOnce());
    expect(result.current.codes).toEqual([]);
  });

  describe('access snapshot', () => {
    it('grants nothing while loading', () => {
      vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(() => undefined)));
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      expect(result.current.access).toMatchObject({ status: 'loading', isInstallationAdministrator: false, companyCodes: [] });
    });

    it('carries the installation flag and the codes of the company they belong to', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response('A', ['roles.view'], true)));
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.status).toBe('ready'));
      expect(result.current.access).toEqual({
        status: 'ready',
        isInstallationAdministrator: true,
        companyId: 'A',
        companyCodes: ['roles.view'],
      });
    });

    it('grants nothing on a server failure (403/500) and never keeps the old answer', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response('a', ['roles.view'], true)).mockResolvedValueOnce(new Response(null, { status: 500 })));
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.status).toBe('ready'));
      act(() => result.current.retry());
      expect(result.current.access).toMatchObject({ status: 'loading', isInstallationAdministrator: false, companyCodes: [] });
      await waitFor(() => expect(result.current.access.status).toBe('error'));
      expect(result.current.access).toMatchObject({ isInstallationAdministrator: false, companyCodes: [] });
    });

    it('refresh keeps the snapshot while re-checking (no unmount loop) and applies the new answer', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response('a', ['roles.view'], false)).mockResolvedValueOnce(response('a', [], false)));
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.status).toBe('ready'));
      act(() => result.current.refresh());
      expect(result.current.access.status).toBe('ready');
      await waitFor(() => expect(result.current.access.companyCodes).toEqual([]));
    });

    it('a failed refresh still ends in error and grants nothing', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response('a', ['roles.view'], true)).mockResolvedValueOnce(new Response(null, { status: 500 })));
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.status).toBe('ready'));
      act(() => result.current.refresh());
      await waitFor(() => expect(result.current.access.status).toBe('error'));
      expect(result.current.access).toMatchObject({ isInstallationAdministrator: false, companyCodes: [] });
    });

    it('grants nothing for a malformed answer', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ unexpected: true }), { status: 200, headers: { 'Content-Type': 'application/json' } })));
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.status).toBe('error'));
      expect(result.current.access.companyCodes).toEqual([]);
    });

    it('drops the old company at once on a company change and reloads for the new one', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response('a', ['roles.view'], true)).mockResolvedValueOnce(response('b', [])));
      const { result, rerender } = renderHook(({ company }) => useCompanyPermissions(company, onUnauthenticated), { initialProps: { company: 'a' } });
      await waitFor(() => expect(result.current.access.isInstallationAdministrator).toBe(true));
      rerender({ company: 'b' });
      expect(result.current.access).toMatchObject({ status: 'loading', isInstallationAdministrator: false, companyCodes: [] });
      await waitFor(() => expect(result.current.access).toMatchObject({ status: 'ready', companyId: 'b', isInstallationAdministrator: false, companyCodes: [] }));
    });

    it('is denied when there is no active company', () => {
      const fetchMock = vi.fn();
      vi.stubGlobal('fetch', fetchMock);
      const { result } = renderHook(() => useCompanyPermissions(undefined, onUnauthenticated));
      expect(result.current.access).toMatchObject({ status: 'loading', companyCodes: [], isInstallationAdministrator: false });
      expect(fetchMock).not.toHaveBeenCalled();
    });
  });

  describe('revalidation when the tab is shown again', () => {
    it('reloads after a minute and applies the reduced permissions', async () => {
      const now = vi.spyOn(Date, 'now').mockReturnValue(1_000_000);
      const fetchMock = vi.fn().mockResolvedValueOnce(response('a', ['roles.view', 'roles.update'])).mockResolvedValueOnce(response('a', ['roles.view']));
      vi.stubGlobal('fetch', fetchMock);
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.companyCodes).toEqual(['roles.view', 'roles.update']));

      now.mockReturnValue(1_000_000 + 61_000);
      act(() => { document.dispatchEvent(new Event('visibilitychange')); });
      await waitFor(() => expect(result.current.access.companyCodes).toEqual(['roles.view']));
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('does not reload for a quick return, and not while the tab is hidden', async () => {
      const now = vi.spyOn(Date, 'now').mockReturnValue(1_000_000);
      const fetchMock = vi.fn().mockResolvedValue(response('a', ['roles.view']));
      vi.stubGlobal('fetch', fetchMock);
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.status).toBe('ready'));

      now.mockReturnValue(1_000_000 + 5_000);
      act(() => { document.dispatchEvent(new Event('visibilitychange')); });
      now.mockReturnValue(1_000_000 + 120_000);
      const hidden = vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');
      act(() => { document.dispatchEvent(new Event('visibilitychange')); });
      hidden.mockRestore();
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it('grants nothing when the revalidation fails', async () => {
      const now = vi.spyOn(Date, 'now').mockReturnValue(1_000_000);
      vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response('a', ['roles.view'], true)).mockResolvedValueOnce(new Response(null, { status: 500 })));
      const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
      await waitFor(() => expect(result.current.access.status).toBe('ready'));
      now.mockReturnValue(1_000_000 + 61_000);
      act(() => { document.dispatchEvent(new Event('visibilitychange')); });
      await waitFor(() => expect(result.current.access.status).toBe('error'));
      expect(result.current.access.isInstallationAdministrator).toBe(false);
      expect(result.current.access.companyCodes).toEqual([]);
    });
  });
});
