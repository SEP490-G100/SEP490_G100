using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Subscription;

public class CreateSubscriptionPaymentRequest
{
    [Required(ErrorMessage = "Goi subscription khong duoc de trong.")]
    public Guid SubscriptionPlanId { get; set; }
}
