import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { CreateMembershipRequest } from './CreateMembershipRequest';
import { json, stubApi } from '../../test/adminUsers';

const onUnauthenticated = vi.fn(() => false);

describe('CreateMembershipRequest', () => {
  it('validates exact search, lists only discovered destinations and sends the selected id with antiforgery', async () => {
    const user = userEvent.setup();
    const onCreated = vi.fn();
    const { calls } = stubApi((call) => {
      if (call.path.endsWith('/destinations')) return json({ items: [{ companyId: 'b', name: 'شرکت ب' }], page: 1, pageSize: 25, totalCount: 1 });
      if (call.method === 'POST') return json({ id: 'r', status: 'Pending' }, 201);
      return new Response(null, { status: 404 });
    });
    render(<CreateMembershipRequest onUnauthenticated={onUnauthenticated} onCreated={onCreated} />);
    await user.type(screen.getByLabelText('نام دقیق شرکت مقصد'), 'اب');
    await user.click(screen.getByRole('button', { name: 'جستجوی شرکت' }));
    expect(screen.getByRole('alert')).toHaveTextContent('۳');
    expect(calls).toHaveLength(0);
    await user.clear(screen.getByLabelText('نام دقیق شرکت مقصد'));
    await user.type(screen.getByLabelText('نام دقیق شرکت مقصد'), 'شرکت ب');
    await user.click(screen.getByRole('button', { name: 'جستجوی شرکت' }));
    await user.click(await screen.findByRole('button', { name: 'درخواست عضویت در شرکت ب' }));
    await waitFor(() => expect(onCreated).toHaveBeenCalledOnce());
    const post = calls.find((call) => call.method === 'POST');
    expect(post?.body).toEqual({ destinationCompanyId: 'b' });
    expect(post?.headers['X-XSRF-TOKEN']).toBe('tok');
    expect(screen.getByRole('status')).toHaveTextContent('در انتظار تأیید');
  });

  it('shows server rejection and retains the result for retry', async () => {
    const user = userEvent.setup();
    stubApi((call) => call.path.endsWith('/destinations')
      ? json({ items: [{ companyId: 'b', name: 'شرکت ب' }], page: 1, pageSize: 25, totalCount: 1 })
      : json({ error: 'شرکت پیدا نشد' }, 404));
    render(<CreateMembershipRequest onUnauthenticated={onUnauthenticated} onCreated={vi.fn()} />);
    await user.type(screen.getByLabelText('نام دقیق شرکت مقصد'), 'شرکت ب');
    await user.click(screen.getByRole('button', { name: 'جستجوی شرکت' }));
    await user.click(await screen.findByRole('button', { name: 'درخواست عضویت در شرکت ب' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('شرکت پیدا نشد');
  });

  it('resets loading when a pending destination search is cancelled by clearing the query', async () => {
    const user = userEvent.setup();
    let destinationSearchCount = 0;
    const { calls } = stubApi((call) => {
      if (call.path.endsWith('/destinations')) {
        destinationSearchCount += 1;
        if (destinationSearchCount === 1) return new Promise<Response>(() => undefined);
        return json({ items: [{ companyId: 'b', name: 'شرکت ب' }], page: 1, pageSize: 25, totalCount: 1 });
      }
      return new Response(null, { status: 404 });
    });

    render(<CreateMembershipRequest onUnauthenticated={onUnauthenticated} onCreated={vi.fn()} />);
    const searchInput = screen.getByLabelText('نام دقیق شرکت مقصد');
    const searchButton = screen.getByRole('button', { name: 'جستجوی شرکت' });

    await user.type(searchInput, 'شرکت الف');
    await user.click(searchButton);
    expect(await screen.findByRole('status')).toHaveTextContent('در حال جستجو');

    await user.clear(searchInput);
    await waitFor(() => expect(searchButton).toBeEnabled());

    await user.type(searchInput, 'شرکت ب');
    await user.click(searchButton);
    expect(await screen.findByRole('button', { name: 'درخواست عضویت در شرکت ب' })).toBeInTheDocument();
    expect(calls.filter((call) => call.path.endsWith('/destinations'))).toHaveLength(2);
  });

});
