using System.Text.Json.Serialization;

namespace Nanny_BackEnd.DTOs.Subscription;

public class CassoWebhookRequest
{
    [JsonPropertyName("error")]
    public int Error { get; set; }

    [JsonPropertyName("data")]
    public List<CassoWebhookTransaction> Data { get; set; } = [];
}

public class CassoWebhookTransaction
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tid")]
    public string? Tid { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("when")]
    public string? When { get; set; }

    [JsonPropertyName("bank_sub_acc_id")]
    public string? BankSubAccountId { get; set; }

    [JsonPropertyName("subAccId")]
    public string? SubAccountId { get; set; }

    [JsonPropertyName("bankName")]
    public string? BankName { get; set; }

    [JsonPropertyName("bankAbbreviation")]
    public string? BankAbbreviation { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("transactionDateTime")]
    public string? TransactionDateTime { get; set; }
}

public class MarkSubscriptionTransferredResponse
{
    public Guid TransactionId { get; set; }
    public int TransactionStatus { get; set; }
    public string TransactionStatusLabel { get; set; } = "";
    public string Message { get; set; } = "";
}
