(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        // Accessible show/hide of the password field.
        var toggle = document.querySelector('[data-password-toggle]');
        var password = toggle ? document.getElementById(toggle.getAttribute('data-password-toggle')) : null;

        if (toggle && password) {
            toggle.addEventListener('click', function () {
                var visible = password.type === 'password';
                password.type = visible ? 'text' : 'password';

                var showLabel = toggle.getAttribute('data-label-show') || 'Show';
                var hideLabel = toggle.getAttribute('data-label-hide') || 'Hide';
                var showAria = toggle.getAttribute('data-aria-show') || 'Show password';
                var hideAria = toggle.getAttribute('data-aria-hide') || 'Hide password';

                toggle.textContent = visible ? hideLabel : showLabel;
                toggle.setAttribute('aria-label', visible ? hideAria : showAria);
                toggle.setAttribute('aria-pressed', String(visible));
            });
        }

        // One submit at a time: the button is disabled while the form posts.
        // This is only a usability guard - the server validates every request.
        var form = document.querySelector('.taraz-login-form');
        if (form) {
            form.addEventListener('submit', function () {
                var button = form.querySelector('.taraz-submit');
                if (!button || button.disabled) {
                    return;
                }

                if (!form.checkValidity()) {
                    return;
                }

                button.disabled = true;
                button.setAttribute('aria-busy', 'true');

                var label = button.querySelector('.taraz-submit-label');
                var status = button.querySelector('.taraz-submit-status');
                if (label) {
                    label.hidden = true;
                }
                if (status) {
                    status.hidden = false;
                }
            });

            // Back/forward cache: a restored page must not keep a disabled button.
            window.addEventListener('pageshow', function () {
                var button = form.querySelector('.taraz-submit');
                if (button && button.disabled) {
                    button.disabled = false;
                    button.removeAttribute('aria-busy');

                    var label = button.querySelector('.taraz-submit-label');
                    var status = button.querySelector('.taraz-submit-status');
                    if (label) {
                        label.hidden = false;
                    }
                    if (status) {
                        status.hidden = true;
                    }
                }
            });
        }

        // Move the focus to the first invalid field after a failed post.
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
