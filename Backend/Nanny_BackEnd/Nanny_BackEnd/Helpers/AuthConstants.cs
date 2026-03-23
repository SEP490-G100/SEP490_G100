namespace Nanny_BackEnd.Helpers;

public enum AuthProvider
{
    Email = 0,
    Google = 1
}

public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Pending = 3,
    Banned = 4
}

public static class AuthConstants
{
    public const string DefaultRole = "Parent";
    public const int RefreshTokenDays = 7;
    public const int OtpExpirationMinutes = 10;
    public const int MaxOtpAttempts = 5;
}
