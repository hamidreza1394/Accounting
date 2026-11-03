import { describe, expect, it } from 'vitest';
import { eventTypeLabel, knownEventTypes } from './securityEvents';

describe('security event labels', () => {
  it('labels a known code and leaves an unknown one to be shown as it is', () => {
    expect(eventTypeLabel('PasswordChanged')).toBe('تغییر رمز عبور');
    expect(eventTypeLabel('SomethingNew')).toBeNull();
  });

  it('labels the login, work-context, corporate-group and provisioning events the server writes', () => {
    for (const code of ['LoginSucceeded', 'LoginFailed', 'LoginDeniedInactive', 'LoginDeniedEnrollmentPending', 'WorkContextSelected', 'WorkContextChanged', 'WorkContextSelectionDenied', 'CorporateGroupCreated', 'CorporateGroupMemberAdded', 'CorporateGroupMemberEnded', 'ProvisioningApplied', 'ProvisioningRejected']) {
      expect(eventTypeLabel(code), code).not.toBeNull();
    }
    expect(eventTypeLabel('LoginSucceeded')).toBe('ورود موفق');
  });

  it('is not fooled by object prototype names', () => {
    expect(eventTypeLabel('toString')).toBeNull();
    expect(eventTypeLabel('constructor')).toBeNull();
  });

  it('offers every labelled code as a filter suggestion', () => {
    expect(knownEventTypes).toContain('UserCompanyRoleAssigned');
    expect(new Set(knownEventTypes).size).toBe(knownEventTypes.length);
  });
});
