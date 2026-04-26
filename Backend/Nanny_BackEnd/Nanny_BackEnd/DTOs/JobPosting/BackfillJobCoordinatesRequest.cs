using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

public class BackfillJobCoordinatesRequest
{
    public bool DryRun { get; set; } = true;

    [Range(1, 1000, ErrorMessage = "MaxItems phải trong khoảng 1-1000.")]
    public int MaxItems { get; set; } = 200;

    [Range(0, 5000, ErrorMessage = "DelayMs phải trong khoảng 0-5000.")]
    public int DelayMs { get; set; } = 1100;

    public DateTime? CreatedBeforeUtc { get; set; }

    public bool ForceGeocode { get; set; } = false;
}
