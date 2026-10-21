// Normalisation for the company search: users type Persian or Arabic digits and letters, extra
// spaces and invisible marks, and the stored name or national id may use any of them.

const digitMaps: ReadonlyArray<readonly [string, string]> = [
  ['۰۱۲۳۴۵۶۷۸۹', '0123456789'],
  ['٠١٢٣٤٥٦٧٨٩', '0123456789'],
];

/** Lower-cases, unifies Arabic/Persian letters and digits, drops invisible marks, collapses spaces. */
export function normalizeSearchText(value: string): string {
  let text = value
    .replace(/[‌‍‎‏‪-‮]/g, ' ')
    .replace(/ي/g, 'ی')
    .replace(/ك/g, 'ک')
    .replace(/ۀ/g, 'ه')
    .replace(/[أإآ]/g, 'ا');

  for (const [from, to] of digitMaps) {
    for (let index = 0; index < from.length; index += 1) {
      text = text.replaceAll(from.charAt(index), to.charAt(index));
    }
  }

  return text.toLowerCase().replace(/\s+/g, ' ').trim();
}

/** True when every word of the query occurs in the company name or in its national id. */
export function matchesCompany(query: string, name: string, nationalId: string | null): boolean {
  const words = normalizeSearchText(query).split(' ').filter(Boolean);
  if (words.length === 0) {
    return true;
  }
  const haystack = `${normalizeSearchText(name)} ${normalizeSearchText(nationalId ?? '')}`;

  return words.every((word) => haystack.includes(word));
}
