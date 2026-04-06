namespace WebSite.Models.Search;

public class JobPostingReportFormModel
{
    public Guid JobPostingId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Evidence { get; set; }
}
