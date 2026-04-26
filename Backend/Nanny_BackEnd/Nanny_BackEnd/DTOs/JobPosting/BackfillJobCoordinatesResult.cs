namespace Nanny_BackEnd.DTOs.JobPosting;

public class BackfillJobCoordinatesResult
{
    public bool DryRun { get; set; }
    public int ScannedCount { get; set; }
    public int CandidateCount { get; set; }
    public int UpdatedCount { get; set; }
    public int GeocodedCount { get; set; }
    public int SwappedCount { get; set; }
    public int FailedCount { get; set; }
    public List<BackfillJobCoordinateItemResult> Items { get; set; } = [];
}

public class BackfillJobCoordinateItemResult
{
    public Guid JobId { get; set; }
    public string Title { get; set; } = "";
    public decimal? OldLatitude { get; set; }
    public decimal? OldLongitude { get; set; }
    public decimal? NewLatitude { get; set; }
    public decimal? NewLongitude { get; set; }
    public string Action { get; set; } = "";
    public string Message { get; set; } = "";
}

