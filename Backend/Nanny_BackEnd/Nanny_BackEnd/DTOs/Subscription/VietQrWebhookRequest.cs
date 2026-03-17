using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.Subscription;

public class VietQrWebhookRequest
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("data")]
    public List<VietQrWebhookPaymentData> Data { get; set; } = [];
}

public class VietQrWebhookPaymentData
{
    [JsonPropertyName("orderCode")]
    public int OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("paymentLinkId")]
    public string? PaymentLinkId { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("transactionDatetime")]
    public string? TransactionDatetime { get; set; }
}
