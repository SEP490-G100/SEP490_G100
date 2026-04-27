namespace Nanny_BackEnd.DTOs.Recommendation;

public sealed class NanniesForJobGatingResult
{
    public bool IsAllowed { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class JobsForNannyGatingResult
{
    public bool IsAllowed { get; init; }
    public int HttpStatus { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? NannyProfileId { get; init; }
}
