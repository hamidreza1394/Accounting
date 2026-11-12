namespace Server.Api;

/// <summary>
/// The shared user-creation request validation of the installation administrator
/// (`POST /admin/users`) and the company-scoped provisioning endpoint D-078
/// (`POST /companies/{companyId}/users`). One definition, so the two flows can never
/// drift apart — the company endpoint must not be a weaker bypass of the same policy.
/// </summary>
internal static class UserCreationValidation
{
    /// <summary>
    /// Validates the request with exactly the policy of the installation administrator's
    /// create endpoint. Returns the shared Persian error message, or <c>null</c> when the
    /// request is valid; the normalized fields are returned through the out parameters.
    /// </summary>
    internal static string? Validate(
        CreateUserRequest request,
        out string email,
        out string username,
        out string? firstName,
        out string? lastName,
        out string? cellPhone,
        out string? password)
    {
        email = Dtat.Utility.FixText(request.EmailAddress) ?? string.Empty;
        username = Dtat.Utility.FixText(request.Username) ?? string.Empty;
        firstName = Dtat.Utility.FixText(request.FirstName);
        lastName = Dtat.Utility.FixText(request.LastName);
        cellPhone = Dtat.Utility.FixText(request.CellPhoneNumber);
        password = request.Password;

        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > Constants.MaxLength.EmailAddress ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(username) ||
            username.Length < Constants.MinLength.Username ||
            username.Length > Constants.MaxLength.Username ||
            // The same username policy the Razor views enforce; the API must not be a
            // bypass (the column is varchar(20): ASCII letters, digits and . _ @ + - only).
            !new System.ComponentModel.DataAnnotations.RegularExpressionAttribute(Constants.RegularExpression.Username).IsValid(username) ||
            (firstName?.Length > Constants.MaxLength.FirstName) ||
            (lastName?.Length > Constants.MaxLength.LastName) ||
            (cellPhone is not null && (cellPhone.Length != Constants.FixedLength.CellPhoneNumber || !new System.ComponentModel.DataAnnotations.RegularExpressionAttribute(Constants.RegularExpression.CellPhoneNumber).IsValid(cellPhone))) ||
            string.IsNullOrWhiteSpace(password) ||
            password.Length < Constants.MinLength.UserPassword ||
            password.Length > Constants.MaxLength.UserPassword ||
            !new ViewModels.Validation.NotCommonPasswordAttribute().IsValid(password) ||
            !string.Equals(password, request.ConfirmPassword, StringComparison.Ordinal))
            return "اطلاعات کاربر یا سیاست رمز عبور معتبر نیست.";

        return null;
    }
}
