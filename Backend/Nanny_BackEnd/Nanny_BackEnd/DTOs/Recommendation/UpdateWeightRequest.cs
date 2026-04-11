using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Recommendation;

public class UpdateWeightRequest
{
    [Required]
    public string Key { get; set; } = null!;

    [Required]
    [Range(0.0, 1.0)]
    public double Value { get; set; }
}
