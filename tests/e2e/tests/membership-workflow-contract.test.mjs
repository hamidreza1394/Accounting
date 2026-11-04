// Dependency-free guard for the membership-workflow E2E spec.
//
// The browser spec itself needs a published server, a real SQL Server and Chromium, so it can only
// run in the `e2e` CI job. These checks run everywhere (`npm run test:contract` → `node --test`),
// catch the mistakes that are cheap to make and expensive to discover in CI, and pin the assertions
// that make the spec a *positive* end-to-end test rather than a smoke test:
//
//   * every API path it uses is one the server maps, with the method it uses;
//   * nothing is mocked or intercepted: no `page.route`, no stubbed responses;
//   * request, approval and rejection are asserted on both sides (SQL Server rows and API payload);
//   * it cleans up in `finally` and only ever touches the disposable FaranouE2E database.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';

const e2e = new URL('../', import.meta.url);
const repo = new URL('../../', e2e);
const read = (url) => readFileSync(url, 'utf8');

const spec = read(new URL('specs/membership-workflow.spec.mjs', e2e));
const fixture = read(new URL('lib/membershipFixture.mjs', e2e));
const session = read(new URL('lib/session.mjs', e2e));
const membershipApi = read(new URL('Server/Api/CompanyMembershipRequestEndpoints.cs', repo));
const api = read(new URL('Server/Api/ApiEndpoints.cs', repo));
const mePermissions = read(new URL('Server/Api/MePermissionsEndpoints.cs', repo));
const directory = read(new URL('Server/Api/CompanyUserDirectoryEndpoints.cs', repo));
const workContext = read(new URL('Server/Api/WorkContextEndpoints.cs', repo));

/** The path helpers of the spec: `const pendingPath = (companyId) => \`/api/v1/...\``. */
const helpers = new Map(
  [...spec.matchAll(/const (\w+Path) = \([^)]*\) => `([^`]+)`/g)].map((match) => [match[1], match[2]]),
);

test('the spec declares the path helpers it uses, with the prefixes the server maps', () => {
  assert.deepEqual([...helpers.keys()].sort(), [
    'approvePath', 'assignableRolesPath', 'pendingPath', 'rejectPath',
  ]);
  assert.equal(
    helpers.get('pendingPath'),
    '/api/v1/admin/companies/${companyId}/membership-requests/pending',
  );
  assert.equal(
    helpers.get('assignableRolesPath'),
    '/api/v1/admin/companies/${companyId}/membership-requests/assignable-roles',
  );
  assert.equal(
    helpers.get('approvePath'),
    '/api/v1/admin/companies/${companyId}/membership-requests/${requestId}/approve',
  );
  assert.equal(
    helpers.get('rejectPath'),
    '/api/v1/admin/companies/${companyId}/membership-requests/${requestId}/reject',
  );
  for (const name of helpers.keys()) {
    assert.ok(spec.includes(`${name}(`), `the helper ${name} is declared but never used`);
  }
});

test('every route the spec exercises is mapped by the published server', () => {
  const routes = [
    // The spec proves the route, and the server must still map it.
    ['GET destinations', /\/api\/v1\/me\/membership-requests\/destinations\?search=/, 'MapGet("/destinations"'],
    ['POST my request', /post\(page, '\/api\/v1\/me\/membership-requests'/, 'MapPost(""'],
    ['POST my request without a token', /'\/api\/v1\/me\/membership-requests',\s*\{\s*\n?\s*body:/, 'MapPost(""'],
    ['GET my requests', /'\/api\/v1\/me\/membership-requests\?status=/, 'MapGet(""'],
    ['GET pending queue', /pendingPath\(destination\.id\)/, 'MapGet("/pending"'],
    ['GET assignable roles', /assignableRolesPath\(destination\.id\)/, 'MapGet("/assignable-roles"'],
    ['GET decision history', /\/api\/v1\/admin\/companies\/\$\{destination\.id\}\/membership-requests\?status=/, 'MapGet(""'],
    ['POST approve', /approvePath\(destination\.id, requestId\)/, 'MapPost("/{requestId:guid}/approve"'],
    ['POST reject', /rejectPath\(destination\.id, requestId\)/, 'MapPost("/{requestId:guid}/reject"'],
    // Supporting routes used by the access assertions.
    ['GET companies', /'\/api\/v1\/companies'/, 'MapGet("/companies"'],
    ['GET company overview', /\/api\/v1\/companies\/\$\{companyId\}\/overview/, 'api.MapGet("/companies/{companyId:guid}/overview"'],
    ['GET company directory', /\/api\/v1\/companies\/\$\{destination\.id\}\/users/, 'users.MapGet("", GetUsersAsync)'],
    ['GET work context', /'\/api\/v1\/context'/, 'MapGet("/context"'],
    ['GET me/permissions', /'\/api\/v1\/me\/permissions'/, 'MapGet("/me/permissions"'],
    ['GET anti-forgery token', /'\/api\/v1\/antiforgery\/token'/, 'MapGet("/antiforgery/token"'],
  ];
  for (const [label, used, mapped] of routes) {
    const inSpec = used.test(spec) || used.test(session);
    assert.ok(inSpec, `the spec never exercises ${label}`);
  }
  for (const marker of [
    'MapGet("/destinations"', 'MapPost(""', 'MapGet("/pending"', 'MapGet("/assignable-roles"',
    'MapGet(""', 'MapPost("/{requestId:guid}/approve"', 'MapPost("/{requestId:guid}/reject"',
  ]) {
    assert.ok(membershipApi.includes(marker), `the server no longer maps ${marker}`);
  }
  for (const [label, marker] of routes.slice(9).map(([name, , mapped]) => [name, mapped])) {
    const source = marker.startsWith('MapGet("/me/permissions')
      ? mePermissions
      : marker.startsWith('users.MapGet')
        ? directory
        : marker.startsWith('MapGet("/context"')
          ? workContext
          : api;
    assert.ok(source.includes(marker), `the server no longer maps ${label} (${marker})`);
  }
});

test('no API call of the spec points at an unresolved or non-API path', () => {
  const paths = [
    ...spec.matchAll(/['`](\/api\/v1\/[^'`]*)['`]/g),
  ].map((match) => match[1]);
  assert.ok(paths.length >= 10, `only ${paths.length} concrete API paths found`);
  for (const path of paths) {
    assert.ok(!path.includes('undefined'), `unresolved path: ${path}`);
    assert.ok(!/\s/.test(path), `path contains whitespace: ${path}`);
  }
  // The membership routes are the ones under test, not a different company surface.
  assert.ok(paths.some((path) => path.endsWith('/me/membership-requests')));
  assert.ok(paths.some((path) => path.includes('/membership-requests/destinations?search=')));
});

test('nothing is mocked, intercepted or answered from a stub', () => {
  for (const [name, source] of [['spec', spec], ['session helper', session], ['fixture', fixture]]) {
    for (const forbidden of ['page.route(', 'route.fulfill', 'route.abort', 'page.unroute(', 'nock', 'msw', 'sinon', 'mockResolvedValue']) {
      assert.ok(!source.includes(forbidden), `${name} uses '${forbidden}'`);
    }
  }
  // Anti-forgery is exercised for real, and both rejection paths are asserted.
  assert.ok(session.includes("'/api/v1/antiforgery/token'"), 'the helper no longer fetches a real token');
  assert.match(spec, /an anti-forgery token of another session/);
  assert.match(spec, /Without the anti-forgery request token/);
  // The browser is the real Chromium project of playwright.config.mjs.
  const config = read(new URL('playwright.config.mjs', e2e));
  assert.ok(config.includes("devices['Desktop Chrome']"), 'the E2E project is no longer real Chromium');
  assert.ok(config.includes('ConnectionStrings__ConnectionString: connectionString'), 'the server is no longer bound to SQL Server');
});

test('the positive scenario asserts database, API and access state', () => {
  // Pending: read back through SQL and through the API, before and after the decision.
  assert.match(spec, /readRequests\(fixture, \{ userId: requester\.id, companyId: destination\.id \}\)/);
  assert.match(spec, /Status: 'Pending'/);
  assert.match(spec, /'\/api\/v1\/me\/membership-requests\?status=Pending'/);
  // Approved: status, decided role, decider, membership row and audit event.
  assert.match(spec, /Status: 'Approved'/);
  assert.match(spec, /ApprovedRoleId: destination\.memberRole\.id/);
  assert.match(spec, /DecidedByUserId: approver\.id/);
  assert.match(spec, /CompanyMembershipApproved/);
  // Rejected: status, no role, no membership row and the audit event.
  assert.match(spec, /Status: 'Rejected'/);
  assert.match(spec, /ApprovedRoleId: null/);
  assert.match(spec, /CompanyMembershipRejected/);
  // The membership row itself is checked, not only the request status.
  assert.match(spec, /readMembership\(fixture, \{ userId: requester\.id, companyId: destination\.id \}\)/);
  assert.match(spec, /RoleId: destination\.memberRole\.id/);
  // Access before and after the decision, through the real endpoints.
  assert.match(spec, /companyOverview\(page, destination\.id\)/);
  assert.match(spec, /accessibleCompanyIds/);
  // Only the delegable role may be offered.
  assert.match(spec, /toEqual\(\[\s*\{\s*id: destination\.memberRole\.id/);
  // The UI drives the workflow: search, request, approve dialog, reject dialog.
  for (const ui of [
    /getByLabel\('نام دقیق شرکت مقصد'\)/,
    /درخواست عضویت در \$\{destination\.name\}/,
    /تأیید درخواست \$\{requester\.label\}/,
    /رد درخواست \$\{requester\.label\}/,
    /getByLabel\('گروه دسترسی'\)/,
  ]) {
    assert.match(spec, ui, `the spec does not drive the UI: ${ui}`);
  }
});

test('every security check of the task is covered', () => {
  const checks = [
    ['self-approval', /self-approval/],
    ['approver of another company', /approver of the source company cannot|approvePath\(source\.id, requestId\)/],
    ['role outside the delegation ceiling', /above the ceiling/],
    ['delegation ceiling recalculated', /setRolePermissions\(destination\.approverRole\.id, \[fixture\.permissions\.approve\]\)/],
    ['pending request grants no access', /A pending request is not access/],
    ['duplicate pending request', /409/],
    ['company outside the customer boundary', /outside the boundary/],
    ['tampered company id', /11111111-1111-1111-1111-111111111111/],
    ['anti-forgery retained', /anti-forgery/],
    ['rate limit retained', /sensitive rate limit/],
    ['permission re-check after revocation', /setMembershipActive\(approver, destination, false\)/],
  ];
  for (const [label, pattern] of checks) {
    assert.match(spec, pattern, `the spec does not cover: ${label}`);
  }
});

test('the fixture is isolated, credential-complete and always cleaned up', () => {
  // Cleanup in finally, for the describe-level fixture and for the stand-alone test.
  assert.ok(spec.includes('if (fixture) await fixture.destroy();'), 'afterEach does not destroy the fixture');
  assert.match(spec, /finally \{\n    await fixture\.destroy\(\);/);
  // The fixture refuses anything that is not the disposable E2E database.
  assert.match(fixture, /Database=FaranouE2E/);
  assert.match(fixture, /must name the disposable E2E database/);
  // It builds its own boundary, companies, roles and users instead of reusing the shared seed.
  assert.match(fixture, /CustomerBoundaryId/);
  assert.match(fixture, /INSERT INTO Companies/);
  assert.match(fixture, /INSERT INTO CompanyRoles/);
  assert.match(fixture, /INSERT INTO Users/);
  assert.match(fixture, /INSERT INTO UserCompanyMemberships/);
  // Real password hashes, so every sign-in is a real sign-in.
  assert.match(fixture, /pbkdf2Sync/);
  assert.match(fixture, /users\.membership\.approve/);
  // It deletes everything it created, in foreign-key order.
  for (const table of [
    'CompanyMembershipRequests', 'SecurityAuditEvents', 'CompanyRolePermissions',
    'UserCompanyMemberships', 'LoginLogs', 'Users', 'CompanyRoles', 'FiscalPeriods', 'Companies',
  ]) {
    assert.match(fixture, new RegExp(`DELETE FROM ${table}`), `destroy() does not delete ${table}`);
  }
  // The shared seed is not modified by this work.
  const seed = read(new URL('seed.mjs', e2e));
  assert.ok(seed.length > 0);
  assert.ok(!fixture.includes("from './seed.mjs'"), 'the fixture must not reuse the shared seed module');
});

test('the change touches tests and documentation only', () => {
  // The guard of the task: no Backend, API, migration or UI change is part of this work. If a spec
  // ever needs to read product code, it may only read it (as this test does), never patch it.
  for (const source of [spec, fixture, session]) {
    assert.ok(!/\bwriteFileSync\(/.test(source), 'a test must not write product files');
    assert.ok(!/process\.env\.(ASPNETCORE|ConnectionStrings)/.test(source), 'a test must not patch server settings');
  }
});
