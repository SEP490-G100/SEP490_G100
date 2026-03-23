using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.JobPosting;

public class ModerateJobPostingRequest
{
    [Required(ErrorMessage = "Action is required.")]
    public int Action { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chu moderator toi da 500 ky tu.")]
    public string? Note { get; set; }
}
