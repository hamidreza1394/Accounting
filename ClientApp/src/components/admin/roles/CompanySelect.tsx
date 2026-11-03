import type { CompanyChoices } from './useCompanyChoices';
import { FormField, Select } from '../../ui/FormField';

export const companyListGapNote = 'فهرست شرکت‌ها محدود به شرکت‌های عضو شماست.';

type Props = {
  choices: CompanyChoices;
  value: string;
  onChange: (companyId: string) => void;
  onRetry: () => void;
};

/**
 * Chooses the company whose roles are shown. The API has no installation-wide company list, so the
 * options are the signed-in user's own companies (`GET /companies`); an id taken from the URL that
 * is not among them is kept as an explicit option and the server decides whether it may be used.
 */
export function CompanySelect({ choices, value, onChange, onRetry }: Props) {
  const companies = choices.kind === 'ready' ? choices.companies : [];
  const known = companies.some((company) => company.id === value);
  return (
    <div className="space-y-1">
      <FormField label="شرکت" className="w-full sm:w-80" hint={companyListGapNote}>
        <Select
          className="h-11 sm:h-10"
          value={value}
          disabled={choices.kind === 'loading'}
          onChange={(event) => onChange(event.target.value)}
        >
          {choices.kind === 'loading' ? <option value={value}>در حال بارگذاری…</option> : null}
          {!known && choices.kind !== 'loading' && value ? <option value={value}>شرکت خارج از فهرست شما ({value})</option> : null}
          {companies.map((company) => (
            <option key={company.id} value={company.id}>
              {company.name}
            </option>
          ))}
        </Select>
      </FormField>
      {choices.kind === 'error' ? (
        <p role="alert" className="text-xs text-danger">
          {choices.message}{' '}
          <button type="button" onClick={onRetry} className="rounded font-semibold underline outline-none focus-visible:ring-2 focus-visible:ring-gold">
            تلاش دوباره
          </button>
        </p>
      ) : null}
    </div>
  );
}
