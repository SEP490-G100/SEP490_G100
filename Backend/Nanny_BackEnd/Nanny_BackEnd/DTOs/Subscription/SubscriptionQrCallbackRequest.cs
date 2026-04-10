using System.ComponentModel.DataAnnotations;

namespace Nanny_BackEnd.DTOs.Subscription;

public class SubscriptionQrCallbackRequest
{
    [Required(ErrorMessage = "OrderCode khong duoc de trong.")]
    [Range(1, int.MaxValue, ErrorMessage = "OrderCode khong hop le.")]
    public int OrderCode { get; set; }

    [Range(typeof(decimal), "1", "999999999999", ErrorMessage = "So tien callback phai lon hon 0.")]
    public decimal Amount { get; set; }

    public bool IsSuccess { get; set; } = true;
    public string? TransferContent { get; set; }
    public string? ProviderTransactionId { get; set; }
}

public class SubscriptionQrCallbackResponse
{
    public bool Processed { get; set; }
    public bool SubscriptionActivated { get; set; }
    public Guid? TransactionId { get; set; }
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}
