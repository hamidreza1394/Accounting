/** The client-side checks of a personnel photo; the server checks the real file signature again. */
export const photoMaxBytes = 2 * 1024 * 1024;
export const photoAcceptedTypes = ['image/jpeg', 'image/png', 'image/webp'] as const;
export const photoAcceptAttribute = photoAcceptedTypes.join(',');

export const photoTypeMessage = 'فقط عکس با قالب JPEG، PNG یا WebP پذیرفته می‌شود.';
export const photoSizeMessage = 'حجم عکس نباید بیشتر از ۲ مگابایت باشد.';

/** `null` when the file may be uploaded, otherwise the Persian reason it may not. */
export function validatePhotoFile(file: Pick<File, 'type' | 'size'>): string | null {
  if (!photoAcceptedTypes.some((type) => type === file.type)) return photoTypeMessage;
  if (file.size > photoMaxBytes) return photoSizeMessage;
  if (file.size === 0) return 'فایل عکس خالی است.';
  return null;
}

/**
 * Reads a file as a `data:` URL for the preview. The page CSP allows `img-src 'self' data:` (no
 * `blob:`), and the file never leaves the browser until the user saves.
 */
export function readPhotoPreview(file: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => (typeof reader.result === 'string' ? resolve(reader.result) : reject(new Error('preview')));
    reader.onerror = () => reject(reader.error ?? new Error('preview'));
    reader.readAsDataURL(file);
  });
}

/** Up to two initials for the default avatar (first letter of the first and last name). */
export function initialsOf(firstName: string, lastName: string): string {
  const first = [...firstName.trim()][0] ?? '';
  const last = [...lastName.trim()][0] ?? '';
  return `${first}${last}`.trim();
}
