namespace Constants;

public static class MinLength : object
{
        static MinLength()
        {
        }

        public const int CellPhoneNumberVerificationKey = 6;

        /// <summary>
        /// Minimum length of a user credential password, set when a password is
        /// created or changed (not at login). D-073 §5: minimum 15 characters for
        /// every new or changed password; existing accounts are never force-rotated.
        /// </summary>
        public const int UserPassword = 15;

        public const int Username = 3;
}
