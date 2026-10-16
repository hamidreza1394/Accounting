$(function () {
	$('.login-form').on('submit', function (event) {
		const form = $(this);

		if (!form.valid()) {
			return;
		}

		event.preventDefault();
		form.find('button').prop('disabled', true);
		form.find('[data-login-spinner]').removeClass('d-none');
		form.find('.login-submit-label').addClass('visually-hidden');
		form[0].submit();
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
