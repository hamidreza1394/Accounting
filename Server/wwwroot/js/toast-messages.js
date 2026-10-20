// Shows the server-side toast messages (Shared/PartialViews/_DisplayToastMessages.cshtml).
// Kept in a file so that the pages need no inline script (Content-Security-Policy).
window.addEventListener('load', () => {
	document.querySelectorAll('.toast').forEach((toastElement) => {
		new bootstrap.Toast(toastElement).show();
	});
});
