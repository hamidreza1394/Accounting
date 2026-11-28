import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';
import { afterEach, describe, expect, it, vi } from 'vitest';

const staticFiles = new URL('../../Vendor.Host/Development/StaticFiles/', import.meta.url);
const setupHtml = readFileSync(new URL('index.html', staticFiles), 'utf8');
const setupScript = readFileSync(new URL('setup.js', staticFiles), 'utf8');

function jsonResponse(body, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  };
}

function deferred() {
  let resolve;
  const promise = new Promise(done => { resolve = done; });
  return { promise, resolve };
}

describe('Vendor Development database setup page', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('ignores a successful connection-test response that arrives after provisioning starts', async () => {
    const dom = new JSDOM(setupHtml, {
      runScripts: 'outside-only',
      url: 'https://localhost/development/database-setup',
    });
    const { window } = dom;
    const testResponse = deferred();
    const testStarted = deferred();
    const provisionStarted = deferred();
    const statusLoaded = deferred();

    const fetchMock = vi.fn((input) => {
      const url = String(input);
      if (url.endsWith('/antiforgery')) {
        return Promise.resolve(jsonResponse({ requestToken: 'csrf-test' }));
      }
      if (url.endsWith('/status')) {
        statusLoaded.resolve();
        return Promise.resolve(jsonResponse({
          source: 'none',
          canDeleteSavedRuntimeConnection: true,
          savedRuntimeConnectionInWindowsCredentialManager: false,
        }));
      }
      if (url.endsWith('/test')) {
        testStarted.resolve();
        return testResponse.promise;
      }
      if (url.endsWith('/provision-token')) {
        return Promise.resolve(jsonResponse({ token: 'one-time-bootstrap-token' }));
      }
      if (url.endsWith('/provision')) {
        provisionStarted.resolve();
        return Promise.resolve(jsonResponse({
          success: true,
          stages: [
            { key: 'connection', state: 'success', message: 'ok' },
            { key: 'test', state: 'success', message: 'ok' },
            { key: 'inspect', state: 'success', message: 'ok' },
            { key: 'createDatabase', state: 'success', message: 'ok' },
            { key: 'migrate', state: 'success', message: 'ok' },
            { key: 'runtimeIdentity', state: 'success', message: 'ok' },
            { key: 'verify', state: 'success', message: 'ok' },
          ],
          runtimeUsername: 'runtime-user',
          generatedPassword: null,
          savedToCredentialManager: true,
          message: 'راه‌اندازی با موفقیت تکمیل شد.',
        }));
      }
      throw new Error(`Unexpected setup request: ${url}`);
    });

    window.fetch = fetchMock;
    window.confirm = vi.fn(() => true);
    try {
      window.eval(setupScript);
      await statusLoaded.promise;
      await new Promise(resolve => setTimeout(resolve, 0));

      window.document.querySelector('#challenge-input').value = 'console-challenge';
      window.document.querySelector('#test-button').click();
      await testStarted.promise;

      const form = window.document.querySelector('#connection-form');
      form.dispatchEvent(new window.Event('submit', { bubbles: true, cancelable: true }));
      await provisionStarted.promise;
      await new Promise(resolve => setTimeout(resolve, 0));

      expect(window.document.querySelector('#runtime-panel').hidden).toBe(false);
      expect(window.document.querySelector('#result').textContent).toBe('راه‌اندازی با موفقیت تکمیل شد.');

      testResponse.resolve(jsonResponse({ success: true, message: 'late connection-test success' }));
      await new Promise(resolve => setTimeout(resolve, 0));

      expect(window.document.querySelector('#result').textContent).toBe('راه‌اندازی با موفقیت تکمیل شد.');
      expect(window.document.querySelector('#save-button')).toBeNull();
      expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/save'))).toBe(false);
    } finally {
      window.close();
    }
  });
});
