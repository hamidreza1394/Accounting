import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useCompanyPermissions } from './useCompanyPermissions';

const onUnauthenticated = vi.fn(() => false);
const response = (companyId: string, codes: string[]) => new Response(JSON.stringify({ isInstallationAdministrator: false, company: { id: companyId, permissionCodes: codes } }), { status: 200, headers: { 'Content-Type': 'application/json' } });

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
    vi.unstubAllGlobals();
  });

  it('fails closed on a mismatched company and allows retry', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response('other', ['users.list'])).mockResolvedValueOnce(response('a', ['users.membership.approve'])));
    const { result } = renderHook(() => useCompanyPermissions('a', onUnauthenticated));
    await waitFor(() => expect(result.current.status).toBe('error'));
    expect(result.current.codes).toEqual([]);
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.codes).toEqual(['users.membership.approve']));
    vi.unstubAllGlobals();
  });
  it('passes session expiry to the sign-in handler', async () => {
    const handleExpiry = vi.fn(() => true);
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 401 })));
    const { result } = renderHook(() => useCompanyPermissions('a', handleExpiry));
    await waitFor(() => expect(handleExpiry).toHaveBeenCalledOnce());
    expect(result.current.codes).toEqual([]);
    vi.unstubAllGlobals();
  });

});
