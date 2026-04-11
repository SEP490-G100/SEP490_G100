namespace WebSite.Models.Search;

public class JobPostingComplainFormModel
{
    public Guid JobPostingId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ProfileComplainFormModel
{
    public Guid ComplainedUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string? ReturnUrl { get; set; }
}

public class MessageComplainFormModel
{
    public Guid MessageId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string? ReturnUrl { get; set; }
}


