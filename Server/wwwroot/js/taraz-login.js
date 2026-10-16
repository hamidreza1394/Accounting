(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var toggle = document.querySelector('[data-password-toggle]');
        var password = toggle ? document.getElementById(toggle.getAttribute('data-password-toggle')) : null;

        if (toggle && password) {
            toggle.addEventListener('click', function () {
                var visible = password.type === 'password';
                password.type = visible ? 'text' : 'password';
                toggle.textContent = visible ? 'پنهان‌کردن' : 'نمایش';
                toggle.setAttribute('aria-label', visible ? 'پنهان‌کردن رمز عبور' : 'نمایش رمز عبور');
                toggle.setAttribute('aria-pressed', String(visible));
            });
        }

        var form = document.querySelector('.taraz-login-form');
        if (form) {
            form.addEventListener('submit', function () {
                if (!form.checkValidity()) return;
                var button = form.querySelector('.taraz-submit');
                if (!button || button.disabled) return;
                button.disabled = true;
                button.querySelector('.taraz-submit-label').hidden = true;
                button.querySelector('.taraz-submit-status').hidden = false;
            });
        }
    });
}());