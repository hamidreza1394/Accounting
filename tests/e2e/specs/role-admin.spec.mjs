// Real Chromium -> published Production server -> disposable SQL Server; no API mocks.
import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { randomBytes, randomUUID, pbkdf2Sync } from 'node:crypto';
import sql from 'mssql';

const state = JSON.parse(readFileSync(new URL('../.e2e-state.json', import.meta.url), 'utf8'));
const { a, b } = state.companies;

async function signIn(page, user, { rememberMe = false } = {}) {
  await page.goto('/Account/Login');
  await page.locator('#ViewModel_Username').fill(user.username);
  await page.locator('#ViewModel_Password').fill(user.password);
  // Set the option explicitly: a remembered username comes back with the box already ticked.
  const box = page.locator('#ViewModel_RememberMe');
  if ((await box.isChecked()) !== rememberMe) {
    await page.locator('label.taraz-remember').click();
  }
  await expect(box).toBeChecked({ checked: rememberMe });
  // Wait for the form POST navigation to finish, whether authentication succeeds (redirect)
  // or fails (the login page is rendered again), before callers navigate or clear cookies.
  await Promise.all([
    page.waitForNavigation({ waitUntil: 'load' }),
    page.locator('form.taraz-login-form button[type="submit"]').click(),
  ]);
}

async function callApi(page, method, path, body, token) {
  return page.evaluate(async ({ method, path, body, token }) => {
    const headers = {};
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (token?.headerName && token?.requestToken) headers[token.headerName] = token.requestToken;
    const response = await fetch(path, {
      method,
      credentials: 'same-origin',
      headers,
      ...(body === undefined ? {} : { body: JSON.stringify(body) }),
    });
    const text = await response.text();
    let payload = null;
    if (text) {
      try { payload = JSON.parse(text); } catch { payload = text; }
    }
    return { status: response.status, body: payload };
  }, { method, path, body, token });
}


// The account is created inside the test and references the existing active Admin role. The
// disposable fixture uses dedicated identifiers and never changes the shared admin/Alice/Bob.
// Do not retain login credentials or antiforgery tokens in failure artifacts.
test.use({ trace: 'off', screenshot: 'off' });
test('an installation administrator can create, read and update company roles against SQL Server', async ({ page }) => {
  test.setTimeout(60_000);
  const account = state.roleAdmin;
  const roleCode = `E2E-${randomBytes(12).toString('hex').toUpperCase()}`;
  const rolesPath = `/api/v1/admin/companies/${a.id}/roles`;
  // Refuse to run fixture SQL on anything other than the disposable E2E database.
  const connectionString = process.env.FARANOU_E2E_SQL;
  if (!connectionString || !/Database=FaranouE2E/i.test(connectionString)) throw new Error('Disposable E2E database required');
  const pool = await new sql.ConnectionPool(connectionString).connect();
  const query = (text) => pool.request()
    .input('user', sql.UniqueIdentifier, account.id)
    .input('globalRole', sql.UniqueIdentifier, state.users.admin.roleId)
    .input('company', sql.UniqueIdentifier, a.id)
    .input('code', sql.VarChar(50), roleCode)
    .query(text);
  try {
    // ASP.NET Core Identity V3 PBKDF2 hash, matching the shared fixture's password format.
    const salt = randomBytes(16);
    const header = Buffer.alloc(13);
    header.writeUInt8(1, 0);
    header.writeUInt32BE(2, 1);
    header.writeUInt32BE(100_000, 5);
    header.writeUInt32BE(salt.length, 9);
    const hash = Buffer.concat([header, salt, pbkdf2Sync(account.password, salt, 100_000, 32, 'sha512')]).toString('base64');
    await pool.request().input('id', sql.UniqueIdentifier, account.id)
      .input('role', sql.UniqueIdentifier, state.users.admin.roleId)
      .input('username', sql.VarChar(20), account.username)
      .input('password', sql.VarChar(255), hash)
      .input('email', sql.VarChar(254), `${account.username}@e2e.test`)
      .input('key', sql.UniqueIdentifier, randomUUID())
      .query(`INSERT INTO Users (Id, RoleId, Ordering, InsertDateTime, UpdateDateTime, IsActive, IsSystemic, IsProgrammer, IsUndeletable,
        IsProfilePublic, IsVisibleInContactUsPage, IsEmailAddressVerified, IsCellPhoneNumberVerified,
        Username, Password, FirstName, LastName, EmailAddress, EmailAddressVerificationKey)
        VALUES (@id, @role, 10000, SYSDATETIME(), SYSDATETIME(), 1, 0, 0, 0, 0, 0, 1, 0,
        @username, @password, N'E2E', N'Admin', @email, @key)`);

    await signIn(page, account);
    await page.goto('/app/');
    const catalog = await callApi(page, 'GET', '/api/v1/admin/permission-catalog');
    expect(catalog.status).toBe(200);
    expect(catalog.body.some((permission) => permission.code === 'corporate-groups.view')).toBe(true);

    // The existing authenticated cookie must not override the current database role record.
    // D-071: without the administrator role and without any company membership the catalog
    // hides itself with a body-less 404 instead of disclosing an explicit refusal reason.
    await query('UPDATE Users SET RoleId = NULL WHERE Id = @user');
    expect((await callApi(page, 'GET', '/api/v1/admin/permission-catalog')).status).toBe(404);
    await query('UPDATE Users SET RoleId = @globalRole WHERE Id = @user');
    expect((await callApi(page, 'GET', '/api/v1/admin/permission-catalog')).status).toBe(200);

    const initialRoles = await callApi(page, 'GET', rolesPath);
    expect(initialRoles.status).toBe(200);
    expect(initialRoles.body.some((role) => role.id === a.roleId)).toBe(true);
    const createBody = { code: roleCode, name: 'نقش آزمون مدیر نصب', templateCode: null, permissionCodes: ['corporate-groups.view'] };
    expect((await callApi(page, 'POST', rolesPath, createBody)).status).toBe(400);
    const afterRejected = await callApi(page, 'GET', rolesPath);
    expect(afterRejected.status).toBe(200);
    expect(afterRejected.body.some((role) => role.code === roleCode)).toBe(false);

    const antiforgery = await callApi(page, 'GET', '/api/v1/antiforgery/token');
    expect(antiforgery.status).toBe(200);
    const created = await callApi(page, 'POST', rolesPath, createBody, antiforgery.body);
    expect(created.status).toBe(201);
    expect(created.body.id).toBeTruthy();
    const afterCreate = await callApi(page, 'GET', rolesPath);
    expect(afterCreate.status).toBe(200);
    expect(afterCreate.body.find((role) => role.id === created.body.id)).toMatchObject({
      code: roleCode, name: createBody.name, isSystemRole: false, isActive: true,
      permissionCodes: ['corporate-groups.view'],
    });
    const updated = await callApi(page, 'PUT', `${rolesPath}/${created.body.id}`, {
      name: 'نقش آزمون به‌روزشده', isActive: true, permissionCodes: [],
    }, antiforgery.body);
    expect(updated.status).toBe(204);
    const afterUpdate = await callApi(page, 'GET', rolesPath);
    expect(afterUpdate.status).toBe(200);
    expect(afterUpdate.body.find((role) => role.id === created.body.id)).toMatchObject({
      name: 'نقش آزمون به‌روزشده', isActive: true, permissionCodes: [],
    });

    const foreignBefore = await callApi(page, 'GET', `/api/v1/admin/companies/${b.id}/roles`);
    expect(foreignBefore.status).toBe(200);
    const foreignRole = foreignBefore.body.find((role) => role.id === b.roleId);
    expect(foreignRole).toBeTruthy();
    const foreignRoleUpdate = await callApi(page, 'PUT', `${rolesPath}/${b.roleId}`, {
      name: 'نباید تغییر کند', isActive: true, permissionCodes: [],
    }, antiforgery.body);
    expect(foreignRoleUpdate.status).toBe(404);
    const foreignAfter = await callApi(page, 'GET', `/api/v1/admin/companies/${b.id}/roles`);
    expect(foreignAfter.status).toBe(200);
    expect(foreignAfter.body.find((role) => role.id === b.roleId)).toEqual(foreignRole);
  } finally {
    // Match the random code AND company, even if POST succeeded but its response was lost.
    // Deleting the dedicated account also removes its disposable session/login/audit records.
    try {
      await query(`SET XACT_ABORT ON; BEGIN TRANSACTION;
        DELETE FROM CompanyRolePermissions WHERE RoleId IN (SELECT Id FROM CompanyRoles WHERE TenantId = @company AND Code = @code);
        DELETE FROM CompanyRoles WHERE TenantId = @company AND Code = @code;
        DELETE FROM SecurityAuditEvents WHERE UserId = @user;
        DELETE FROM UserSessions WHERE UserId = @user;
        DELETE FROM LoginLogs WHERE UserId = @user;
        DELETE FROM Users WHERE Id = @user;
        COMMIT TRANSACTION;`);
    } finally {
      await pool.close();
    }
  }
});
