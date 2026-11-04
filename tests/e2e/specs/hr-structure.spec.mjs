// Real Chromium -> published Production server -> disposable SQL Server; no API mocks.
// The company-scoped organizational structure (S2, D-072): the installation administrator
// and an HR-enabled company manager work against the real database, a member without the
// hr.* permissions is refused, and a foreign company id discloses nothing (404).
import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { randomBytes, randomUUID, pbkdf2Sync } from 'node:crypto';
import sql from 'mssql';

const state = JSON.parse(readFileSync(new URL('../.e2e-state.json', import.meta.url), 'utf8'));
const { a, b } = state.companies;

const HrPermissionCodes = [
  'hr.employees.list', 'hr.employees.view', 'hr.employees.create', 'hr.employees.update',
  'hr.employees.user-link.manage', 'hr.organizational-units.list', 'hr.organizational-units.create',
  'hr.organizational-units.update', 'hr.positions.list', 'hr.positions.create',
  'hr.positions.update', 'hr.assignments.manage',
];

async function signIn(page, user) {
  await page.goto('/Account/Login');
  await page.locator('#ViewModel_Username').fill(user.username);
  await page.locator('#ViewModel_Password').fill(user.password);
  const box = page.locator('#ViewModel_RememberMe');
  if (!(await box.isChecked())) {
    await page.locator('label.taraz-remember').click();
  }
  await expect(box).toBeChecked({ checked: true });
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

test.use({ trace: 'off', screenshot: 'off' });

test('the organizational structure works end to end on SQL Server and fails closed without permissions', async ({ page }) => {
  test.setTimeout(90_000);
  const hrManager = {
    id: randomUUID(),
    username: `hrm${randomBytes(5).toString('hex')}`,
    password: `Hr-${randomBytes(9).toString('base64url')}`,
    roleId: randomUUID(),
    membershipId: randomUUID(),
  };
  const plainMember = {
    id: randomUUID(),
    username: `hrp${randomBytes(5).toString('hex')}`,
    password: `Hr-${randomBytes(9).toString('base64url')}`,
    roleId: randomUUID(),
    membershipId: randomUUID(),
  };
  const hrRoleCode = `HR-${randomBytes(6).toString('hex').toUpperCase()}`;

  // Refuse to run fixture SQL on anything other than the disposable E2E database.
  const connectionString = process.env.FARANOU_E2E_SQL;
  if (!connectionString || !/Database=FaranouE2E/i.test(connectionString)) throw new Error('Disposable E2E database required');
  const pool = await new sql.ConnectionPool(connectionString).connect();
  const now = new Date();
  try {
    // ASP.NET Core Identity V3 PBKDF2 hash, matching the shared fixture's password format.
    // Company-scoped accounts only: RoleId stays NULL, otherwise every seeded account
    // would be an installation administrator and the scope assertions would be meaningless.
    const insertUser = async (account) => {
      const salt = randomBytes(16);
      const header = Buffer.alloc(13);
      header.writeUInt8(1, 0);
      header.writeUInt32BE(2, 1);
      header.writeUInt32BE(100_000, 5);
      header.writeUInt32BE(salt.length, 9);
      const hash = Buffer.concat([header, salt, pbkdf2Sync(account.password, salt, 100_000, 32, 'sha512')]).toString('base64');
      await pool.request().input('id', sql.UniqueIdentifier, account.id)
        .input('username', sql.VarChar(20), account.username)
        .input('password', sql.VarChar(255), hash)
        .input('email', sql.VarChar(254), `${account.username}@e2e.test`)
        .input('key', sql.UniqueIdentifier, randomUUID())
        .query(`INSERT INTO Users (Id, RoleId, Ordering, InsertDateTime, UpdateDateTime, IsActive, IsSystemic, IsProgrammer, IsUndeletable,
          IsProfilePublic, IsVisibleInContactUsPage, IsEmailAddressVerified, IsCellPhoneNumberVerified,
          Username, Password, FirstName, LastName, EmailAddress, EmailAddressVerificationKey)
          VALUES (@id, NULL, 10000, SYSDATETIME(), SYSDATETIME(), 1, 0, 0, 0, 0, 0, 1, 0,
          @username, @password, N'E2E', N'HR', @email, @key)`);
    };
    await insertUser(hrManager);
    await insertUser(plainMember);

    // Company a: one role carrying the hr.* catalog codes and one plain role without them.
    await pool.request().input('id', sql.UniqueIdentifier, hrManager.roleId)
      .input('tenant', sql.UniqueIdentifier, a.id)
      .input('code', sql.VarChar(50), hrRoleCode)
      .query(`INSERT INTO CompanyRoles (Id, TenantId, Ordering, InsertDateTime, Code, Name, IsSystemRole, IsActive)
        VALUES (@id, @tenant, 10000, SYSDATETIME(), @code, N'منابع انسانی E2E', 0, 1)`);
    await pool.request().input('id', sql.UniqueIdentifier, plainMember.roleId)
      .input('tenant', sql.UniqueIdentifier, a.id)
      .input('code', sql.VarChar(50), `PL-${randomBytes(6).toString('hex').toUpperCase()}`)
      .query(`INSERT INTO CompanyRoles (Id, TenantId, Ordering, InsertDateTime, Code, Name, IsSystemRole, IsActive)
        VALUES (@id, @tenant, 10000, SYSDATETIME(), @code, N'کارمند ساده E2E', 0, 1)`);

    for (const code of HrPermissionCodes) {
      await pool.request().input('role', sql.UniqueIdentifier, hrManager.roleId)
        .input('code', sql.VarChar(50), code)
        .query(`INSERT INTO CompanyRolePermissions (RoleId, PermissionId)
          SELECT @role, Id FROM PermissionDefinitions WHERE Code = @code`);
    }

    for (const account of [hrManager, plainMember]) {
      await pool.request().input('id', sql.UniqueIdentifier, account.membershipId)
        .input('user', sql.UniqueIdentifier, account.id)
        .input('tenant', sql.UniqueIdentifier, a.id)
        .input('role', sql.UniqueIdentifier, account.roleId)
        .query(`INSERT INTO UserCompanyMemberships (Id, Ordering, InsertDateTime, UserId, TenantId, RoleId, IsActive, CreatedAtUtc)
          VALUES (@id, 10000, SYSDATETIME(), @user, @tenant, @role, 1, SYSDATETIME())`);
    }

    // ---- The installation administrator works without any membership (D-071 dual mode).
    await signIn(page, state.users.admin);
    await page.goto('/app/');
    const antiforgery = await callApi(page, 'GET', '/api/v1/antiforgery/token');
    expect(antiforgery.status).toBe(200);

    const employeesPath = `/api/v1/companies/${a.id}/org/employees`;
    const unitsPath = `/api/v1/companies/${a.id}/org/units`;
    const positionsPath = `/api/v1/companies/${a.id}/org/positions`;
    const assignmentsPath = `/api/v1/companies/${a.id}/org/assignments`;

    const employee = await callApi(page, 'POST', employeesPath, {
      employeeNumber: `E2E-${randomBytes(4).toString('hex').toUpperCase()}`,
      firstName: 'علی', lastName: 'محمدی', hiredOnUtc: null,
    }, antiforgery.body);
    expect(employee.status).toBe(201);
    expect(employee.body.id).toBeTruthy();

    const unit = await callApi(page, 'POST', unitsPath, {
      code: `U-${randomBytes(4).toString('hex').toUpperCase()}`, name: 'دفتر مرکزی E2E',
      description: null, parentUnitId: null, validFromUtc: null, validToUtc: null,
    }, antiforgery.body);
    expect(unit.status).toBe(201);

    const position = await callApi(page, 'POST', positionsPath, {
      code: `P-${randomBytes(4).toString('hex').toUpperCase()}`, name: 'حسابدار E2E',
      description: null, rank: null,
    }, antiforgery.body);
    expect(position.status).toBe(201);

    const started = await callApi(page, 'POST', assignmentsPath, {
      employeeId: employee.body.id, organizationalUnitId: unit.body.id, positionId: position.body.id,
      managerEmployeeId: null, startOnUtc: new Date().toISOString(), endActive: false,
    }, antiforgery.body);
    expect(started.status).toBe(201);

    // A second active assignment is refused until the current one ends.
    const second = await callApi(page, 'POST', assignmentsPath, {
      employeeId: employee.body.id, organizationalUnitId: unit.body.id, positionId: position.body.id,
      managerEmployeeId: null, startOnUtc: new Date().toISOString(), endActive: false,
    }, antiforgery.body);
    expect(second.status).toBe(409);

    const detail = await callApi(page, 'GET', `${employeesPath}/${employee.body.id}`);
    expect(detail.status).toBe(200);
    expect(detail.body.activeAssignment.id).toBe(started.body.id);
    expect(detail.body.activeAssignment.organizationalUnitId).toBe(unit.body.id);
    expect(detail.body.rowVersion).toBeTruthy();

    // The history endpoint serves the append-only rows without rewriting anything.
    const history = await callApi(page, 'GET', `${assignmentsPath}?employeeId=${employee.body.id}`);
    expect(history.status).toBe(200);
    expect(history.body.totalCount).toBe(1);
    expect(history.body.items[0].status).toBe(1);

    // Terminating the employee ends the active assignment in one transaction and never
    // touches the linked membership.
    expect((await callApi(page, 'POST', `${employeesPath}/${employee.body.id}/deactivate`, undefined, antiforgery.body)).status).toBe(204);
    const afterDeactivate = await callApi(page, 'GET', `${employeesPath}/${employee.body.id}`);
    expect(afterDeactivate.body.employmentStatus).toBe(3);
    const historyAfterEnd = await callApi(page, 'GET', `${assignmentsPath}?employeeId=${employee.body.id}`);
    expect(historyAfterEnd.body.items[0].status).toBe(2);

    // ---- The company manager with hr.* permissions manages their own company.
    await page.goto('/Account/Logout');
    await signIn(page, hrManager);
    await page.goto('/app/');
    const managerAntiforgery = await callApi(page, 'GET', '/api/v1/antiforgery/token');
    const managerEmployee = await callApi(page, 'POST', employeesPath, {
      employeeNumber: `E2E-${randomBytes(4).toString('hex').toUpperCase()}`,
      firstName: 'مریم', lastName: 'احمدی', hiredOnUtc: null,
    }, managerAntiforgery.body);
    expect(managerEmployee.status).toBe(201);
    expect((await callApi(page, 'GET', employeesPath)).status).toBe(200);

    // A company manager of company a stays inside their scope: company b is a 404 without
    // disclosure, exactly like a company that does not exist.
    expect((await callApi(page, 'GET', `/api/v1/companies/${b.id}/org/employees`)).status).toBe(404);

    // ---- A member without hr.* permissions is refused explicitly (403).
    await page.goto('/Account/Logout');
    await signIn(page, plainMember);
    await page.goto('/app/');
    expect((await callApi(page, 'GET', employeesPath)).status).toBe(403);
  } finally {
    await pool.close();
  }
});
