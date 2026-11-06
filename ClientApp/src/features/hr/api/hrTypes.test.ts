import { describe, expect, it } from 'vitest';
import { employeeDetail, employeeListItem } from '../test/hrFixtures';
import {
  HrContractError,
  parseCreatedId,
  parseHrEmployeeDetail,
  parseHrEmployeeListResponse,
  parseHrPositionDetail,
  parseHrUnitDetail,
  parseHrUnitSubtree,
  parseHrUnitTreeResponse,
} from './hrTypes';

describe('hrTypes parsers', () => {
  it('parses a well-formed employee list page', () => {
    const parsed = parseHrEmployeeListResponse({
      items: [employeeListItem()],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    });
    expect(parsed.items[0]?.employmentStatus).toBe(1);
    expect(parsed.items[0]?.unitName).toBe('دفتر مرکزی');
  });

  it('rejects an employment status outside the domain enum', () => {
    const broken = employeeListItem();
    (broken as unknown as Record<string, unknown>).employmentStatus = 7;
    expect(() => parseHrEmployeeListResponse({ items: [broken], totalCount: 1, page: 1, pageSize: 25 })).toThrow(HrContractError);
  });

  it('rejects a missing or malformed rowVersion on details', () => {
    expect(() => parseHrEmployeeDetail({ ...employeeDetail(), rowVersion: 'not base64!' })).toThrow(HrContractError);
    const withoutToken = employeeDetail() as unknown as Record<string, unknown>;
    delete withoutToken.rowVersion;
    expect(() => parseHrEmployeeDetail(withoutToken)).toThrow(HrContractError);
  });

  it('accepts a detail whose active assignment is null and keeps instants as sent', () => {
    const detail = parseHrEmployeeDetail(employeeDetail({ activeAssignment: null, hiredOnUtc: '2026-01-10T08:30:00' }));
    expect(detail.activeAssignment).toBeNull();
    // No `Z` on the wire: the value stays exactly as sent (lib/time normalizes for display only).
    expect(detail.hiredOnUtc).toBe('2026-01-10T08:30:00');
  });

  it('parses the unit tree recursively, including inactive units', () => {
    const child = { ...employeeListItem, id: undefined }; // shape guard below uses unit shapes
    void child;
    const parsed = parseHrUnitTreeResponse({
      roots: [
        {
          id: '5d2e9f10-2222-4000-8000-00000000ef01',
          code: 'HQ',
          name: 'دفتر مرکزی',
          description: null,
          parentUnitId: null,
          isActive: true,
          validFromUtc: null,
          validToUtc: null,
          children: [
            {
              id: 'b1b2c3d4-4444-4000-8000-00000000ef02',
              code: 'SALES',
              name: 'فروش',
              description: null,
              parentUnitId: '5d2e9f10-2222-4000-8000-00000000ef01',
              isActive: false,
              validFromUtc: null,
              validToUtc: null,
              children: [],
            },
          ],
        },
      ],
    });
    expect(parsed.roots[0]?.children[0]?.isActive).toBe(false);
  });

  it('parses the subtree node with its assignment counts', () => {
    const subtree = parseHrUnitSubtree({
      id: '5d2e9f10-2222-4000-8000-00000000ef01',
      code: 'HQ',
      name: 'دفتر مرکزی',
      isActive: true,
      parentUnitId: null,
      activeAssignmentCount: 3,
      children: [],
      validFromUtc: null,
      validToUtc: null,
    });
    expect(subtree.activeAssignmentCount).toBe(3);
  });

  it('parses unit and position details with their counters', () => {
    const unit = parseHrUnitDetail({
      id: '5d2e9f10-2222-4000-8000-00000000ef01',
      code: 'HQ',
      name: 'دفتر مرکزی',
      description: null,
      parentUnitId: null,
      isActive: true,
      validFromUtc: null,
      validToUtc: null,
      activeChildCount: 2,
      activeAssignmentCount: 4,
      rowVersion: 'AAAAAAAAB9I=',
    });
    const position = parseHrPositionDetail({
      id: '7a1c3b40-3333-4000-8000-00000000beef',
      code: 'ACC',
      name: 'حسابدار',
      description: null,
      rank: 2,
      isActive: true,
      activeAssignmentCount: 4,
      rowVersion: 'AAAAAAAAB9I=',
    });
    expect(unit.activeChildCount).toBe(2);
    expect(position.rank).toBe(2);
  });

  it('rejects created responses with a broken id and accepts valid ones', () => {
    expect(parseCreatedId({ id: '8c5b7e2a-1111-4000-8000-00000000abcd' })).toEqual({
      id: '8c5b7e2a-1111-4000-8000-00000000abcd',
    });
    expect(() => parseCreatedId({ id: 'nope' })).toThrow(HrContractError);
  });

  it('rejects non-object bodies loudly', () => {
    expect(() => parseHrEmployeeListResponse(null)).toThrow(HrContractError);
    expect(() => parseHrEmployeeListResponse([1, 2])).toThrow(HrContractError);
  });
});
