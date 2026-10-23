// Database setup page (Pages/DatabaseSetup.cshtml). CSP-safe: no inline script or handlers.
// The server stays authoritative; these checks only give early, per-field feedback.
(() => {
    'use strict';

    const form = document.querySelector('[data-setup-form]');
    if (!form) return;

    const messages = {
        server: 'نام یا نشانی سرور SQL Server را وارد کنید؛ مثال: localhost یا localhost\\SQLEXPRESS یا host,1433.',
        serverLong: 'نام سرور حداکثر ۲۵۶ نویسه است؛ آن را کوتاه‌تر کنید.',
        database: 'نام پایگاه داده را وارد کنید؛ مثال: Farano.',
        databaseLong: 'نام پایگاه داده حداکثر ۱۲۳ نویسه است (نام فایل لاگ هم از روی آن ساخته می‌شود)؛ نام کوتاه‌تری انتخاب کنید.',
        databaseUnsafe: 'نام دیتابیس فقط می‌تواند حروف، رقم، زیرخط (_)، خط تیره (-)، نقطهٔ میانی و فاصلهٔ عادی در میانهٔ نام داشته باشد؛ نویسه‌های مسیر و نقل‌قول، نویسهٔ نامرئی، نقطه یا فاصله در ابتدا/انتها و «..» مجاز نیست.',
        databaseSystem: 'پایگاه داده‌های سیستمی (master، model، msdb، tempdb) قابل استفاده نیستند؛ نام دیگری انتخاب کنید.',
        user: 'برای ورود با SQL Server نام کاربری لازم است؛ آن را وارد کنید.',
        password: 'برای ورود با SQL Server رمز عبور لازم است؛ آن را وارد کنید.',
        token: 'کد راه‌اندازی را از فایل روی سرور وارد کنید.'
    };
    const systemDatabases = ['master', 'model', 'msdb', 'tempdb'];
    // Real allowlist (the name becomes part of the .mdf/_log.ldf file names); the server is authoritative.
    const allowedName = v => /^[\p{L}\p{M}\p{N}_ .\-]+$/u.test(v) && !/^[. ]|[. ]$|\.\./.test(v);
    const isSystemDatabase = v => {
        const plain = v.trim().toLowerCase();
        let folded = plain;
        try { folded = v.trim().normalize('NFKC').trim().toLowerCase(); } catch (e) { return true; }
        return systemDatabases.includes(plain) || systemDatabases.includes(folded);
    };

    const field = name => form.querySelector('[data-field="' + name + '"]');
    const errorSlot = name => form.querySelector('[data-error-for="' + name + '"]');

    const errorSummary = form.querySelector('[data-error-summary]');

    // The ids that were there before any error (the hint) are kept; the error id is appended and removed again.
    const baseDescribedBy = input => {
        if (!input.hasAttribute('data-base-describedby')) {
            const slotId = 'err-' + input.getAttribute('data-field');
            const ids = (input.getAttribute('aria-describedby') || '').split(/\s+/).filter(id => id && id !== slotId);
            input.setAttribute('data-base-describedby', ids.join(' '));
        }
        return input.getAttribute('data-base-describedby');
    };

    const setError = (name, text) => {
        const input = field(name);
        const slot = errorSlot(name);
        if (!input || !slot) return;
        const base = baseDescribedBy(input);
        slot.textContent = text || '';
        slot.hidden = !text;
        if (text) {
            input.setAttribute('aria-invalid', 'true');
            input.setAttribute('aria-describedby', (base ? base + ' ' : '') + slot.id);
        } else {
            input.removeAttribute('aria-invalid');
            if (base) input.setAttribute('aria-describedby', base);
            else input.removeAttribute('aria-describedby');
        }
    };

    const mode = form.querySelector('[data-auth-mode]');
    const sqlFields = form.querySelectorAll('[data-sql-only]');
    const isSql = () => !!mode && mode.value === 'Sql';

    // Hidden SQL fields are disabled so they are neither validated nor posted in Windows mode.
    const refreshMode = () => {
        sqlFields.forEach(wrapper => {
            wrapper.hidden = !isSql();
            wrapper.querySelectorAll('input').forEach(input => { input.disabled = !isSql(); });
        });
        if (!isSql()) {
            setError('UserName', '');
            setError('Password', '');
        }
    };
    if (mode) {
        mode.addEventListener('change', refreshMode);
        refreshMode();
    }

    // Password show/hide.
    form.querySelectorAll('[data-toggle-password]').forEach(button => {
        const target = document.getElementById(button.getAttribute('data-target') || '');
        if (!target) return;
        button.addEventListener('click', () => {
            const show = target.type === 'password';
            target.type = show ? 'text' : 'password';
            button.setAttribute('aria-pressed', show ? 'true' : 'false');
            button.setAttribute('aria-label', show ? 'پنهان‌کردن رمز عبور' : 'نمایش رمز عبور');
            button.textContent = show ? 'پنهان' : 'نمایش';
        });
    });

    // Example chips fill the server field.
    form.querySelectorAll('[data-fill]').forEach(chip => {
        chip.addEventListener('click', () => {
            const target = document.getElementById(chip.getAttribute('data-target') || '');
            if (!target) return;
            target.value = chip.getAttribute('data-fill') || '';
            target.dispatchEvent(new Event('input', { bubbles: true }));
            target.focus();
        });
    });

    const validators = {
        SetupToken: v => (v.trim() ? '' : messages.token),
        Server: v => (!v.trim() ? messages.server : (v.length > 256 ? messages.serverLong : '')),
        Database: v => {
            if (!v.trim()) return messages.database;
            if (v.trim().length > 123) return messages.databaseLong;
            if (!allowedName(v.trim())) return messages.databaseUnsafe;
            return isSystemDatabase(v) ? messages.databaseSystem : '';
        },
        UserName: v => (isSql() && !v.trim() ? messages.user : ''),
        Password: v => (isSql() && !v ? messages.password : '')
    };

    const check = name => {
        const input = field(name);
        if (!input || input.disabled) return true;
        const text = validators[name](input.value);
        setError(name, text);
        return !text;
    };

    Object.keys(validators).forEach(name => {
        const input = field(name);
        if (!input) return;
        // Do not validate on the blur caused by pressing the submit button: the message would move the
        // button between mousedown and mouseup and swallow the click. The submit handler reports all.
        input.addEventListener('blur', event => {
            if (event.relatedTarget && event.relatedTarget === form.querySelector('[data-submit]')) return;
            check(name);
        });
        input.addEventListener('input', () => { if (input.getAttribute('aria-invalid')) check(name); });
    });

    const submit = form.querySelector('[data-submit]');
    const status = form.querySelector('[data-busy-status]');
    const idleText = submit ? submit.textContent : '';
    let busy = false;
    let failsafe = 0;

    const setBusy = on => {
        busy = on;
        if (!on) clearTimeout(failsafe);
        if (!submit) return;
        submit.disabled = on;
        submit.setAttribute('aria-busy', on ? 'true' : 'false');
        submit.textContent = on ? (submit.getAttribute('data-busy-text') || idleText) : idleText;
        if (status) status.textContent = on ? (submit.getAttribute('data-busy-text') || '') : '';
        form.classList.toggle('is-busy', on);
    };

    form.addEventListener('submit', event => {
        if (busy) { event.preventDefault(); return; }
        // Evaluate every field (no short-circuit) so all independent errors appear together.
        const results = Object.keys(validators).map(check);
        const firstInvalid = Object.keys(validators).find((name, index) => !results[index]);
        if (firstInvalid) {
            event.preventDefault();
            if (errorSummary) {
                const count = results.filter(ok => !ok).length;
                errorSummary.textContent = count + ' مورد در فرم نیاز به اصلاح دارد.';
            }
            field(firstInvalid).focus();
            return;
        }
        if (errorSummary) errorSummary.textContent = '';
        setBusy(true);
        // Failsafe: Esc/Stop or a dropped connection never leaves the button disabled for good.
        clearTimeout(failsafe);
        failsafe = setTimeout(() => setBusy(false), 120000);
    });

    // Back/forward cache may restore the page in the busy state; leaving the page ends it too.
    window.addEventListener('pageshow', () => setBusy(false));
    window.addEventListener('pagehide', () => setBusy(false));
    // Returning to a tab whose navigation was aborted (Esc/Stop): the server allows one setup at a
    // time, so a repeated click is answered safely instead of starting a second run.
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') setBusy(false);
    });

    // After a server-side failed POST, start at the first field the server marked invalid.
    const firstServerInvalid = form.querySelector('[aria-invalid="true"]:not(:disabled)');
    if (firstServerInvalid) firstServerInvalid.focus();
})();
