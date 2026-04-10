namespace WebSite.Models.Search;

public class JobPostingReportFormModel
{
    public Guid JobPostingId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ProfileReportFormModel
{
    public Guid ReportedUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string? ReturnUrl { get; set; }
}

public class MessageReportFormModel
{
    public Guid MessageId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public string? ReturnUrl { get; set; }
}
