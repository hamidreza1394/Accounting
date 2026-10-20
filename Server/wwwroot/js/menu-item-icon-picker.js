// Icon picker of the admin menu-item forms (Pages/Admin/MenuItems/Create|Update).
// Kept in a file so that the pages need no inline script (Content-Security-Policy); the
// localized captions come from data-iconpicker-* attributes of the input.
(() => {
	const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (character) => ({
		'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
	})[character]);

	document.querySelectorAll('.iconpicker').forEach((input) => {
		const data = input.dataset;

		$(input).iconpicker({
			title: data.iconpickerTitle ?? '',
			selected: false,
			defaultValue: false,
			placement: 'bottom',
			collision: 'none',
			animation: true,
			hideOnSelect: true,
			showFooter: false,
			searchInFooter: false,
			mustAccept: false,
			selectedCustomClass: 'bg-primary',
			fullClassFormatter: (value) => value,
			input: 'input,.iconpicker-input',
			inputSearch: false,
			container: false,
			component: '.input-group-addon,.iconpicker-component',
			templates: {
				footer: '<div class="popover-footer"></div>',
				iconpicker: '<div class="iconpicker"><div class="iconpicker-items"></div></div>',
				// href="#" instead of a script URL, which the CSP blocks.
				iconpickerItem: '<a role="button" href="#" class="iconpicker-item"><i></i></a>',
				search: '<input type="search" class="form-control iconpicker-search" placeholder="' + escapeHtml(data.iconpickerSearch) + '" />',
				popover: '<div class="iconpicker-popover popover" role="tooltip"><div class="arrow"></div><div class="popover-title"></div><div class="popover-content"></div></div>',
				buttons: '<button class="iconpicker-btn iconpicker-btn-cancel btn btn-default btn-sm">' + escapeHtml(data.iconpickerCancel) + '</button>'
					+ ' <button class="iconpicker-btn iconpicker-btn-accept btn btn-primary btn-sm">' + escapeHtml(data.iconpickerAccept) + '</button>',
			},
		});
	});
})();
