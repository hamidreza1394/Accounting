import { lazy, useState } from 'react';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, onTestFinished } from 'vitest';
import { Link, RouteView, RouterProvider, matchPath, useLocation, useNavigate, useDocumentTitle, useParams, useSearchParams, type Route } from './router';

function Home() {
  return <h1>خانه</h1>;
}

function UserPage() {
  const { id } = useParams();
  return <h1>کاربر {id}</h1>;
}

function Nav() {
  const navigate = useNavigate();
  const location = useLocation();
  return (
    <nav>
      <Link to="/">خانه</Link>
      <Link to="/users/a%20b">کاربر</Link>
      <Link to="/users/7" target="_blank">تب جدید</Link>
      <Link to="/users/8" download>دانلود</Link>
      <Link to="https://example.com/x">بیرونی</Link>
      <Link to="//evil.example/x">پروتکل‌نسبی</Link>
      <Link to="mailto:a@b.ir">ایمیل</Link>
      <Link to="/missing">ناشناس</Link>
      <Link to="/admin">مدیریت</Link>
      <button type="button" onClick={() => navigate('/users/9', { replace: true })}>برو ۹</button>
      <button type="button" onClick={() => navigate('/lazy')}>برو تنبل</button>
      <output>{location.pathname}</output>
    </nav>
  );
}

const LazyPage = lazy(() => Promise.resolve({ default: () => <h1>تنبل</h1> }));

const routes: Route[] = [
  { path: '/', element: Home, title: 'خانه' },
  { path: '/users/:id', element: UserPage, title: 'کاربر' },
  { path: '/lazy', element: LazyPage, title: 'صفحهٔ تنبل' },
  { path: '/admin', element: Home, title: 'مدیریت', permission: 'admin.users.view' },
];

function setup(path = '/app/', canAccess?: (p: string) => boolean) {
  window.history.pushState(null, '', path);
  return render(
    <RouterProvider routes={routes} {...(canAccess ? { canAccess } : {})}>
      <Nav />
      <RouteView />
    </RouterProvider>,
  );
}

describe('router', () => {
  beforeEach(() => {
    window.history.pushState(null, '', '/app/');
  });

  it('matches params segments and decodes them', () => {
    expect(matchPath('/users/:id', '/users/a%20b')).toEqual({ id: 'a b' });
    expect(matchPath('/users/:id', '/users')).toBeNull();
    expect(matchPath('/', '/')).toEqual({});
  });

  it('renders the route for the initial URL under the base and sets the title', () => {
    setup('/app/users/42');
    expect(screen.getByRole('heading', { name: 'کاربر 42' })).toBeInTheDocument();
    expect(document.title).toBe('کاربر | فرانو');
  });

  it('navigates with a plain click, pushes history and passes params', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('link', { name: 'کاربر' }));
    expect(window.location.pathname).toBe('/app/users/a%20b');
    expect(screen.getByRole('heading', { name: 'کاربر a b' })).toBeInTheDocument();
    expect(document.title).toBe('کاربر | فرانو');
    expect(screen.getByRole('link', { name: 'کاربر' })).toHaveAttribute('href', '/app/users/a%20b');
  });

  it('follows the back button (popstate)', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('link', { name: 'کاربر' }));
    act(() => {
      window.history.back();
    });
    await screen.findByRole('heading', { name: 'خانه', level: 1 });
    expect(window.location.pathname).toBe('/app/');
  });

  /** Records whether the router already prevented the default, then blocks jsdom's real navigation. */
  function observeDefaultPrevented(): boolean[] {
    const seen: boolean[] = [];
    const listener = (event: Event) => {
      seen.push(event.defaultPrevented);
      event.preventDefault();
    };
    document.addEventListener('click', listener);
    onTestFinished(() => document.removeEventListener('click', listener));
    return seen;
  }

  it('does not intercept modified clicks, target or download links', async () => {
    const user = userEvent.setup();
    setup();
    const seen = observeDefaultPrevented();
    await user.keyboard('{Control>}');
    await user.click(screen.getByRole('link', { name: 'کاربر' }));
    await user.keyboard('{/Control}');
    await user.click(screen.getByRole('link', { name: 'تب جدید' }));
    await user.click(screen.getByRole('link', { name: 'دانلود' }));
    expect(seen).toEqual([false, false, false]);
    expect(window.location.pathname).toBe('/app/');
  });

  it('leaves external and protocol-relative targets to the browser', async () => {
    const user = userEvent.setup();
    setup();
    const seen = observeDefaultPrevented();
    await user.click(screen.getByRole('link', { name: 'بیرونی' }));
    await user.click(screen.getByRole('link', { name: 'پروتکل‌نسبی' }));
    await user.click(screen.getByRole('link', { name: 'ایمیل' }));
    expect(seen).toEqual([false, false, false]);
    expect(screen.getByRole('link', { name: 'بیرونی' })).toHaveAttribute('href', 'https://example.com/x');
    expect(screen.getByRole('link', { name: 'پروتکل‌نسبی' })).toHaveAttribute('href', '//evil.example/x');
    expect(window.location.pathname).toBe('/app/');
  });

  it('replaces instead of pushing when navigating to the identical URL', async () => {
    const user = userEvent.setup();
    setup('/app/users/9');
    const length = window.history.length;
    await user.click(screen.getByRole('button', { name: 'برو ۹' }));
    expect(window.history.length).toBe(length);
  });

  it('does not intercept a non-left click', async () => {
    setup();
    const seen = observeDefaultPrevented();
    const link = screen.getByRole('link', { name: 'کاربر' });
    link.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 1 }));
    expect(seen).toEqual([false]);
    expect(window.location.pathname).toBe('/app/');
  });

  it('renders NotFound for an unknown path and sets its title', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('link', { name: 'ناشناس' }));
    expect(screen.getByRole('alert')).toHaveTextContent('صفحه پیدا نشد');
    expect(document.title).toBe('صفحه پیدا نشد | فرانو');
  });

  it('renders a lazy route after the loading state', async () => {
    setup('/app/lazy');
    expect(await screen.findByRole('heading', { name: 'تنبل' })).toBeInTheDocument();
    expect(document.title).toBe('صفحهٔ تنبل | فرانو');
  });

  it('supports programmatic replace navigation', async () => {
    const user = userEvent.setup();
    setup();
    const length = window.history.length;
    await user.click(screen.getByRole('button', { name: 'برو ۹' }));
    expect(screen.getByRole('heading', { name: 'کاربر 9' })).toBeInTheDocument();
    expect(window.history.length).toBe(length);
  });

  it('shows a forbidden page when the UI permission hint denies the route', async () => {
    const user = userEvent.setup();
    setup('/app/', () => false);
    await user.click(screen.getByRole('link', { name: 'مدیریت' }));
    expect(screen.getByRole('alert')).toHaveTextContent('دسترسی ندارید');
  });

  it('keeps the query string in the URL through useSearchParams (push, replace, back)', async () => {
    function Query() {
      const [params, setParams] = useSearchParams();
      return (
        <div>
          <output aria-label="q">{params.get('q') ?? 'none'}</output>
          <button type="button" onClick={() => setParams(new URLSearchParams({ q: 'علی' }))}>
            جستجو
          </button>
          <button type="button" onClick={() => setParams(new URLSearchParams({ q: 'علی', page: '2' }), { replace: true })}>
            صفحه ۲
          </button>
          <button type="button" onClick={() => setParams(new URLSearchParams())}>
            پاک
          </button>
        </div>
      );
    }
    const user = userEvent.setup();
    window.history.pushState(null, '', '/app/users/1?q=ab');
    render(
      <RouterProvider routes={routes}>
        <Query />
      </RouterProvider>,
    );
    expect(screen.getByLabelText('q')).toHaveTextContent('ab');
    await user.click(screen.getByRole('button', { name: 'جستجو' }));
    expect(window.location.search).toBe('?q=%D8%B9%D9%84%DB%8C');
    expect(window.location.pathname).toBe('/app/users/1');
    const length = window.history.length;
    await user.click(screen.getByRole('button', { name: 'صفحه ۲' }));
    expect(window.history.length).toBe(length);
    expect(window.location.search).toContain('page=2');
    await user.click(screen.getByRole('button', { name: 'پاک' }));
    expect(window.location.search).toBe('');
    expect(screen.getByLabelText('q')).toHaveTextContent('none');
    act(() => {
      window.history.back();
    });
    await screen.findByText('علی');
  });

  it('moves focus to the page heading after a navigation, but not on the first load', async () => {
    const user = userEvent.setup();
    setup('/app/users/42');
    expect(document.body).toHaveFocus();
    await user.click(screen.getByRole('link', { name: 'خانه' }));
    expect(screen.getByRole('heading', { name: 'خانه' })).toHaveFocus();
  });

  it('waits for a lazy page heading before focusing it', async () => {
    const user = userEvent.setup();
    setup();
    await user.click(screen.getByRole('button', { name: 'برو تنبل' }));
    expect(await screen.findByRole('heading', { name: 'تنبل' })).toHaveFocus();
  });

  it('does not move focus for a query-only change', async () => {
    const user = userEvent.setup();
    setup('/app/users/42');
    const button = screen.getByRole('button', { name: 'برو ۹' });
    await user.click(screen.getByRole('link', { name: 'خانه' }));
    await user.click(screen.getByRole('link', { name: 'خانه' }));
    button.focus();
    act(() => {
      window.history.replaceState(null, '', '/app/?x=1');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect(button).toHaveFocus();
  });

  it('sets a refined title with useDocumentTitle', async () => {
    const user = userEvent.setup();
    // Like a page that learns its subject after loading: the refinement comes in a later commit.
    function Titled() {
      const [name, setName] = useState<string | null>(null);
      useDocumentTitle(name);
      return (
        <h1>
          <button type="button" onClick={() => setName('علی رضایی')}>
            بارگذاری
          </button>
        </h1>
      );
    }
    window.history.pushState(null, '', '/app/');
    render(<RouterProvider routes={[{ path: '/', element: Titled, title: 'عمومی' }]} />);
    expect(document.title).toBe('عمومی | فرانو');
    await user.click(screen.getByRole('button', { name: 'بارگذاری' }));
    expect(document.title).toBe('علی رضایی | فرانو');
  });
});
