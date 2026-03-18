using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

public class ModerateJobPostingRequest
{
    [StringLength(500, ErrorMessage = "Ghi chu moderator toi da 500 ky tu.")]
    public string? Note { get; set; }
}
