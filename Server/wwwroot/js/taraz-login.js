(function () {
    'use strict';

    function setPasswordVisibility(toggle, password, visible) {
        var showLabel = toggle.getAttribute('data-label-show') || 'نمایش رمز';
        var hideLabel = toggle.getAttribute('data-label-hide') || 'پنهان کردن رمز';
        var showAria = toggle.getAttribute('data-aria-show') || 'نمایش رمز';
        var hideAria = toggle.getAttribute('data-aria-hide') || 'پنهان کردن رمز';

        password.type = visible ? 'text' : 'password';
        toggle.textContent = visible ? hideLabel : showLabel;
        toggle.setAttribute('aria-label', visible ? hideAria : showAria);
        toggle.setAttribute('aria-pressed', String(visible));
        toggle.setAttribute('title', visible ? hideAria : showAria);

        var icon = toggle.querySelector('[data-password-icon]');
        if (!icon) {
            icon = document.createElement('span');
            icon.setAttribute('data-password-icon', 'true');
            icon.setAttribute('aria-hidden', 'true');
            toggle.insertBefore(icon, toggle.firstChild);
        }

        icon.textContent = visible ? '◉' : '◌';
    }

    document.addEventListener('DOMContentLoaded', function () {
        var form = document.querySelector('.taraz-login-form');
        var toggle = form ? form.querySelector('[data-password-toggle]') : null;
        var password = toggle ? document.getElementById(toggle.getAttribute('data-password-toggle')) : null;

        if (toggle && password) {
            setPasswordVisibility(toggle, password, false);
            toggle.addEventListener('click', function (event) {
                event.preventDefault();
                setPasswordVisibility(toggle, password, password.type === 'password');
            });
        }

        if (form) {
            form.addEventListener('submit', function () {
                var button = form.querySelector('.taraz-submit');
                if (!button || button.disabled || !form.checkValidity()) {
                    return;
                }

                button.disabled = true;
                button.setAttribute('aria-busy', 'true');

                var label = button.querySelector('.taraz-submit-label');
                var status = button.querySelector('.taraz-submit-status');
                if (label) label.hidden = true;
                if (status) status.hidden = false;
            });

            window.addEventListener('pageshow', function () {
                var button = form.querySelector('.taraz-submit');
                if (button && button.disabled) {
                    button.disabled = false;
                    button.removeAttribute('aria-busy');
                    var label = button.querySelector('.taraz-submit-label');
                    var status = button.querySelector('.taraz-submit-status');
                    if (label) label.hidden = false;
                    if (status) status.hidden = true;
                }
            });
        }

        var firstError = document.querySelector('.taraz-field input[aria-invalid="true"], .taraz-field-error:not(:empty)');
        if (firstError) {
            var invalidInput = firstError.tagName === 'INPUT'
                ? firstError
                : (firstError.closest('.taraz-field') || document).querySelector('input');
            if (invalidInput && typeof invalidInput.focus === 'function') {
                invalidInput.focus({ preventScroll: false });
            }
        }
    });
}());
