import { describe, expect, it } from 'vitest';
import { userDetails, userItem, listBody } from '../test/adminUsers';
import { memberItem, memberPage } from '../test/roleMembers';
import { myRequest, pageBody, pendingItem } from '../test/membership';
import {
  parseAdminUserDetails,
  parseAdminUserList,
  parseCompanyRoleMembers,
  parseLoginHistory,
  parseMyMembershipRequests,
  parseMyProfile,
  parsePendingMembershipRequests,
} from './types';

/**
 * The wire contract of time (docs/TIME_CONTRACT.md): `*Utc` fields are UTC even when the server
 * writes them without `Z`; the legacy server-local fields are never reinterpreted.
 */
const naive = '2026-11-03T10:00:00';
const naiveUtc = '2026-11-03T10:00:00Z';

describe('UTC fields (*Utc)', () => {
  it('login history', () => {
    const [item] = parseLoginHistory([{ eventType: 'LoginSucceeded', occurredAtUtc: naive, ipAddress: null }]);
    expect(item?.occurredAtUtc).toBe(naiveUtc);
  });

  it('user memberships', () => {
    const details = parseAdminUserDetails(userDetails(1, { memberships: [{ companyId: 'c1', companyName: 'ش', roleCode: 'R', roleName: 'ر', isActive: true, createdAtUtc: naive }] }));
    expect(details.memberships[0]?.createdAtUtc).toBe(naiveUtc);
  });

  it('role members and membership requests', () => {
    expect(parseCompanyRoleMembers(memberPage([memberItem(1, { memberSinceUtc: naive })])).items[0]?.memberSinceUtc).toBe(naiveUtc);
    const mine = parseMyMembershipRequests(pageBody([myRequest({ requestedAtUtc: naive, decidedAtUtc: naive })]));
    expect(mine.items[0]).toMatchObject({ requestedAtUtc: naiveUtc, decidedAtUtc: naiveUtc });
    expect(parsePendingMembershipRequests(pageBody([pendingItem(1, { requestedAtUtc: naive })])).items[0]?.requestedAtUtc).toBe(naiveUtc);
  });

  it('a value that already has Z or an offset is not converted twice', () => {
    const [item] = parseLoginHistory([{ eventType: 'LogoutSucceeded', occurredAtUtc: '2026-11-03T13:30:00+03:30', ipAddress: null }]);
    expect(item?.occurredAtUtc).toBe('2026-11-03T13:30:00+03:30');
    const [z] = parseLoginHistory([{ eventType: 'LogoutSucceeded', occurredAtUtc: naiveUtc, ipAddress: null }]);
    expect(z?.occurredAtUtc).toBe(naiveUtc);
  });
});

describe('legacy server-local fields (DateTime.Now on the server)', () => {
  it('are returned exactly as written: never given a Z, because they are not UTC', () => {
    const list = parseAdminUserList(listBody([userItem(1, { lastLoginDateTime: naive, insertDateTime: naive })]));
    expect(list.items[0]).toMatchObject({ lastLoginDateTime: naive, insertDateTime: naive });
    const details = parseAdminUserDetails(userDetails(1, { lastLoginDateTime: naive, insertDateTime: naive, updateDateTime: naive }));
    expect(details).toMatchObject({ lastLoginDateTime: naive, insertDateTime: naive, updateDateTime: naive });
    const profile = parseMyProfile({
      username: 'u', firstName: null, lastName: null, emailAddress: null, cellPhoneNumber: null,
      isEmailAddressVerified: false, isCellPhoneNumberVerified: false, lastLoginDateTime: naive, insertDateTime: naive,
    });
    expect(profile).toMatchObject({ lastLoginDateTime: naive, insertDateTime: naive });
  });
});
