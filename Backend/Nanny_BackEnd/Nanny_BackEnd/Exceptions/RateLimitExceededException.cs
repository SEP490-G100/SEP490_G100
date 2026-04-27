namespace Nanny_BackEnd.Exceptions;

public sealed class RateLimitExceededException : Exception
{
    public string Code { get; }
    public DateTime CooldownUntilUtc { get; }
    public int RetryAfterSeconds { get; }

    public RateLimitExceededException(
        string code,
        string message,
        DateTime cooldownUntilUtc)
        : base(message)
    {
        Code = code;
        CooldownUntilUtc = cooldownUntilUtc;
        var seconds = (int)Math.Ceiling((cooldownUntilUtc - DateTime.UtcNow).TotalSeconds);
        RetryAfterSeconds = Math.Max(1, seconds);
    }
}
