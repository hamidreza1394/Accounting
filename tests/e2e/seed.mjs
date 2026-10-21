// Seeds the E2E database (already migrated by "dotnet Server.dll --migrate") with uniquely
// generated accounts and companies, and writes the credentials to .e2e-state.json (git-ignored)
// for the tests and for the server start (Master credential). Passwords are random per run.
//   FARANOU_E2E_SQL  connection string of the E2E database (its name must start with FaranouE2E)
import { randomBytes, randomUUID, pbkdf2Sync } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import sql from 'mssql';

const connectionString = process.env.FARANOU_E2E_SQL;
if (!connectionString || !/Database=FaranouE2E/i.test(connectionString)) {
  throw new Error('FARANOU_E2E_SQL must point to a disposable database whose name starts with FaranouE2E.');
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

const password = () => `E2e-${randomBytes(12).toString('hex')}`;
const suffix = randomBytes(3).toString('hex');

const state = {
  master: { username: `e2e-master-${suffix}`, password: password() },
  users: {
    alice: { username: `alice${suffix}`, password: password(), id: randomUUID(), active: true },
    bob: { username: `bob${suffix}`, password: password(), id: randomUUID(), active: true },
    carol: { username: `carol${suffix}`, password: password(), id: randomUUID(), active: false },
    dave: { username: `dave${suffix}`, password: password(), id: randomUUID(), active: true },
    erin: { username: `erin${suffix}`, password: password(), id: randomUUID(), active: true },
  },
  companies: {
    a: { id: randomUUID(), name: `شرکت الف ${suffix}`, roleId: randomUUID() },
    b: { id: randomUUID(), name: `شرکت ب ${suffix}`, roleId: randomUUID() },
    c: { id: randomUUID(), name: `شرکت ج ${suffix}`, roleId: randomUUID() },
  },
  secretPeriodTitle: `دوره محرمانه ب ${suffix}`,
};
state.master.hash = hashPasswordV3(state.master.password);

const pool = await sql.connect(connectionString);
const now = new Date();

async function run(statement, parameters) {
  const request = pool.request();
  for (const [name, [type, value]] of Object.entries(parameters)) {
    request.input(name, type, value);
  }
  await request.query(statement);
}

for (const user of Object.values(state.users)) {
  await run(
    `INSERT INTO Users (Id, Ordering, InsertDateTime, UpdateDateTime, IsActive, IsSystemic, IsProgrammer, IsUndeletable,
       IsProfilePublic, IsVisibleInContactUsPage, IsEmailAddressVerified, IsCellPhoneNumberVerified,
       Username, Password, FirstName, LastName, EmailAddress, EmailAddressVerificationKey)
     VALUES (@id, 10000, @now, @now, @active, 0, 0, 0, 0, 0, 1, 0, @username, @password, @first, N'آزمون', @email, @key)`,
    {
      id: [sql.UniqueIdentifier, user.id], now: [sql.DateTime2, now], active: [sql.Bit, user.active],
      username: [sql.VarChar(20), user.username], password: [sql.VarChar(255), hashPasswordV3(user.password)],
      first: [sql.NVarChar(50), user.username.replace(/[0-9a-f]+$/, '')], email: [sql.VarChar(254), `${user.username}@e2e.test`],
      key: [sql.UniqueIdentifier, randomUUID()],
    },
  );
}

for (const company of Object.values(state.companies)) {
  await run(`INSERT INTO Companies (Id, Ordering, InsertDateTime, Name, IsActive) VALUES (@id, 10000, @now, @name, 1)`,
    { id: [sql.UniqueIdentifier, company.id], now: [sql.DateTime2, now], name: [sql.NVarChar(200), company.name] });
  await run(
    `INSERT INTO CompanyRoles (Id, Ordering, InsertDateTime, TenantId, Code, Name, IsSystemRole, IsActive)
     VALUES (@id, 10000, @now, @tenant, 'accountant', N'حسابدار', 0, 1)`,
    { id: [sql.UniqueIdentifier, company.roleId], now: [sql.DateTime2, now], tenant: [sql.UniqueIdentifier, company.id] });
}

async function member(user, company) {
  await run(
    `INSERT INTO UserCompanyMemberships (Id, Ordering, InsertDateTime, UserId, TenantId, RoleId, IsActive, CreatedAtUtc)
     VALUES (@id, 10000, @now, @user, @tenant, @role, 1, @now)`,
    { id: [sql.UniqueIdentifier, randomUUID()], now: [sql.DateTime2, now], user: [sql.UniqueIdentifier, user.id],
      tenant: [sql.UniqueIdentifier, company.id], role: [sql.UniqueIdentifier, company.roleId] });
}

async function period(company, title, start, end, status = 1) {
  await run(
    `INSERT INTO FiscalPeriods (Id, Ordering, InsertDateTime, TenantId, Title, StartDate, EndDate, Status)
     VALUES (@id, 10000, @now, @tenant, @title, @start, @end, @status)`,
    { id: [sql.UniqueIdentifier, randomUUID()], now: [sql.DateTime2, now], tenant: [sql.UniqueIdentifier, company.id],
      title: [sql.NVarChar(100), title], start: [sql.Date, start], end: [sql.Date, end], status: [sql.Int, status] });
}

const { alice, bob, carol, dave } = state.users;
const { a, b, c } = state.companies;
await member(alice, a);
await member(alice, c);
await member(bob, b);
await member(carol, a);
await member(dave, c); // company without any fiscal period
// erin has no company at all
await period(a, 'سال مالی ۱۴۰۵', '2026-03-21', '2027-03-20', 1);
await period(a, 'سال مالی ۱۴۰۴', '2025-03-21', '2026-03-20', 2);
await period(b, state.secretPeriodTitle, '2026-03-21', '2027-03-20', 1);

await pool.close();
writeFileSync(new URL('./.e2e-state.json', import.meta.url), JSON.stringify(state, null, 2));
console.log('E2E data seeded (credentials in tests/e2e/.e2e-state.json, never committed).');
