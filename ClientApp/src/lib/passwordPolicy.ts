/**
 * The password length policy of new or changed passwords. It mirrors
 * `Constants.MinLength.UserPassword` / `Constants.MaxLength.UserPassword` on the server (D-078:
 * minimum 8). The server stays authoritative and enforces it again; this is only the fast,
 * field-level feedback. Keep the numbers in sync with the C# constants.
 */
export const passwordMinLength = 8;
export const passwordMaxLength = 128;

const fa = new Intl.NumberFormat('fa-IR', { useGrouping: false });

/** The message for a password shorter than the minimum. */
export const passwordMinMessage = `رمز عبور باید حداقل ${fa.format(passwordMinLength)} نویسه داشته باشد.`;

/** The hint shown under a password field. */
export const passwordMinHint = `حداقل ${fa.format(passwordMinLength)} نویسه`;
