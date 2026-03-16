using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscribeRequest
{
    [Required(ErrorMessage = "Gói subscription không được để trống.")]
    public Guid SubscriptionPlanId { get; set; }

    [StringLength(200, ErrorMessage = "Mã giao dịch cổng thanh toán tối đa 200 ký tự.")]
    public string? PaymentGatewayTransactionId { get; set; }
}
