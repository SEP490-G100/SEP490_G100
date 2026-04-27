using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Subscription;

public class CreateSubscriptionPaymentRequest
{
    [Required(ErrorMessage = "Gói dịch vụ không được để trống.")]
    public Guid SubscriptionPlanId { get; set; }

    public string? ClientIp { get; set; }
}
