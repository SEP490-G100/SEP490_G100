using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.FAQ;

public class CreateFaqRequest
{
    [JsonPropertyName("question")]
    [Required(ErrorMessage = "Question không được để trống.")]
    public string Question { get; set; } = null!;

    [JsonPropertyName("answer")]
    [Required(ErrorMessage = "Answer không được để trống.")]
    public string Answer { get; set; } = null!;

    [JsonPropertyName("category")]
    [Required(ErrorMessage = "Category không được để trống.")]
    public string Category { get; set; } = null!;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    // SortOrder is auto-assigned by service (max + 1), not provided by client
}

public class UpdateFaqRequest
{
    [JsonPropertyName("question")]
    [Required(ErrorMessage = "Question không được để trống.")]
    public string Question { get; set; } = null!;

    [JsonPropertyName("answer")]
    [Required(ErrorMessage = "Answer không được để trống.")]
    public string Answer { get; set; } = null!;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    // SortOrder is read-only, not updatable by client
}
