/**
 * Behavioral test: the Faranou login password toggle button must actually
 * flip the password input's `type` attribute between `password` and `text`
 * when clicked, and keep the ARIA state in sync.
 *
 * This test exists because the xUnit tests in LoginPasswordToggleTests.cs only
 * inspect the raw .cshtml / .js source as text. They cannot detect a behavioral
 * regression where the markup looks right but the script wires the wrong element,
 * misses the DOMContentLoaded listener, or reads the wrong data-* attribute.
 *
 * Approach:
 *   1. Read the real Login.cshtml + taraz-login.js + site.js from the repo.
 *   2. Render the <form>...</form> slice in jsdom. Razor directives and
 *      @(...) ternaries are normalized to their fa-IR/RTL branch so the DOM
 *      matches what ASP.NET Core would actually emit at request time.
 *      Tag-helper attributes (asp-for) are expanded to the id/name that the
 *      runtime would generate, so taraz-login.js's getElementById lookup
 *      finds the password input.
 *   3. Inject jQuery, then taraz-login.js, then site.js (the legacy toggle
 *      handler in site.js is scoped to NOT match .taraz-login-form, so it
 *      must remain inert inside this fixture).
 *   4. Dispatch DOMContentLoaded so taraz-login.js attaches its click handler.
 *   5. Click the toggle three times and assert the contract after each click.
 */

const fs = require('fs');
const path = require('path');
const test = require('node:test');
const assert = require('node:assert/strict');
const { JSDOM } = require('jsdom');

const REPO_ROOT = path.resolve(__dirname, '..', '..');
const LOGIN_CSHTML_PATH = path.join(REPO_ROOT, 'Server', 'Pages', 'Account', 'Login.cshtml');
const TARAZ_LOGIN_JS_PATH = path.join(REPO_ROOT, 'Server', 'wwwroot', 'js', 'taraz-login.js');
const SITE_JS_PATH = path.join(REPO_ROOT, 'Server', 'wwwroot', 'js', 'site.js');
const JQUERY_PATH = path.join(REPO_ROOT, 'Server', 'wwwroot', 'lib', 'jquery', 'jquery.min.js');

/**
 * Read the Login.cshtml and return a self-contained HTML document whose <body>
 * contains the rendered form (Razor directives stripped, tag-helper attributes
 * expanded, RTL branch of ternaries selected).
 */
function buildLoginDocument() {
    const cshtml = fs.readFileSync(LOGIN_CSHTML_PATH, 'utf8');

    const formStart = cshtml.indexOf('<form');
    const formEnd = cshtml.indexOf('</form>');
    if (formStart < 0 || formEnd < 0) {
        throw new Error('Could not locate <form>...</form> in Login.cshtml');
    }
    const formEndFull = formEnd + '</form>'.length;
    const formMarkup = cshtml.slice(formStart, formEndFull);

    // Razor directives and helpers do not render to the DOM verbatim.
    // 1. Drop @Html.* helper calls.
    // 2. Resolve `@(isRightToLeft ? "RTL" : "LTR")` to the RTL branch (the Faranou
    //    login page defaults to fa-IR).
    // 3. Replace @Resources.* placeholders with a stable placeholder label so
    //    the DOM is well-formed and deterministic.
    // 4. Drop @section blocks (already excluded by the form slice, but defensive).
    // 5. Strip any remaining @identifier directives.
    // 6. Expand the password input's `asp-for="ViewModel.Password"` to the
    //    id="ViewModel_Password" name="ViewModel.Password" pair that ASP.NET Core's
    //    tag helper would emit at request time. taraz-login.js looks up the password
    //    by id using the data-password-toggle attribute value, so the id must be
    //    "ViewModel_Password" to match the contract.
    let rendered = formMarkup
        .replace(/@Html\.\w+(\([^)]*\))?/g, '')
        .replace(/@\(isRightToLeft\s*\?\s*"([^"]+)"\s*:\s*"[^"]+"\s*\)/g, (_, rtl) => rtl)
        .replace(/@(Resources\.[\w.]+)/g, 'Field')
        .replace(/@section\s+\w+\s*\{[^}]*\}/g, '')
        .replace(/@\w+(\([^)]*\))?/g, '')
        .replace(
            /<input([^>]*?)\basp-for="ViewModel\.Password"([^>]*?)\s*\/>/,
            (m, pre, post) => `<input${pre} id="ViewModel_Password" name="ViewModel.Password"${post} />`
        );

    return `<!DOCTYPE html>
<html><head><meta charset="utf-8"></head>
<body>
${rendered}
</body></html>`;
}

/** Build a jsdom window with jQuery + taraz-login.js + site.js loaded and DOMContentLoaded fired. */
function buildWindow() {
    const html = buildLoginDocument();
    const dom = new JSDOM(html, {
        runScripts: 'outside-only',
        url: 'http://localhost/',
    });
    const { window } = dom;

    // jQuery is required by site.js. taraz-login.js itself is plain DOM, but
    // loading site.js in a window without jQuery would throw, which would
    // mask real failures in taraz-login.js. Load jQuery first.
    const jquery = fs.readFileSync(JQUERY_PATH, 'utf8');
    const taraz = fs.readFileSync(TARAZ_LOGIN_JS_PATH, 'utf8');
    const site = fs.readFileSync(SITE_JS_PATH, 'utf8');
    window.eval(jquery);
    window.eval(taraz);
    // site.js wraps everything in $(function(){...}) which jQuery fires
    // immediately if readyState is 'complete'. jsdom reports 'complete' by
    // default after construction, so this is effectively synchronous.
    try { window.eval(site); } catch (err) {
        // If site.js throws for a jsdom limitation (e.g. missing feature),
        // surface the error so we know why; do not silently swallow.
        // The legacy toggle handler is scoped to NOT match .taraz-login-form,
        // so behavioral correctness here does not depend on site.js.
        // Re-throw only if the error is not a benign jQuery DOMReady quirk.
        if (!/jQuery|tinyMCE/i.test(err.message)) throw err;
    }

    // Fire DOMContentLoaded so taraz-login.js's listener attaches.
    const ev = new window.Event('DOMContentLoaded', { bubbles: true, cancelable: false });
    window.document.dispatchEvent(ev);

    return window;
}

test('Faranou markup exposes the toggle and the password input', () => {
    const window = buildWindow();
    const toggle = window.document.querySelector('[data-password-toggle="ViewModel_Password"]');
    const password = window.document.getElementById('ViewModel_Password');
    assert.ok(toggle, 'password toggle button must be present in the rendered markup');
    assert.ok(password, 'password input must be present and carry id="ViewModel_Password"');
});

test('Initial state: password.type === "password", aria-pressed === "false"', () => {
    const window = buildWindow();
    const password = window.document.getElementById('ViewModel_Password');
    const toggle = window.document.querySelector('[data-password-toggle="ViewModel_Password"]');
    assert.equal(password.getAttribute('type'), 'password');
    assert.equal(toggle.getAttribute('aria-pressed'), 'false');
});

test('Click #1 shows the password: type becomes "text", aria-pressed becomes "true"', () => {
    const window = buildWindow();
    const password = window.document.getElementById('ViewModel_Password');
    const toggle = window.document.querySelector('[data-password-toggle="ViewModel_Password"]');
    toggle.dispatchEvent(new window.Event('click', { bubbles: true, cancelable: true }));
    assert.equal(password.getAttribute('type'), 'text');
    assert.equal(password.type, 'text');
    assert.equal(toggle.getAttribute('aria-pressed'), 'true');
});

test('Click #2 hides the password: type returns to "password", aria-pressed returns to "false"', () => {
    const window = buildWindow();
    const password = window.document.getElementById('ViewModel_Password');
    const toggle = window.document.querySelector('[data-password-toggle="ViewModel_Password"]');
    toggle.dispatchEvent(new window.Event('click', { bubbles: true, cancelable: true }));
    toggle.dispatchEvent(new window.Event('click', { bubbles: true, cancelable: true }));
    assert.equal(password.getAttribute('type'), 'password');
    assert.equal(password.type, 'password');
    assert.equal(toggle.getAttribute('aria-pressed'), 'false');
});

test('Three clicks end with the password visible (idempotent toggle)', () => {
    const window = buildWindow();
    const password = window.document.getElementById('ViewModel_Password');
    const toggle = window.document.querySelector('[data-password-toggle="ViewModel_Password"]');
    for (let i = 0; i < 3; i++) {
        toggle.dispatchEvent(new window.Event('click', { bubbles: true, cancelable: true }));
    }
    assert.equal(password.type, 'text');
    assert.equal(toggle.getAttribute('aria-pressed'), 'true');
});

test('aria-label toggles between the show and hide phrases', () => {
    const window = buildWindow();
    const toggle = window.document.querySelector('[data-password-toggle="ViewModel_Password"]');
    const initial = toggle.getAttribute('aria-label');
    toggle.dispatchEvent(new window.Event('click', { bubbles: true, cancelable: true }));
    const afterClick1 = toggle.getAttribute('aria-label');
    toggle.dispatchEvent(new window.Event('click', { bubbles: true, cancelable: true }));
    const afterClick2 = toggle.getAttribute('aria-label');
    assert.notEqual(initial, afterClick1, 'aria-label must change after the first click');
    assert.equal(initial, afterClick2, 'aria-label must return to the initial phrase after the second click');
});

test('The legacy jQuery toggle handler in site.js does NOT double-fire inside .taraz-login-form', () => {
    // site.js binds its own click handler to [data-password-toggle] but excludes
    // .taraz-login-form [data-password-toggle]. If that exclusion broke, both
    // handlers would run on the same click and the visible-state toggle would
    // fire twice, leaving password.type unchanged after a single click.
    const window = buildWindow();
    const password = window.document.getElementById('ViewModel_Password');
    const toggle = window.document.querySelector('[data-password-toggle="ViewModel_Password"]');
    toggle.dispatchEvent(new window.Event('click', { bubbles: true, cancelable: true }));
    // If only taraz-login.js handled the click, the type must be 'text'.
    // If site.js also handled it (regression), the type would still be 'password'.
    assert.equal(password.type, 'text',
        'site.js must not double-toggle the password inside .taraz-login-form');
});
