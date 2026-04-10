using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.Subscription;

public class PayOsWebhookRequest
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public PayOsWebhookData Data { get; set; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";
}

public class PayOsWebhookData
{
    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; } = "";

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = "";

    [JsonPropertyName("transactionDateTime")]
    public string TransactionDateTime { get; set; } = "";

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";

    [JsonPropertyName("paymentLinkId")]
    public string PaymentLinkId { get; set; } = "";

    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = "";

    [JsonPropertyName("counterAccountBankId")]
    public string CounterAccountBankId { get; set; } = "";

    [JsonPropertyName("counterAccountBankName")]
    public string CounterAccountBankName { get; set; } = "";

    [JsonPropertyName("counterAccountName")]
    public string CounterAccountName { get; set; } = "";

    [JsonPropertyName("counterAccountNumber")]
    public string CounterAccountNumber { get; set; } = "";

    [JsonPropertyName("virtualAccountName")]
    public string VirtualAccountName { get; set; } = "";

    [JsonPropertyName("virtualAccountNumber")]
    public string VirtualAccountNumber { get; set; } = "";
}
