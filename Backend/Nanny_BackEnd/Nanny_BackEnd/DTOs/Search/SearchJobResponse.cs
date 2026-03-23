using Nanny_BackEnd.DTOs.JobPosting;

namespace Nanny_BackEnd.DTOs.Search;

public class SearchJobResponse
{
    public Guid Id { get; set; }
    public Guid? ParentProfileId { get; set; }
    public Guid? ChildProfileId { get; set; }
    public bool IsOwner { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ParentName { get; set; } = "";
    public int JobType { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public bool SalaryNegotiable { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Location { get; set; }
    public string? Characteristic { get; set; }
    public int? BirthType { get; set; }
    public string? BirthTypeLabel { get; set; }
    public string? SpecialNeeds { get; set; }
    public int? MinNannyAge { get; set; }
    public int? MaxNannyAge { get; set; }
    public List<string> Skills { get; set; } = [];
    public List<JobScheduleSlotResponse> ScheduleSlots { get; set; } = [];
    public int? NumberOfChildren { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int Status { get; set; }
    public int ModerationStatus { get; set; }
    public string? ModerationNote { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public double? DistanceKm { get; set; }
    public string? SubscriptionPlanCode { get; set; }
    public bool FeaturedBadge { get; set; }
    public bool SearchPriority { get; set; }
}
