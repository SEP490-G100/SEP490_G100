namespace Nanny_BackEnd.DTOs.Profile;

using System.ComponentModel.DataAnnotations;

public class UpdatePersonalInfoRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = null!;
    [StringLength(20)]
    public string? PhoneNumber { get; set; }
    [StringLength(500)]
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Gender { get; set; }
    [StringLength(500)]
    public string? Address { get; set; }
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(100)]
    public string? District { get; set; }
    [StringLength(100)]
    public string? Ward { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Nanny-specific fields (optional, only applied for nanny role)
    [StringLength(2000)]
    public string? Bio { get; set; }
    [Range(0, 80)]
    public int? YearsOfExperience { get; set; }
    [Range(0, 10)]
    public int? EducationLevel { get; set; }
    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }
    [Range(0, 1000)]
    public int? MaxTravelDistance { get; set; }
    public List<Guid>? SkillIds { get; set; }
}
