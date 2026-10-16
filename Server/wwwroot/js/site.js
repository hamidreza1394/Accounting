$(function () {
	$('form').on('submit', function () {
		if ($(this).valid()) {
			$('button').prop('disabled', true);
			$('input[type=button]').prop('disabled', true);
		}
	});

	$('[data-password-toggle]').on('click', function () {
		const button = $(this);
		const passwordId = button.attr('data-password-toggle');
		const password = document.getElementById(passwordId);
		const icon = button.find('i');

		if (!password) {
			return;
		}

		const isPassword = password.getAttribute('type') === 'password';
		password.setAttribute('type', isPassword ? 'text' : 'password');
		icon.toggleClass('bi-eye bi-eye-slash');
	});

	$('[data-login-submit]').on('click', function () {
		const button = $(this);
		const form = button.closest('form');

		if (form.valid()) {
			button.find('[data-login-spinner]').removeClass('d-none');
			button.find('.login-submit-label').addClass('visually-hidden');
		}
	});
});

// ------------------------- TINY MCE -------------------------
if (typeof tinymce !== 'undefined') {
	tinymce.init({
		skin: 'oxide',
		selector: '.tinymce',
		language: 'fa',
		plugins: 'image code link save table wordcount emoticons directionality insertdatetime nonbreaking pagebreak searchreplace visualblocks',
		toolbar: 'undo redo | link image | code table | save searchreplace | bold italic underline | bullist numlist | blockquote quicklink | blocks | alignleft aligncentre alignright alignjustify | indent outdent | bullist numlist'
	});
}
