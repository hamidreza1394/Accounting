import { describe, expect, it } from 'vitest';
import { productDetail } from '../test/tradeFixtures';
import {
  buildProductBody,
  emptyProductForm,
  isProductFormDirty,
  productFormFromDetail,
  validateProductForm,
  type ProductFormState,
} from './productValidation';

const valid: ProductFormState = { ...emptyProductForm, code: 'PRD-1', name: 'گندم', baseUnitCode: 'KGM' };
const units = new Set(['KGM', 'TNE']);

describe('validateProductForm (mirror of the server rules)', () => {
  it('accepts a minimal valid form and a full one', () => {
    expect(validateProductForm(valid, units)).toEqual({});
    expect(validateProductForm({ ...valid, productGroupCode: 'G', hsCode: '1001.19.00' }, units)).toEqual({});
  });

  it('requires the code, the name and the base unit', () => {
    const errors = validateProductForm({ ...emptyProductForm, code: '  ' }, units);
    expect(errors.code).toBe('کد الزامی است.');
    expect(errors.name).toBe('نام الزامی است.');
    expect(errors.baseUnitCode).toBe('واحد پایه الزامی است.');
  });

  it('enforces the length limits on the trimmed value (64 / 200 / 64 / 16)', () => {
    expect(validateProductForm({ ...valid, code: 'a'.repeat(64) }, units).code).toBeUndefined();
    expect(validateProductForm({ ...valid, code: 'a'.repeat(65) }, units).code).toMatch(/بیش از/);
    expect(validateProductForm({ ...valid, name: 'a'.repeat(201) }, units).name).toMatch(/بیش از/);
    expect(validateProductForm({ ...valid, productGroupCode: 'a'.repeat(65) }, units).productGroupCode).toMatch(/بیش از/);
    expect(validateProductForm({ ...valid, hsCode: 'a'.repeat(16) }, units).hsCode).toBeUndefined();
    expect(validateProductForm({ ...valid, hsCode: 'a'.repeat(17) }, units).hsCode).toMatch(/بیش از/);
    expect(validateProductForm({ ...valid, hsCode: ` ${'1'.repeat(16)} ` }, units).hsCode).toBeUndefined();
    expect(validateProductForm({ ...valid, hsCode: ' 0101.21  ' }, units).hsCode).toBeUndefined();
  });

  it('accepts only printable ASCII for the HS code (text, leading zeroes kept)', () => {
    expect(validateProductForm({ ...valid, hsCode: '0101 21~' }, units).hsCode).toBeUndefined();
    expect(validateProductForm({ ...valid, hsCode: '۱۰۰۱' }, units).hsCode).toMatch(/لاتین/);
    expect(validateProductForm({ ...valid, hsCode: 'é' }, units).hsCode).toMatch(/لاتین/);
    expect(validateProductForm({ ...valid, hsCode: 'a\u0000b' }, units).hsCode).toMatch(/لاتین/);
    expect(buildProductBody({ ...valid, hsCode: ' 0101.21 ' }).hsCode).toBe('0101.21');
  });

  it('refuses control and bidi characters in code, name and group, but allows the ZWNJ', () => {
    expect(validateProductForm({ ...valid, name: 'ab‮cd' }, units).name).toMatch(/غیرمجاز/);
    expect(validateProductForm({ ...valid, code: 'a\u0007' }, units).code).toMatch(/غیرمجاز/);
    expect(validateProductForm({ ...valid, productGroupCode: 'a⁦' }, units).productGroupCode).toMatch(/غیرمجاز/);
    expect(validateProductForm({ ...valid, name: 'می‌خواهم' }, units).name).toBeUndefined();
  });

  it('refuses ALM, line/paragraph separators and BOM inside the value (a leading/trailing BOM is stripped by JS trim(), unlike .NET)', () => {
    for (const forbidden of ['\u061c', '\u2028', '\u2029', '\ufeff']) {
      expect(validateProductForm({ ...valid, code: `a${forbidden}b` }, units).code).toMatch(/غیرمجاز/);
      expect(validateProductForm({ ...valid, name: `a${forbidden}b` }, units).name).toMatch(/غیرمجاز/);
      expect(validateProductForm({ ...valid, productGroupCode: `a${forbidden}b` }, units).productGroupCode).toMatch(/غیرمجاز/);
    }
    expect(validateProductForm({ ...valid, name: '\ufeffگندم' }, units).name).toBeUndefined();
  });

  it('never echoes the submitted value in a message', () => {
    expect(validateProductForm({ ...valid, hsCode: `SECRET${'é'.repeat(20)}` }, units).hsCode).not.toContain('SECRET');
  });

  it('checks the unit against the loaded list only when it is loaded', () => {
    expect(validateProductForm({ ...valid, baseUnitCode: 'ZZZ' }, units).baseUnitCode).toBe('واحد پایه نامعتبر است.');
    expect(validateProductForm({ ...valid, baseUnitCode: 'ZZZ' }, null).baseUnitCode).toBeUndefined();
  });
});

describe('product body and dirty tracking', () => {
  it('builds a trimmed body: empty optional text becomes null and nothing else is included', () => {
    expect(buildProductBody({ ...valid, code: ' PRD-1 ', productGroupCode: '  ', hsCode: '' })).toEqual({
      code: 'PRD-1',
      name: 'گندم',
      productGroupCode: null,
      hsCode: null,
      baseUnitCode: 'KGM',
    });
  });

  it('is dirty only after a real change (whitespace does not count)', () => {
    const baseline = productFormFromDetail(productDetail());
    expect(isProductFormDirty(baseline, baseline)).toBe(false);
    expect(isProductFormDirty({ ...baseline, name: ` ${baseline.name} ` }, baseline)).toBe(false);
    expect(isProductFormDirty({ ...baseline, name: 'تغییر' }, baseline)).toBe(true);
    expect(isProductFormDirty({ ...baseline, baseUnitCode: 'TNE' }, baseline)).toBe(true);
    expect(isProductFormDirty({ ...baseline, hsCode: '' }, baseline)).toBe(true);
  });
});
