// End-to-end: the *positive* company-membership workflow, in a real browser (Chromium) against the
// published server (Production environment) and a real SQL Server (#444, #510, #511, D-065).
//
// Covered here, with no mocked API and no database shortcut for the workflow itself:
//   1. a user requests membership of a company inside its own customer boundary, discovered through
//      the real destinations API and submitted from the real UI;
//   2. a pending request grants nothing: no membership row, no destination-company access;
//   3. the destination approver sees the request, receives only the assignable roles, picks one and
//      approves; the request becomes Approved and an active membership with that role exists;
//   4. the requester can then access the destination company;
//   5. a second, independent request is rejected: Rejected in API and database, no membership, no
//      access;
//   6. security checks: duplicate pending request, a company id outside the boundary, a tampered
//      company id, a missing anti-forgery token, a token of another session, a role outside the
//      approver's delegation ceiling, self-approval, approval by an approver of another company, and
//      a revocation that happens before the approval.
//
// Fixtures are isolated per test (tests/e2e/lib/membershipFixture.mjs) and deleted in `finally`.
import { expect, test } from '@playwright/test';
import {
  createMembershipFixture,
  readAudit,
  readMembership,
  readRequests,
  readRolePermissions,
} from '../lib/membershipFixture.mjs';
import { antiforgery, callApi, DASHBOARD_HEADING, enterWorkspace, post, watchPage } from '../lib/session.mjs';

const ACCEPT_BUTTON = 'تأیید و ایجاد عضویت';

const pendingPath = (companyId) => `/api/v1/admin/companies/${companyId}/membership-requests/pending`;
// Typed explicitly: the approval body must carry `roleId` as a JSON string, and sending a Guid type
// here would be a silent 400 instead of the assertion this spec is about.
const roleBody = (roleId) => ({ roleId: String(roleId) });
const assignableRolesPath = (companyId) => `/api/v1/admin/companies/${companyId}/membership-requests/assignable-roles`;
const approvePath = (companyId, requestId) => `/api/v1/admin/companies/${companyId}/membership-requests/${requestId}/approve`;
const rejectPath = (companyId, requestId) => `/api/v1/admin/companies/${companyId}/membership-requests/${requestId}/reject`;

/** Proves the signed-in user really has (or has not) operational access to a company. */
async function companyOverview(page, companyId) {
  return callApi(page, 'GET', `/api/v1/companies/${companyId}/overview`);
}

/** The companies the server says the signed-in user may work in. */
async function accessibleCompanyIds(page) {
  const result = await callApi(page, 'GET', '/api/v1/companies');
  expect(result.status, 'GET /api/v1/companies').toBe(200);
  return result.body.map((company) => company.id);
}

/** A second, independent browser context (a different user, or a different session). */
async function openContext(browser, page) {
  return browser.newContext({
    baseURL: new URL(page.url()).origin,
    locale: 'fa-IR',
    timezoneId: 'Asia/Tehran',
  });
}

test.describe('membership workflow: request, approve, reject', () => {
  let fixture;

  test.beforeEach(async () => {
    fixture = await createMembershipFixture('جریان');
  });

  test.afterEach(async () => {
    if (fixture) await fixture.destroy();
    fixture = undefined;
  });

  test('the approver approves a pending request with a delegable role and the requester gains access', async ({ page, browser }) => {
    test.setTimeout(240_000);
    const watch = await watchPage(page);
    const { source, destination, approver } = fixture;
    const requester = await fixture.addUser('requester');
    // The requester is an active member of the SOURCE company only: no access to the destination.
    await fixture.addMembership(requester, source, source.memberRole.id);

    // ------------------------------------------------------------- 1. the requester signs in
    // One company with one open year: the server tells the app to enter it directly. If the
    // production rule for direct entry ever changes, this fails with a clear message instead of a
    // confusing 'heading not found'.
    expect(
      await enterWorkspace(page, requester),
      'the requester must enter its single company directly (server rule: one company, one open period)',
    ).toBe('dashboard');
    await expect(page.getByRole('heading', { name: DASHBOARD_HEADING })).toBeVisible();
    expect(await accessibleCompanyIds(page)).toEqual([source.id]);

    // ------------------------------------------- 2. destination discovery through the real API
    const discovered = await callApi(
      page, 'GET', `/api/v1/me/membership-requests/destinations?search=${encodeURIComponent(destination.name)}`,
    );
    expect(discovered.status).toBe(200);
    expect(discovered.body.items).toEqual([{ companyId: destination.id, name: destination.name }]);
    expect(discovered.cacheControl).toBe('no-store');
    // A company with the *same name* but another customer boundary is never disclosed: the search
    // for that name returns only the destination company, which is inside the requester's boundary.
    const sameName = await callApi(
      page, 'GET', `/api/v1/me/membership-requests/destinations?search=${encodeURIComponent(fixture.impostor.name)}`,
    );
    expect(sameName.status).toBe(200);
    expect(sameName.body.items.map((item) => item.companyId)).toEqual([destination.id]);
    expect(sameName.body.totalCount).toBe(1);

    // A company outside the boundary, and one without a boundary, cannot be requested directly
    // either: both answer exactly like a company that does not exist.
    for (const target of [fixture.foreign, fixture.unprovisioned, { id: '11111111-1111-1111-1111-111111111111' }]) {
      const refused = await post(page, '/api/v1/me/membership-requests', { destinationCompanyId: target.id });
      expect(refused.status, `requesting a company outside the boundary (${target.id})`).toBe(404);
    }
    expect(await readRequests(fixture, { userId: requester.id, companyId: destination.id })).toHaveLength(0);

    // ---------------------------------------------- 3. the request is submitted from the real UI
    await page.goto('/app/account/membership-requests');
    await expect(page.getByRole('heading', { level: 1, name: 'درخواست‌های عضویت من' })).toBeVisible();
    await page.getByLabel('نام دقیق شرکت مقصد').fill(destination.name);
    const searchResponse = page.waitForResponse(
      (response) => response.url().includes('/membership-requests/destinations?') && response.request().method() === 'GET',
    );
    await page.getByRole('button', { name: 'جستجوی شرکت' }).click();
    expect((await searchResponse).status()).toBe(200);
    const destinationRow = page.getByRole('listitem').filter({ hasText: destination.name });
    await expect(destinationRow.getByRole('button', { name: `درخواست عضویت در ${destination.name}` })).toBeVisible();
    const createResponse = page.waitForResponse(
      (response) => response.url().endsWith('/api/v1/me/membership-requests') && response.request().method() === 'POST',
    );
    await destinationRow.getByRole('button', { name: `درخواست عضویت در ${destination.name}` }).click();
    const created = await createResponse;
    expect(created.status()).toBe(201);
    await expect(page.getByText('درخواست عضویت ثبت شد و در انتظار تأیید است.')).toBeVisible();

    // The UI intentionally consumes only the success status. Read the durable request id from SQL
    // rather than making the browser test depend on re-reading a response body already consumed
    // by the application's fetch implementation.
    const [createdRow] = await readRequests(fixture, { userId: requester.id, companyId: destination.id });
    expect(createdRow).toMatchObject({
      Status: 'Pending', ApprovedRoleId: null, DecidedByUserId: null,
    });
    const requestId = createdRow.Id;
    const myRequestsTable = page.getByRole('table', { name: 'فهرست درخواست‌های عضویت من' });
    await expect(myRequestsTable.getByText(destination.name)).toBeVisible();
    await expect(myRequestsTable.getByText('در انتظار تأیید')).toBeVisible();
    await watch.assertClean('requester: my membership requests');

    // ---------------------------------------------- 4. Pending — database and API agree
    const [pendingRow] = await readRequests(fixture, { userId: requester.id, companyId: destination.id });
    expect(pendingRow).toMatchObject({
      Id: requestId, Status: 'Pending', ApprovedRoleId: null, DecidedByUserId: null,
    });
    const myRequests = await callApi(page, 'GET', '/api/v1/me/membership-requests?status=Pending');
    expect(myRequests.status).toBe(200);
    expect(myRequests.body.items).toHaveLength(1);
    expect(myRequests.body.items[0]).toMatchObject({
      id: requestId,
      destinationCompanyId: destination.id,
      destinationCompanyName: destination.name,
      status: 'Pending',
      approvedRoleCode: null,
      approvedRoleName: null,
    });

    // A pending request is not access, and it cannot be duplicated.
    expect(await readMembership(fixture, { userId: requester.id, companyId: destination.id })).toBeUndefined();
    expect((await companyOverview(page, destination.id)).status).toBe(404);
    expect((await post(page, '/api/v1/me/membership-requests', { destinationCompanyId: destination.id })).status).toBe(409);
    // Without the anti-forgery request token the POST is refused before anything is written.
    expect((await callApi(page, 'POST', '/api/v1/me/membership-requests', {
      body: { destinationCompanyId: destination.id },
    })).status).toBe(400);
    expect(await readRequests(fixture, { userId: requester.id, companyId: destination.id })).toHaveLength(1);

    // ------------------------------------------------------------- 5. the approver signs in
    const approverContext = await openContext(browser, page);
    try {
      const approverPage = await approverContext.newPage();
      const approverWatch = await watchPage(approverPage);
      expect(await enterWorkspace(approverPage, approver)).toBe('dashboard');
      await expect(approverPage.getByRole('heading', { name: DASHBOARD_HEADING })).toBeVisible();

      // The permission is real, and it is scoped to the destination company.
      const permissions = await callApi(approverPage, 'GET', '/api/v1/me/permissions');
      expect(permissions.status).toBe(200);
      expect(permissions.body.isInstallationAdministrator).toBe(false);
      expect(permissions.body.company).toMatchObject({
        id: destination.id,
        permissionCodes: expect.arrayContaining(['users.membership.approve', 'users.list']),
      });
      // The same account holds no approval permission in the source company.
      expect((await callApi(approverPage, 'GET', pendingPath(source.id))).status).toBe(404);

      // The pending queue: minimal allowlist fields only, no contact data of the requester.
      const pending = await callApi(approverPage, 'GET', `${pendingPath(destination.id)}?page=1&pageSize=25`);
      expect(pending.status).toBe(200);
      expect(pending.body.items).toHaveLength(1);
      expect(pending.body.items[0]).toMatchObject({
        id: requestId,
        userId: requester.id,
        requesterDisplayName: requester.displayName,
        username: requester.username,
        isRequesterActive: true,
      });
      expect(JSON.stringify(pending.body)).not.toContain('@e2e.test');

      // Only the active, non-system role inside the approver's delegation ceiling is offered.
      const roles = await callApi(approverPage, 'GET', `${assignableRolesPath(destination.id)}?page=1&pageSize=100`);
      expect(roles.status).toBe(200);
      expect(roles.body.items).toEqual([
        { id: destination.memberRole.id, code: destination.memberRole.code, name: 'عضو مقصد', isSystemRole: false },
      ]);

      // The server refuses the two roles the list hides, before anything is written.
      for (const [label, role] of [['above the ceiling', destination.restrictedRole], ['a system role', destination.systemRole]]) {
        const refused = await post(approverPage, approvePath(destination.id, requestId), roleBody(role.id));
        expect(refused.status, `approving with ${label}`).toBe(403);
      }
      expect((await readRequests(fixture, { userId: requester.id, companyId: destination.id }))[0].Status).toBe('Pending');
      expect(await readMembership(fixture, { userId: requester.id, companyId: destination.id })).toBeUndefined();

      // ------------------------------------------ 6. approval through the queue in the real UI
      await approverPage.goto('/app/admin/membership-requests');
      await expect(approverPage.getByRole('heading', { level: 1, name: 'درخواست‌های عضویت در انتظار تأیید' })).toBeVisible();
      const queue = approverPage.getByRole('table', { name: 'فهرست درخواست‌های عضویت در انتظار تأیید' });
      await expect(queue.getByText(requester.username)).toBeVisible();
      await approverPage.getByRole('button', { name: `تأیید درخواست ${requester.label}` }).click();
      const dialog = approverPage.getByRole('dialog', { name: 'تأیید درخواست عضویت' });
      await expect(dialog).toBeVisible();
      const roleSelect = dialog.getByLabel('گروه دسترسی');
      await expect(roleSelect).toBeEnabled();
      const offered = await roleSelect.locator('option').evaluateAll((options) =>
        options.filter((option) => option.value).map((option) => ({ value: option.value, text: (option.textContent ?? '').trim() })));
      expect(offered).toEqual([{ value: destination.memberRole.id, text: 'عضو مقصد' }]);

      await roleSelect.selectOption(destination.memberRole.id);
      const approveResponse = approverPage.waitForResponse(
        (response) => response.url().endsWith('/approve') && response.request().method() === 'POST',
      );
      await dialog.getByRole('button', { name: ACCEPT_BUTTON }).click();
      expect((await approveResponse).status()).toBe(204);
      await expect(approverPage.getByText(`درخواست «${requester.displayName}» تأیید شد.`)).toBeVisible();
      await expect(queue.getByText(requester.username)).toHaveCount(0);
      await approverWatch.assertClean('approver: pending membership requests');

      // -------------------------------------------- 7. state after the approval (DB + API)
      const [approvedRow] = await readRequests(fixture, { userId: requester.id, companyId: destination.id });
      expect(approvedRow).toMatchObject({
        Id: requestId,
        Status: 'Approved',
        ApprovedRoleId: destination.memberRole.id,
        DecidedByUserId: approver.id,
      });
      expect(approvedRow.DecidedAtUtc).toBeInstanceOf(Date);
      const membership = await readMembership(fixture, { userId: requester.id, companyId: destination.id });
      expect(membership).toBeTruthy();
      expect(membership.IsActive).toBe(true);
      expect(membership.RoleId).toBe(destination.memberRole.id);
      const audit = await readAudit(fixture, { companyId: destination.id, eventType: 'CompanyMembershipApproved' });
      expect(audit).toHaveLength(1);
      expect(audit[0]).toMatchObject({ UserId: approver.id, EntityType: 'CompanyMembershipRequest', EntityId: requestId });
      const history = await callApi(
        approverPage, 'GET', `/api/v1/admin/companies/${destination.id}/membership-requests?status=Approved`,
      );
      expect(history.status).toBe(200);
      expect(history.body.items[0]).toMatchObject({
        id: requestId, username: requester.username, status: 'Approved', approvedRoleName: 'عضو مقصد',
      });
      // A decided request cannot be decided a second time.
      expect((await post(approverPage, approvePath(destination.id, requestId), roleBody(destination.memberRole.id))).status).toBe(404);

      // ------------------------------- 8. the requester's access, in the same browser session
      const mine = await callApi(page, 'GET', '/api/v1/me/membership-requests?status=Approved');
      expect(mine.status).toBe(200);
      expect(mine.body.items[0]).toMatchObject({ id: requestId, status: 'Approved', approvedRoleName: 'عضو مقصد' });
      await page.goto('/app/account/membership-requests');
      await expect(myRequestsTable.getByText('تأییدشده')).toBeVisible();
      await expect(myRequestsTable.getByText('عضو مقصد')).toBeVisible();
      await watch.assertClean('requester: approved request');
      expect(await accessibleCompanyIds(page)).toEqual(expect.arrayContaining([source.id, destination.id]));
      const overview = await companyOverview(page, destination.id);
      expect(overview.status).toBe(200);
      expect(overview.body.company).toMatchObject({ id: destination.id, name: destination.name, roleName: 'عضو مقصد' });
      // The access is the role's real permission, not a flag: `users.list` now works there.
      const directory = await callApi(page, 'GET', `/api/v1/companies/${destination.id}/users?page=1&pageSize=10`);
      expect(directory.status).toBe(200);
      expect(directory.body.items.some((item) => item.username === requester.username)).toBe(true);
      // The destination company is now offered for selection with the approved role as well.
      const context = await callApi(page, 'GET', '/api/v1/context');
      expect(context.status).toBe(200);
      expect(context.body.companies.map((company) => company.id)).toEqual(expect.arrayContaining([source.id, destination.id]));
      expect(context.body.companies.find((company) => company.id === destination.id).roleName).toBe('عضو مقصد');
    } finally {
      await approverContext.close();
    }
  });

  test('a rejected request creates no membership and no access, and cannot be decided by the requester', async ({ page, browser }) => {
    test.setTimeout(240_000);
    const watch = await watchPage(page);
    const { source, destination, approver } = fixture;
    const requester = await fixture.addUser('requester');
    await fixture.addMembership(requester, source, source.memberRole.id);

    await enterWorkspace(page, requester);
    const created = await post(page, '/api/v1/me/membership-requests', { destinationCompanyId: destination.id });
    expect(created.status).toBe(201);
    const requestId = created.body.id;

    const approverContext = await openContext(browser, page);
    try {
      const approverPage = await approverContext.newPage();
      const approverWatch = await watchPage(approverPage);
      await enterWorkspace(approverPage, approver);

      // The request is visible to the authorized approver of the destination company…
      const pending = await callApi(approverPage, 'GET', pendingPath(destination.id));
      expect(pending.status).toBe(200);
      expect(pending.body.items.map((item) => item.id)).toEqual([requestId]);

      // …and the requester cannot decide it, because it has no permission in the destination.
      expect((await post(page, rejectPath(destination.id, requestId), { reason: 'خودتأیید' })).status).toBe(404);
      expect((await post(page, approvePath(destination.id, requestId), roleBody(destination.memberRole.id))).status).toBe(404);
      expect((await readRequests(fixture, { userId: requester.id, companyId: destination.id }))[0].Status).toBe('Pending');

      // ------------------------------------------------ rejection through the real UI
      await approverPage.goto('/app/admin/membership-requests');
      await approverPage.getByRole('button', { name: `رد درخواست ${requester.label}` }).click();
      const dialog = approverPage.getByRole('alertdialog', { name: 'رد درخواست عضویت' });
      await expect(dialog).toBeVisible();
      await dialog.getByLabel(/توضیح رد درخواست/).fill('مدارک ناقص است.');
      const rejectResponse = approverPage.waitForResponse(
        (response) => response.url().endsWith('/reject') && response.request().method() === 'POST',
      );
      await dialog.getByRole('button', { name: 'رد درخواست' }).click();
      expect((await rejectResponse).status()).toBe(204);
      await expect(approverPage.getByText(`درخواست «${requester.displayName}» رد شد.`)).toBeVisible();
      await approverWatch.assertClean('approver: rejected membership request');

      // ------------------------------------------------ Rejected in database and API
      const [rejected] = await readRequests(fixture, { userId: requester.id, companyId: destination.id });
      expect(rejected).toMatchObject({
        Id: requestId,
        Status: 'Rejected',
        ApprovedRoleId: null,
        DecidedByUserId: approver.id,
        DecisionReason: 'مدارک ناقص است.',
      });
      expect(rejected.DecidedAtUtc).toBeInstanceOf(Date);
      expect(await readMembership(fixture, { userId: requester.id, companyId: destination.id })).toBeUndefined();
      const audit = await readAudit(fixture, { companyId: destination.id, eventType: 'CompanyMembershipRejected' });
      expect(audit).toHaveLength(1);
      expect(audit[0]).toMatchObject({ UserId: approver.id, EntityId: requestId });

      const mine = await callApi(page, 'GET', '/api/v1/me/membership-requests?status=Rejected');
      expect(mine.status).toBe(200);
      expect(mine.body.items[0]).toMatchObject({
        id: requestId, status: 'Rejected', decisionReason: 'مدارک ناقص است.', approvedRoleCode: null,
      });
      await page.goto('/app/account/membership-requests');
      const myRequestsTable = page.getByRole('table', { name: 'فهرست درخواست‌های عضویت من' });
      await expect(myRequestsTable.getByText('ردشده')).toBeVisible();
      await expect(myRequestsTable.getByText('مدارک ناقص است.')).toBeVisible();
      await watch.assertClean('requester: rejected request');

      // ------------------------------------------------ the rejected path never becomes access
      expect(await accessibleCompanyIds(page)).toEqual([source.id]);
      expect((await companyOverview(page, destination.id)).status).toBe(404);
      expect((await callApi(page, 'GET', `/api/v1/companies/${destination.id}/users?page=1&pageSize=10`)).status).toBe(404);
      // No pending request is left behind by the rejection.
      expect((await callApi(page, 'GET', '/api/v1/me/membership-requests?status=Pending')).body.totalCount).toBe(0);
      // Repeating a *rejected* request is allowed by design; the new one is pending and still not access.
      const repeated = await post(page, '/api/v1/me/membership-requests', { destinationCompanyId: destination.id });
      expect(repeated.status).toBe(201);
      expect((await readRequests(fixture, { userId: requester.id, companyId: destination.id }))
        .map((row) => row.Status).sort()).toEqual(['Pending', 'Rejected']);
      expect((await companyOverview(page, destination.id)).status).toBe(404);
    } finally {
      await approverContext.close();
    }
  });

  test('self-approval, a foreign approver and a permission revoked before the decision all fail closed', async ({ page, browser }) => {
    test.setTimeout(240_000);
    const { source, destination, approver } = fixture;
    const requester = await fixture.addUser('requester');
    await fixture.addMembership(requester, source, source.memberRole.id);
    await enterWorkspace(page, requester);

    const created = await post(page, '/api/v1/me/membership-requests', { destinationCompanyId: destination.id });
    expect(created.status).toBe(201);
    const requestId = created.body.id;
    const pendingStatus = async () =>
      (await readRequests(fixture, { userId: requester.id, companyId: destination.id }))[0].Status;
    const noMembership = async () =>
      expect(await readMembership(fixture, { userId: requester.id, companyId: destination.id })).toBeUndefined();

    const approverContext = await openContext(browser, page);
    try {
      const approverPage = await approverContext.newPage();
      await enterWorkspace(approverPage, approver);

      // (a) The company id in the route is the scope: an approver of the source company cannot
      // decide a request of the destination company, even though it holds the same permission.
      expect((await post(approverPage, approvePath(source.id, requestId), roleBody(source.memberRole.id))).status).toBe(404);
      expect(await pendingStatus()).toBe('Pending');

      // (b) Self-approval is refused even when the requester really holds the approval permission
      // in the destination company (D-065); the temporary grant is removed again straight away.
      await fixture.addMembership(requester, destination, destination.approverRole.id);
      try {
        const membershipBefore = await readMembership(fixture, { userId: requester.id, companyId: destination.id });
        expect(membershipBefore).toMatchObject({
          IsActive: true, RoleId: destination.approverRole.id,
        });
        expect((await post(page, approvePath(destination.id, requestId), roleBody(destination.memberRole.id))).status).toBe(403);
        expect(await pendingStatus()).toBe('Pending');
        expect(await readMembership(fixture, { userId: requester.id, companyId: destination.id }))
          .toEqual(membershipBefore);
      } finally {
        await fixture.removeMembership(requester, destination);
      }

      // (c) Revoking the approver's membership makes the very same call fail, and the queue too.
      await fixture.setMembershipActive(approver, destination, false);
      try {
        expect((await post(approverPage, approvePath(destination.id, requestId), roleBody(destination.memberRole.id))).status).toBe(404);
        expect((await callApi(approverPage, 'GET', pendingPath(destination.id))).status).toBe(404);
        expect(await pendingStatus()).toBe('Pending');
        await noMembership();
      } finally {
        await fixture.setMembershipActive(approver, destination, true);
      }

      // (d) The delegation ceiling is recalculated per call: without `users.list` the previously
      // delegable destination role is out of scope, so the approval is refused and writes nothing.
      await fixture.setRolePermissions(destination.approverRole.id, [fixture.permissions.approve]);
      try {
        expect((await post(approverPage, approvePath(destination.id, requestId), roleBody(destination.memberRole.id))).status).toBe(403);
        expect(await pendingStatus()).toBe('Pending');
        await noMembership();
        // The list endpoint agrees: nothing is assignable any more.
        expect((await callApi(approverPage, 'GET', assignableRolesPath(destination.id))).body.totalCount).toBe(0);
      } finally {
        await fixture.setRolePermissions(
          destination.approverRole.id, [fixture.permissions.approve, fixture.permissions.listUsers],
        );
      }
      expect((await readRolePermissions(fixture, destination.approverRole.id)).sort())
        .toEqual([fixture.permissions.approve, fixture.permissions.listUsers].sort());

      // (e) An anti-forgery token issued to a *different* session is not accepted here.
      const foreignToken = await antiforgery(page, { refresh: true });
      const crossSession = await callApi(approverPage, 'POST', approvePath(destination.id, requestId), {
        body: roleBody(destination.memberRole.id),
        token: foreignToken,
      });
      expect(crossSession.status, 'an anti-forgery token of another session').toBe(400);
      expect(await pendingStatus()).toBe('Pending');

      // (f) With every check satisfied the approval succeeds and grants exactly that role.
      expect((await post(approverPage, approvePath(destination.id, requestId), roleBody(destination.memberRole.id))).status).toBe(204);
      expect((await readRequests(fixture, { userId: requester.id, companyId: destination.id }))[0]).toMatchObject({
        Status: 'Approved', ApprovedRoleId: destination.memberRole.id, DecidedByUserId: approver.id,
      });
      expect(await readMembership(fixture, { userId: requester.id, companyId: destination.id }))
        .toMatchObject({ IsActive: true, RoleId: destination.memberRole.id });
      // The requester now has real access to the destination company.
      expect((await companyOverview(page, destination.id)).status).toBe(200);
    } finally {
      await approverContext.close();
    }
  });

  test('an active member cannot request the same company again while the approver queue stays empty', async ({ page, browser }) => {
    const { destination, approver } = fixture;
    const member = await fixture.addUser('member');
    await fixture.addMembership(member, destination, destination.memberRole.id);

    await enterWorkspace(page, member);
    const duplicate = await post(page, '/api/v1/me/membership-requests', { destinationCompanyId: destination.id });
    expect(duplicate.status).toBe(409);
    expect(await readRequests(fixture, { userId: member.id, companyId: destination.id })).toHaveLength(0);
    // The active member is also not offered the destination as a requestable company.
    const discovered = await callApi(
      page, 'GET', `/api/v1/me/membership-requests/destinations?search=${encodeURIComponent(destination.name)}`,
    );
    expect(discovered.body.totalCount).toBe(0);

    // The approver of that company sees no request for that account.
    const approverContext = await openContext(browser, page);
    try {
      const approverPage = await approverContext.newPage();
      await enterWorkspace(approverPage, approver);
      const pending = await callApi(approverPage, 'GET', pendingPath(destination.id));
      expect(pending.status).toBe(200);
      expect(pending.body.totalCount).toBe(0);
    } finally {
      await approverContext.close();
    }
  });
});

test('the membership endpoints keep their per-user sensitive rate limit', async ({ page }) => {
  const fixture = await createMembershipFixture('محدود');
  try {
    // This account belongs to no company, so the shell settles on the "no company" blocker; the API
    // endpoints used below do not need a work context.
    expect(await enterWorkspace(page, fixture.rateLimited)).toBe('blocked');
    // The sensitive window allows 30 requests per minute per user; the 429 must still appear.
    const statuses = await page.evaluate(async () => {
      const results = [];
      for (let index = 0; index < 40; index += 1) {
        const response = await fetch('/api/v1/me/membership-requests/destinations?search=aaaaaa', {
          credentials: 'same-origin',
        });
        results.push(response.status);
      }
      return results;
    });
    // The window is fixed: once the limit is reached every later call in the same window is 429,
    // and the limit is never reached before the configured 30 permits of the partition.
    const firstLimited = statuses.indexOf(429);
    expect(firstLimited, 'the sensitive limit must eventually reject').toBeGreaterThan(-1);
    expect(firstLimited).toBeGreaterThanOrEqual(20);
    expect(statuses.slice(firstLimited).every((status) => status === 429)).toBe(true);
    // A non-sensitive membership endpoint keeps working for the same user.
    expect((await callApi(page, 'GET', '/api/v1/me/membership-requests')).status).toBe(200);
  } finally {
    await fixture.destroy();
  }
});
