import { describe, expect, it } from 'vitest';
import { appRoutes } from '@/components/AppRoutes';
import { navigation } from '@/components/shell/AppSidebar';

/**
 * منو و روتر باید یک حقیقت داشته باشند (Stage 2-B، D-075):
 * - هر آیتم منوی دارای مسیر باید دقیقاً به یک Route ثبت‌شده برسد — هیچ مسیر جعلی.
 * - هر آیتم بدون مسیر باید placeholder صادقانهٔ «به‌زودی» باشد، نه لینک خراب.
 * - هیچ دو Routeی هم‌مسیر نباشند (روتر اولین تطبیق را برمی‌گرداند).
 * - هیچ نام مسیر داخلی انگلیسی به کاربر نمایش داده نشود.
 */

const PERSIAN_ONLY_INTERNAL = ['hr', 'roles', 'users', 'reporting', 'trade', 'consolidation'];

function leafEntries() {
  return navigation.flatMap((entry) => (entry.kind === 'item' ? [entry.leaf] : entry.group.children));
}

function groupEntries() {
  return navigation.flatMap((entry) => (entry.kind === 'group' ? [entry.group] : []));
}

const routePaths = appRoutes.map((route) => route.path);

describe('menu ↔ router consistency (Issue #444 stage 2-B / D-075)', () => {
  it('resolves every routed menu leaf to a real registered route', () => {
    const routedLeaves = leafEntries().filter((leaf) => leaf.to !== undefined);
    expect(routedLeaves.length).toBeGreaterThan(0);
    for (const leaf of routedLeaves) {
      expect(routePaths, `menu leaf «${leaf.label}» targets a non-registered route: ${leaf.to}`).toContain(leaf.to);
    }
  });

  it('keeps unrouted menu leaves as honest placeholders (no dead links)', () => {
    const placeholders = leafEntries().filter((leaf) => leaf.to === undefined);
    expect(placeholders.length).toBeGreaterThan(0);
    for (const leaf of placeholders) {
      expect(leaf.label.length).toBeGreaterThan(0);
    }
  });

  it('never registers two routes with the same path', () => {
    const duplicates = routePaths.filter((path, index) => routePaths.indexOf(path) !== index);
    expect(duplicates).toEqual([]);
  });

  it('gives every route a real Persian title', () => {
    for (const route of appRoutes) {
      expect(route.title.trim().length, `route ${route.path} has an empty title`).toBeGreaterThan(0);
      expect(/[\u0600-\u06FF]/.test(route.title), `route ${route.path} title is not Persian: ${route.title}`).toBe(true);
    }
  });

  it('never shows internal English route names as menu labels', () => {
    for (const leaf of leafEntries()) {
      expect(PERSIAN_ONLY_INTERNAL).not.toContain(leaf.label);
    }
  });

  it('keeps groups non-empty and never declares a group open by default', () => {
    const groups = groupEntries();
    expect(groups.length).toBeGreaterThan(0);
    for (const group of groups) {
      expect(group.children.length, `group «${group.label}» is empty`).toBeGreaterThan(0);
      expect('openByDefault' in group, `group «${group.label}» must not be open by default`).toBe(false);
    }
  });

  it('keeps the foreign-trade group honest: the party and product leaves have their real routes, «واردات و صادرات» stays an unrouted placeholder', () => {
    const trade = groupEntries().find((group) => group.label === 'بازرگانی خارجی');
    expect(trade).toBeDefined();
    const party = trade?.children.find((leaf) => leaf.label === 'طرف‌حساب‌ها');
    expect(party?.to).toBe('/trade/parties');
    for (const path of ['/trade/parties', '/trade/parties/new', '/trade/parties/:id', '/trade/parties/:id/edit']) {
      expect(routePaths).toContain(path);
    }
    // `/new` precedes `/:id`, otherwise «new» would be read as a party id.
    expect(routePaths.indexOf('/trade/parties/new')).toBeLessThan(routePaths.indexOf('/trade/parties/:id'));
    const product = trade?.children.find((leaf) => leaf.label === 'کالاها');
    expect(product?.to).toBe('/trade/products');
    for (const path of ['/trade/products', '/trade/products/new', '/trade/products/:id', '/trade/products/:id/edit']) {
      expect(routePaths).toContain(path);
    }
    expect(routePaths.indexOf('/trade/products/new')).toBeLessThan(routePaths.indexOf('/trade/products/:id'));
    const placeholder = trade?.children.find((leaf) => leaf.label === 'واردات و صادرات');
    expect(placeholder).toBeDefined();
    expect(placeholder?.to).toBeUndefined();
    expect('openByDefault' in (trade as object)).toBe(false);
  });
});
