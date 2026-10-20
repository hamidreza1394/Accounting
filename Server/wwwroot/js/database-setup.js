// Shows the SQL login fields only for SQL authentication (Pages/DatabaseSetup.cshtml).
(() => {
    const mode = document.getElementById('authMode');
    if (!mode) return;
    const fields = document.querySelectorAll('.sql-only');
    const refresh = () => fields.forEach(field => field.hidden = mode.value !== 'Sql');
    mode.addEventListener('change', refresh);
    refresh();
})();
