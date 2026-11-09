import { useEffect, useId, useRef, useState } from 'react';
import { Camera, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/api/client';
import { deleteEmployeePhoto, employeePhotoUrl, putEmployeePhoto } from '@/features/hr/api/hrClient';
import { initialsOf, photoAcceptAttribute, readPhotoPreview, validatePhotoFile } from '@/lib/employeePhoto';
import { Info, TabDataBoundary, useProfileTabData } from '../profile/profileShared';
import { employeeLinkKey, loadLinkedEmployee, type FormTabCache, type UserFormContext } from './userFormShared';

/** The photo picked in the create form: held in the form (above the tab panels) until the save. */
export type PhotoDraft = { file: File; preview: string };

type PhotoViewProps = {
  src: string | null;
  initials: string;
  busy: boolean;
  error: string | null;
  /** Why the photo cannot be chosen right now; the buttons are disabled and the reason is shown. */
  disabledReason: string | null;
  hint: string;
  hasPhoto: boolean;
  onPick: (file: File) => void;
  onRemove: () => void;
  onImageLoaded?: () => void;
  onImageFailed?: () => void;
};

/** The avatar, the buttons and the messages; the two wrappers below decide where the photo lives. */
function PhotoView({ src, initials, busy, error, disabledReason, hint, hasPhoto, onPick, onRemove, onImageLoaded, onImageFailed }: PhotoViewProps) {
  const inputId = useId();
  const messageId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const disabled = disabledReason !== null || busy;
  const [broken, setBroken] = useState(false);
  useEffect(() => setBroken(false), [src]);
  return (
    <div className="flex flex-wrap items-center gap-4 sm:col-span-2" data-testid="user-photo-field">
      <div
        className="relative flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-full border border-line bg-cool text-2xl font-extrabold text-navy"
        aria-busy={busy || undefined}
      >
        {src && !broken ? (
          <img
            src={src}
            alt="عکس پرسنلی"
            className="size-full object-cover"
            onLoad={onImageLoaded}
            onError={() => {
              setBroken(true);
              onImageFailed?.();
            }}
          />
        ) : (
          <span aria-hidden="true">{initials || '؟'}</span>
        )}
        {busy ? <span className="absolute inset-0 flex items-center justify-center bg-white/70 text-xs font-semibold text-navy">در حال ذخیره…</span> : null}
      </div>
      <div className="min-w-0 flex-1 space-y-2">
        <p className="text-sm font-semibold text-ink">عکس پرسنلی</p>
        <div className="flex flex-wrap gap-2">
          <input
            ref={inputRef}
            id={inputId}
            type="file"
            accept={photoAcceptAttribute}
            className="sr-only"
            disabled={disabled}
            aria-describedby={messageId}
            aria-label="انتخاب فایل عکس پرسنلی"
            onChange={(event) => {
              const file = event.target.files?.[0];
              event.target.value = '';
              if (file) onPick(file);
            }}
          />
          <Button type="button" variant="outline" size="sm" disabled={disabled} onClick={() => inputRef.current?.click()}>
            <Camera size={16} aria-hidden="true" />
            {hasPhoto ? 'تغییر عکس' : 'انتخاب عکس'}
          </Button>
          {hasPhoto ? (
            <Button type="button" variant="outline" size="sm" disabled={disabled} onClick={onRemove}>
              <Trash2 size={16} aria-hidden="true" />
              حذف عکس
            </Button>
          ) : null}
        </div>
        <p id={messageId} className="text-xs leading-5 text-sub">
          {disabledReason ?? hint}
        </p>
        {error ? (
          <p role="alert" className="text-xs font-medium text-danger">
            {error}
          </p>
        ) : null}
      </div>
    </div>
  );
}

const pickHint = 'JPEG، PNG یا WebP و حداکثر ۲ مگابایت. عکس همراه پرونده پرسنلی شرکت ذخیره می‌شود.';

type CreateProps = {
  draft: PhotoDraft | null;
  onDraftChange: (draft: PhotoDraft | null) => void;
  firstName: string;
  lastName: string;
  /** Null when the photo can be stored with the personnel record created by this save. */
  unavailableReason: string | null;
  /** A reason the chosen photo would not be saved (blocks the save until resolved). */
  conflictMessage: string | null;
};

/** Create mode: the chosen photo is previewed now and uploaded right after the personnel record exists. */
export function CreatePhotoField({ draft, onDraftChange, firstName, lastName, unavailableReason, conflictMessage }: CreateProps) {
  const [error, setError] = useState<string | null>(null);
  const pick = (file: File) => {
    const problem = validatePhotoFile(file);
    if (problem) {
      setError(problem);
      return;
    }
    setError(null);
    readPhotoPreview(file)
      .then((preview) => onDraftChange({ file, preview }))
      .catch(() => setError('نمایش پیش‌نمایش عکس انجام نشد. فایل دیگری انتخاب کنید.'));
  };
  return (
    <PhotoView
      src={draft?.preview ?? null}
      initials={initialsOf(firstName, lastName)}
      busy={false}
      error={error ?? conflictMessage}
      disabledReason={unavailableReason}
      hint={`${pickHint} عکس پس از «ایجاد کاربر» ذخیره می‌شود.`}
      hasPhoto={draft !== null}
      onPick={pick}
      onRemove={() => {
        setError(null);
        onDraftChange(null);
      }}
    />
  );
}

type EditProps = {
  userId: string;
  firstName: string;
  lastName: string;
  companyId: string;
  context: UserFormContext;
  cache: FormTabCache;
  onUnauthenticated: (error: unknown) => boolean;
};

const failureMessage = (cause: unknown): string => (cause instanceof ApiError ? cause.message : 'ذخیرهٔ عکس انجام نشد. لطفاً دوباره تلاش کنید.');

function EditPhoto({ employeeId, companyId, firstName, lastName, context, onUnauthenticated }: Omit<EditProps, 'userId' | 'cache'> & { employeeId: string }) {
  const canChange = context.can('hr.employees.update', companyId);
  const [version, setVersion] = useState(() => Date.now());
  const [hasPhoto, setHasPhoto] = useState(true);
  const [preview, setPreview] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const busyRef = useRef(false);

  const run = async (action: () => Promise<void>) => {
    if (busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    setError(null);
    try {
      await action();
    } catch (cause) {
      if (!onUnauthenticated(cause)) setError(failureMessage(cause));
    } finally {
      busyRef.current = false;
      setBusy(false);
    }
  };

  const pick = (file: File) => {
    const problem = validatePhotoFile(file);
    if (problem) {
      setError(problem);
      return;
    }
    void run(async () => {
      const shown = await readPhotoPreview(file).catch(() => null);
      setPreview(shown);
      try {
        await putEmployeePhoto(companyId, employeeId, file);
      } catch (cause) {
        setPreview(null);
        throw cause;
      }
      setVersion(Date.now());
      setHasPhoto(true);
    });
  };

  const remove = () =>
    run(async () => {
      await deleteEmployeePhoto(companyId, employeeId);
      setPreview(null);
      setHasPhoto(false);
      setVersion(Date.now());
    });

  return (
    <PhotoView
      src={preview ?? (hasPhoto ? employeePhotoUrl(companyId, employeeId, version) : null)}
      initials={initialsOf(firstName, lastName)}
      busy={busy}
      error={error}
      disabledReason={canChange ? null : 'برای تغییر عکس، مجوز «ویرایش کارمند» در شرکت فعال لازم است.'}
      hint={`${pickHint} تغییر عکس بلافاصله ذخیره می‌شود.`}
      hasPhoto={preview !== null || hasPhoto}
      onPick={pick}
      onRemove={() => void remove()}
      onImageLoaded={() => setHasPhoto(true)}
      onImageFailed={() => {
        if (preview === null) setHasPhoto(false);
      }}
    />
  );
}

/** Edit mode: the photo of the personnel record linked to this user in the work-context company. */
export function EditPhotoField({ userId, companyId, cache, onUnauthenticated, ...rest }: EditProps) {
  const state = useProfileTabData(cache, employeeLinkKey(userId, companyId), (signal) => loadLinkedEmployee(companyId, userId, signal), onUnauthenticated);
  return (
    <TabDataBoundary state={state} label="در حال دریافت عکس پرسنلی…">
      {(employee) =>
        employee ? (
          <EditPhoto employeeId={employee.id} companyId={companyId} onUnauthenticated={onUnauthenticated} {...rest} />
        ) : (
          <Info label="عکس پرسنلی">
            <span className="text-sub">عکس همراه پرونده پرسنلی ذخیره می‌شود؛ پرونده پرسنلی متصل نشده است.</span>
          </Info>
        )
      }
    </TabDataBoundary>
  );
}
