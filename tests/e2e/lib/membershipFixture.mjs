// Test-only fixtures for the positive membership-workflow E2E (#444 / #510 / #511 / D-065).
//
// The published application has no supported way to create a *customer boundary*, a company, a
// company role or a permission grant (docs/DECISIONS.md D-020/D-033 and the note in
// docs/LICENSING_OPEN_DECISIONS.md). The existing E2E seed therefore writes those rows straight
// into the disposable `FaranouE2E` database. This module does the same, but builds one *isolated*
// graph per test instead of extending the shared production-shaped seed:
//
//   * its own opaque CustomerBoundary with two active companies inside it, one company in another
//     boundary, and one company without any boundary (fail-closed case),
//   * company roles backed by real permission catalog rows (`users.membership.approve`,
//     `users.list`, plus `corporate-groups.view` as a permission outside the delegation ceiling),
//   * users with a real ASP.NET Core Identity V3 password hash, so every sign-in is a real sign-in.
//
// Nothing is mocked and no application code, migration or production seed changes. `destroy()`
// removes every row of the fixture and must run in a `finally` block, so a failed assertion never
// leaves data behind.
import { pbkdf2Sync, randomBytes, randomUUID } from 'node:crypto';
import sql from 'mssql';

/** Refuses to run against anything but the disposable E2E database of the CI job. */
export function requireConnectionString() {
  const value = process.env.FARANOU_E2E_SQL;
  if (!value || !/Database=FaranouE2E/i.test(value)) {
    throw new Error('FARANOU_E2E_SQL must name the disposable E2E database (its name must start with FaranouE2E).');
  }
  return value;
}

/** ASP.NET Core Identity PasswordHasher V3: PBKDF2-HMAC-SHA512, 100,000 iterations, 128-bit salt. */
export function hashPasswordV3(password, iterations = 100_000) {
  const salt = randomBytes(16);
  const subkey = pbkdf2Sync(password, salt, iterations, 32, 'sha512');
  const header = Buffer.alloc(13);
  header.writeUInt8(0x01, 0);
  header.writeUInt32BE(2, 1); // KeyDerivationPrf.HMACSHA512
  header.writeUInt32BE(iterations, 5);
  header.writeUInt32BE(salt.length, 9);
  return Buffer.concat([header, salt, subkey]).toString('base64');
}

export const newPassword = () => `E2e-${randomBytes(12).toString('hex')}`;

/** Persian full names of the fixture users, keyed by the ASCII label used in the username. */
const DISPLAY_NAMES = {
  approver: 'مریم تأییدگر',
  srcapprover: 'سعید تأییدگر مبدأ',
  ratelimited: 'کاربر محدود',
  requester: 'رضا درخواستگر',
  member: 'نگار عضو',
};

/** The single open fiscal year of the fixture (dates are Gregorian calendar dates). */
export const FISCAL_YEAR = { title: 'سال مالی ۱۴۰۵', start: '2026-03-21', end: '2027-03-20' };

/**
 * Builds the isolated membership fixture. `label` is embedded in the company names so a failure
 * message makes clear which test produced the data.
 */
export async function createMembershipFixture(label) {
  const pool = await new sql.ConnectionPool(requireConnectionString()).connect();
  const suffix = randomBytes(4).toString('hex').toUpperCase();
  const now = new Date();
  const created = { users: [], companies: [], roles: [], periods: [] };

  async function run(text, parameters = {}) {
    const request = pool.request();
    for (const [name, [type, value]] of Object.entries(parameters)) request.input(name, type, value);
    return request.query(text);
  }

  async function scalar(text, parameters = {}) {
    const result = await run(text, parameters);
    const row = result.recordset?.[0];
    return row ? Object.values(row)[0] : undefined;
  }

  async function permissionId(code) {
    const id = await scalar('SELECT Id FROM PermissionDefinitions WHERE Code = @code', {
      code: [sql.VarChar(120), code],
    });
    if (!id) throw new Error(`The migrated E2E database has no '${code}' permission in the catalog.`);
    // SQL Server prints uniqueidentifier values in upper case; the tests compare them against the
    // lower-case GUIDs the fixture generated, so everything is normalised here.
    return String(id).toLowerCase();
  }

  async function addCompany(name, customerBoundaryId, { active = true } = {}) {
    const id = randomUUID();
    await run(
      `INSERT INTO Companies (Id, Ordering, InsertDateTime, Name, NationalId, IsActive, CustomerBoundaryId)
       VALUES (@id, 10000, @now, @name, NULL, @active, @boundary)`,
      {
        id: [sql.UniqueIdentifier, id],
        now: [sql.DateTime2, now],
        name: [sql.NVarChar(200), name],
        active: [sql.Bit, active],
        boundary: [sql.UniqueIdentifier, customerBoundaryId ?? null],
      },
    );
    created.companies.push(id);
    return { id, name, active };
  }

  async function addRole(company, code, name, permissionIds, { system = false, active = true } = {}) {
    const id = randomUUID();
    await run(
      `INSERT INTO CompanyRoles (Id, Ordering, InsertDateTime, TenantId, Code, Name, IsSystemRole, IsActive)
       VALUES (@id, 10000, @now, @tenant, @code, @name, @system, @active)`,
      {
        id: [sql.UniqueIdentifier, id],
        now: [sql.DateTime2, now],
        tenant: [sql.UniqueIdentifier, company.id],
        code: [sql.VarChar(80), code],
        name: [sql.NVarChar(120), name],
        system: [sql.Bit, system],
        active: [sql.Bit, active],
      },
    );
    created.roles.push(id);
    await setRolePermissions(id, permissionIds);
    return { id, code, name, system, active, permissions: [...permissionIds] };
  }

  async function setRolePermissions(roleId, permissionIds) {
    await run('DELETE FROM CompanyRolePermissions WHERE RoleId = @role', {
      role: [sql.UniqueIdentifier, roleId],
    });
    for (const permission of permissionIds) {
      await run(
        'INSERT INTO CompanyRolePermissions (RoleId, PermissionId) VALUES (@role, @permission)',
        { role: [sql.UniqueIdentifier, roleId], permission: [sql.UniqueIdentifier, permission] },
      );
    }
  }

  /**
   * `asciiLabel` becomes part of the (unique, varchar(20), ASCII) username; `displayName` is the
   * Persian full name the UI and the API show for the user.
   */
  async function addUser(asciiLabel, { active = true } = {}) {
    const id = randomUUID();
    // `Users.Username` is varchar(20) and unique, so the label and the run suffix share the budget.
    const username = `e2e${asciiLabel}${suffix}`.slice(0, 20).toLowerCase();
    const displayName = DISPLAY_NAMES[asciiLabel] ?? asciiLabel;
    const password = newPassword();
    await run(
      `INSERT INTO Users (Id, RoleId, Ordering, InsertDateTime, UpdateDateTime, IsActive, IsSystemic, IsProgrammer, IsUndeletable,
         IsProfilePublic, IsVisibleInContactUsPage, IsEmailAddressVerified, IsCellPhoneNumberVerified,
         Username, Password, FirstName, LastName, EmailAddress, EmailAddressVerificationKey)
       VALUES (@id, NULL, 10000, @now, @now, @active, 0, 0, 0, 0, 0, 1, 0, @username, @password, @first, @last, @email, @key)`,
      {
        id: [sql.UniqueIdentifier, id],
        now: [sql.DateTime2, now],
        active: [sql.Bit, active],
        username: [sql.VarChar(20), username],
        password: [sql.VarChar(255), hashPasswordV3(password)],
        first: [sql.NVarChar(50), displayName],
        // The last name is null on purpose: the API builds "FirstName LastName", so the display
        // name the requester list shows is exactly `displayName`.
        last: [sql.NVarChar(50), null],
        email: [sql.VarChar(254), `${username}@e2e.test`],
        key: [sql.UniqueIdentifier, randomUUID()],
      },
    );
    created.users.push(id);
    return {
      id,
      username,
      password,
      displayName,
      /** Exactly what the pending-request table renders and its buttons are labelled with. */
      label: `${displayName} (${username})`,
    };
  }

  async function addMembership(user, company, roleId, { active = true } = {}) {
    const id = randomUUID();
    await run(
      `INSERT INTO UserCompanyMemberships (Id, Ordering, InsertDateTime, UserId, TenantId, RoleId, IsActive, CreatedAtUtc)
       VALUES (@id, 10000, @now, @user, @tenant, @role, @active, @now)`,
      {
        id: [sql.UniqueIdentifier, id],
        now: [sql.DateTime2, now],
        user: [sql.UniqueIdentifier, user.id],
        tenant: [sql.UniqueIdentifier, company.id],
        role: [sql.UniqueIdentifier, roleId],
        active: [sql.Bit, active],
      },
    );
    return id;
  }

  /** Adds one open fiscal year to a company (dates are unique per company in the schema). */
  async function addPeriod(company, { title = FISCAL_YEAR.title, status = 1 } = {}) {
    const id = randomUUID();
    await run(
      `INSERT INTO FiscalPeriods (Id, Ordering, InsertDateTime, TenantId, Title, StartDate, EndDate, Status)
       VALUES (@id, 10000, @now, @tenant, @title, @start, @end, @status)`,
      {
        id: [sql.UniqueIdentifier, id],
        now: [sql.DateTime2, now],
        tenant: [sql.UniqueIdentifier, company.id],
        title: [sql.NVarChar(100), title],
        start: [sql.Date, FISCAL_YEAR.start],
        end: [sql.Date, FISCAL_YEAR.end],
        status: [sql.Int, status],
      },
    );
    created.periods.push(id);
    return id;
  }

  /** Flips the active flag of one membership (used to prove permission re-checks). */
  async function setMembershipActive(user, company, active) {
    await run(
      'UPDATE UserCompanyMemberships SET IsActive = @active WHERE UserId = @user AND TenantId = @company',
      {
        active: [sql.Bit, active],
        user: [sql.UniqueIdentifier, user.id],
        company: [sql.UniqueIdentifier, company.id],
      },
    );
  }

  /** Removes one membership row completely (lease of a temporary grant used inside a test). */
  async function removeMembership(user, company) {
    await run(
      'DELETE FROM UserCompanyMemberships WHERE UserId = @user AND TenantId = @company',
      {
        user: [sql.UniqueIdentifier, user.id],
        company: [sql.UniqueIdentifier, company.id],
      },
    );
  }

  /** Deactivates a company role; the approval path must then refuse to use it. */
  async function setRoleActive(role, active) {
    await run(
      'UPDATE CompanyRoles SET IsActive = @active WHERE Id = @role',
      { active: [sql.Bit, active], role: [sql.UniqueIdentifier, role.id] },
    );
  }

  /**
   * Deletes every row this fixture (plus the requests, memberships and audit rows a workflow run
   * added against it) in foreign-key order. Safe to call once; closes the pool.
   */
  async function destroy() {
    const idList = (values, prefix) => ({
      clause: values.map((_, index) => `@${prefix}${index}`).join(', '),
      parameters: Object.fromEntries(values.map((value, index) => [
        `${prefix}${index}`, [sql.UniqueIdentifier, value],
      ])),
    });
    const users = idList(created.users, 'fu');
    const companies = idList(created.companies, 'fc');
    const roles = idList(created.roles, 'fr');
    const periods = idList(created.periods, 'fp');
    const parameters = {
      ...users.parameters, ...companies.parameters, ...roles.parameters, ...periods.parameters,
    };

    const actor = [users.clause && `UserId IN (${users.clause})`, companies.clause && `TenantId IN (${companies.clause})`]
      .filter(Boolean).join(' OR ');
    const membership = [users.clause && `UserId IN (${users.clause})`, companies.clause && `TenantId IN (${companies.clause})`]
      .filter(Boolean).join(' OR ');

    const statements = [
      users.clause || companies.clause
        ? `DELETE FROM CompanyMembershipRequests WHERE ${[
          users.clause && `UserId IN (${users.clause})`,
          companies.clause && `DestinationTenantId IN (${companies.clause})`,
        ].filter(Boolean).join(' OR ')}`
        : null,
      actor ? `DELETE FROM SecurityAuditEvents WHERE ${actor}` : null,
      roles.clause ? `DELETE FROM CompanyRolePermissions WHERE RoleId IN (${roles.clause})` : null,
      membership ? `DELETE FROM UserCompanyMemberships WHERE ${membership}` : null,
      users.clause ? `DELETE FROM LoginLogs WHERE UserId IN (${users.clause})` : null,
      users.clause ? `DELETE FROM Users WHERE Id IN (${users.clause})` : null,
      roles.clause ? `DELETE FROM CompanyRoles WHERE Id IN (${roles.clause})` : null,
      periods.clause ? `DELETE FROM FiscalPeriods WHERE Id IN (${periods.clause})` : null,
      companies.clause ? `DELETE FROM Companies WHERE Id IN (${companies.clause})` : null,
    ].filter(Boolean);

    try {
      for (const statement of statements) await run(statement, parameters);
    } finally {
      await pool.close();
    }
  }

  // -----------------------------------------------------------------------------------------------
  // The fixture graph: one boundary, two guest companies, one foreign and one unprovisioned company.
  // -----------------------------------------------------------------------------------------------
  const permissions = {
    approve: await permissionId('users.membership.approve'),
    listUsers: await permissionId('users.list'),
    outsideCeiling: await permissionId('corporate-groups.view'),
  };

  const boundaryId = randomUUID();
  const foreignBoundaryId = randomUUID();

  const source = await addCompany(`شرکت مبدأ آزمون ${label} ${suffix}`, boundaryId);
  const destination = await addCompany(`شرکت مقصد آزمون ${label} ${suffix}`, boundaryId);
  const foreign = await addCompany(`شرکت بیرون از مرز ${label} ${suffix}`, foreignBoundaryId);
  const unprovisioned = await addCompany(`شرکت بدون مرز ${label} ${suffix}`, null);
  // A foreign company whose *name* equals the destination name proves the filter is not name-based.
  const impostor = await addCompany(destination.name, foreignBoundaryId);

  // Source company roles: a plain membership role and a role that may approve requests there.
  source.memberRole = await addRole(source, `SRC${suffix}`, 'عضو مبدأ', []);
  source.approverRole = await addRole(
    source, `SRCAPP${suffix}`, 'تأییدکننده مبدأ', [permissions.approve],
  );

  // Destination company roles: approver, one role inside the ceiling, one above it, one system role.
  destination.approverRole = await addRole(
    destination, `APPR${suffix}`, 'تأییدکننده مقصد', [permissions.approve, permissions.listUsers],
  );
  destination.memberRole = await addRole(
    destination, `MEMB${suffix}`, 'عضو مقصد', [permissions.listUsers],
  );
  destination.restrictedRole = await addRole(
    destination, `REST${suffix}`, 'بالاتر از سقف تفویض', [permissions.outsideCeiling],
  );
  destination.systemRole = await addRole(
    destination, `SYST${suffix}`, 'گروه سیستمی مقصد', [permissions.listUsers], { system: true },
  );

  // Every company of the fixture that must be enterable gets exactly one open fiscal year.
  source.periodId = await addPeriod(source);
  destination.periodId = await addPeriod(destination);

  // The destination approver: an active member of the destination company with the real permission.
  const approver = await addUser('approver');
  await addMembership(approver, destination, destination.approverRole.id);

  // An approver of the *source* company only: proves the permission is company-scoped.
  const sourceApprover = await addUser('srcapprover');
  await addMembership(sourceApprover, source, source.approverRole.id);

  // Signs in only to exercise the sensitive rate limit of its own partition; no membership.
  const rateLimited = await addUser('ratelimited');

  return {
    label,
    suffix,
    pool,
    boundaryId,
    foreignBoundaryId,
    source,
    destination,
    foreign,
    unprovisioned,
    impostor,
    approver,
    sourceApprover,
    rateLimited,
    permissions,
    created,
    run,
    scalar,
    setRolePermissions,
    setMembershipActive,
    removeMembership,
    setRoleActive,
    addCompany,
    addRole,
    addUser,
    addMembership,
    addPeriod,
    destroy,
  };
}

/** A pending/decided membership request as stored (used for the database half of the assertions). */
export async function readRequests(fixture, { userId, companyId }) {
  const result = await fixture.run(
    `SELECT LOWER(CONVERT(varchar(36), Id)) AS Id,
            LOWER(CONVERT(varchar(36), UserId)) AS UserId,
            LOWER(CONVERT(varchar(36), DestinationTenantId)) AS DestinationTenantId,
            Status, RequestedAtUtc,
            LOWER(CONVERT(varchar(36), ApprovedRoleId)) AS ApprovedRoleId,
            LOWER(CONVERT(varchar(36), DecidedByUserId)) AS DecidedByUserId,
            DecidedAtUtc, DecisionReason
     FROM CompanyMembershipRequests
     WHERE UserId = @user AND DestinationTenantId = @company
     ORDER BY RequestedAtUtc DESC`,
    {
      user: [sql.UniqueIdentifier, userId],
      company: [sql.UniqueIdentifier, companyId],
    },
  );
  return result.recordset;
}

/** The membership row of one user in one company, or undefined. */
export async function readMembership(fixture, { userId, companyId }) {
  const result = await fixture.run(
    `SELECT LOWER(CONVERT(varchar(36), Id)) AS Id,
            LOWER(CONVERT(varchar(36), RoleId)) AS RoleId,
            IsActive
     FROM UserCompanyMemberships
     WHERE UserId = @user AND TenantId = @company`,
    {
      user: [sql.UniqueIdentifier, userId],
      company: [sql.UniqueIdentifier, companyId],
    },
  );
  return result.recordset[0];
}

/** The audit rows of the workflow, scoped to the fixture's destination company. */
export async function readAudit(fixture, { companyId, eventType }) {
  const result = await fixture.run(
    `SELECT LOWER(CONVERT(varchar(36), Id)) AS Id,
            LOWER(CONVERT(varchar(36), UserId)) AS UserId,
            EventType, EntityType, EntityId, DetailsJson
     FROM SecurityAuditEvents
     WHERE TenantId = @company AND EventType = @event
     ORDER BY OccurredAtUtc DESC`,
    {
      company: [sql.UniqueIdentifier, companyId],
      event: [sql.VarChar(120), eventType],
    },
  );
  return result.recordset;
}

/** The permission ids a role currently holds (used to prove a role change is re-checked). */
export async function readRolePermissions(fixture, roleId) {
  const result = await fixture.run(
    'SELECT LOWER(CONVERT(varchar(36), PermissionId)) AS PermissionId FROM CompanyRolePermissions WHERE RoleId = @role',
    { role: [sql.UniqueIdentifier, roleId] },
  );
  return result.recordset.map((row) => row.PermissionId);
}
