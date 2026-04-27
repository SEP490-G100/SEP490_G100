using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.FAQ;

public class CreateFaqRequest
{
    [JsonPropertyName("question")]
    [Required(ErrorMessage = "Câu hỏi không được để trống.")]
    public string Question { get; set; } = null!;

    [JsonPropertyName("answer")]
    [Required(ErrorMessage = "Câu trả lời không được để trống.")]
    public string Answer { get; set; } = null!;

    [JsonPropertyName("category")]
    [Required(ErrorMessage = "Danh mục không được để trống.")]
    public string Category { get; set; } = null!;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
    // SortOrder is auto-assigned by service (max + 1), not provided by client
}

public class UpdateFaqRequest
{
    [JsonPropertyName("question")]
    [Required(ErrorMessage = "Câu hỏi không được để trống.")]
    public string Question { get; set; } = null!;

    [JsonPropertyName("answer")]
    [Required(ErrorMessage = "Câu trả lời không được để trống.")]
    public string Answer { get; set; } = null!;

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;
    // SortOrder is read-only, not updatable by client
}
