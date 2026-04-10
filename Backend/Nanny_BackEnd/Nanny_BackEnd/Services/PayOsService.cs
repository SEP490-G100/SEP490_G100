using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nanny_BackEnd.DTOs.Subscription;

namespace Nanny_BackEnd.Services;

public class PayOsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly PayOsOptions _options;

    public PayOsService(IHttpClientFactory httpFactory, IOptions<PayOsOptions> options)
    {
        _http = httpFactory.CreateClient("PayOs");
        _options = options.Value;
    }

    public bool isConfigured() =>
        !string.IsNullOrWhiteSpace(_options.ClientId) &&
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.ChecksumKey);

    public async Task<PayOsPaymentInstruction> createPaymentInstruction(
        Guid transactionId,
        int orderCode,
        decimal amount,
        string planName)
    {
        validateOptions();

        var roundedAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        var normalizedAmount = (int)roundedAmount;
        var description = buildTransferDescription(orderCode);
        var returnUrl = buildSuccessUrl(transactionId);
        var cancelUrl = buildCancelUrl(transactionId);
        var expiredAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.ExpiresAfterMinutes)).ToUnixTimeSeconds();

        var payload = new PayOsCreatePaymentRequest
        {
            OrderCode = orderCode,
            Amount = normalizedAmount,
            Description = description,
            CancelUrl = cancelUrl,
            ReturnUrl = returnUrl,
            ExpiredAt = expiredAt,
            Items =
            [
                new PayOsPaymentItem
                {
                    Name = $"Subscription {planName}",
                    Quantity = 1,
                    Price = normalizedAmount,
                    Unit = "goi"
                }
            ]
        };
        payload.Signature = signCreatePaymentPayload(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/payment-requests");
        request.Headers.Add("x-client-id", _options.ClientId);
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(request);
        var result = await deserializeResponse<PayOsPaymentResponse>(response);
        if (!response.IsSuccessStatusCode || result?.Data == null || !string.Equals(result.Code, "00", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(result?.Desc ?? "Khong tao duoc link thanh toan PayOS.");

        return mapInstruction(result.Data, DateTimeOffset.FromUnixTimeSeconds(expiredAt).UtcDateTime);
    }

    public async Task<PayOsPaymentInstruction?> getPaymentInstruction(int orderCode)
    {
        validateOptions();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"v2/payment-requests/{orderCode}");
        request.Headers.Add("x-client-id", _options.ClientId);
        request.Headers.Add("x-api-key", _options.ApiKey);

        using var response = await _http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        var result = await deserializeResponse<PayOsPaymentLookupResponse>(response);
        if (!response.IsSuccessStatusCode || result?.Data == null || !string.Equals(result.Code, "00", StringComparison.OrdinalIgnoreCase))
            return null;

        var data = result.Data;
        var checkoutUrl = !string.IsNullOrWhiteSpace(data.CheckoutUrl)
            ? data.CheckoutUrl
            : !string.IsNullOrWhiteSpace(data.Id)
                ? $"https://pay.payos.vn/web/{data.Id}"
                : "";

        return new PayOsPaymentInstruction
        {
            OrderCode = data.OrderCode,
            Amount = data.Amount,
            TransferContent = buildTransferDescription(data.OrderCode),
            QrCodeUrl = buildCheckoutQrUrl(checkoutUrl),
            CheckoutUrl = checkoutUrl,
            BankId = "",
            AccountNumber = "",
            AccountName = "",
            PaymentLinkId = data.Id ?? "",
            Status = data.Status ?? "",
            ExpiresAt = data.CreatedAt?.AddMinutes(Math.Max(1, _options.ExpiresAfterMinutes)) ?? DateTime.UtcNow.AddMinutes(Math.Max(1, _options.ExpiresAfterMinutes))
        };
    }

    public async Task<PayOsPaymentStatusResult?> getPaymentStatus(int orderCode)
    {
        validateOptions();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"v2/payment-requests/{orderCode}");
        request.Headers.Add("x-client-id", _options.ClientId);
        request.Headers.Add("x-api-key", _options.ApiKey);

        using var response = await _http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        var result = await deserializeResponse<PayOsPaymentLookupResponse>(response);
        if (!response.IsSuccessStatusCode || result?.Data == null || !string.Equals(result.Code, "00", StringComparison.OrdinalIgnoreCase))
            return null;

        return new PayOsPaymentStatusResult
        {
            OrderCode = result.Data.OrderCode,
            Amount = result.Data.Amount,
            Status = result.Data.Status ?? "",
            PaymentLinkId = result.Data.Id ?? ""
        };
    }

    public bool isWebhookValid(PayOsWebhookRequest request)
    {
        if (!isConfigured() || string.IsNullOrWhiteSpace(request.Signature))
            return false;

        var data = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["accountNumber"] = request.Data.AccountNumber ?? "",
            ["amount"] = normalizeValue(request.Data.Amount),
            ["code"] = request.Data.Code ?? "",
            ["counterAccountBankId"] = request.Data.CounterAccountBankId ?? "",
            ["counterAccountBankName"] = request.Data.CounterAccountBankName ?? "",
            ["counterAccountName"] = request.Data.CounterAccountName ?? "",
            ["counterAccountNumber"] = request.Data.CounterAccountNumber ?? "",
            ["currency"] = request.Data.Currency ?? "",
            ["desc"] = request.Data.Desc ?? "",
            ["description"] = request.Data.Description ?? "",
            ["orderCode"] = request.Data.OrderCode.ToString(CultureInfo.InvariantCulture),
            ["paymentLinkId"] = request.Data.PaymentLinkId ?? "",
            ["reference"] = request.Data.Reference ?? "",
            ["transactionDateTime"] = request.Data.TransactionDateTime ?? "",
            ["virtualAccountName"] = request.Data.VirtualAccountName ?? "",
            ["virtualAccountNumber"] = request.Data.VirtualAccountNumber ?? ""
        };

        var payload = string.Join("&", data.Select(pair => $"{pair.Key}={pair.Value}"));
        var signature = computeSignature(payload);
        return string.Equals(signature, request.Signature, StringComparison.OrdinalIgnoreCase);
    }

    public string buildSuccessUrl(Guid transactionId) => buildReturnUrl(_options.SuccessUrl, transactionId, false);

    public string buildCancelUrl(Guid transactionId) => buildReturnUrl(_options.CancelUrl, transactionId, true);

    public string buildCheckoutQrUrl(string? checkoutUrl)
    {
        if (string.IsNullOrWhiteSpace(checkoutUrl))
            return "";

        return $"https://api.qrserver.com/v1/create-qr-code/?size=320x320&data={Uri.EscapeDataString(checkoutUrl)}";
    }

    private string signCreatePaymentPayload(PayOsCreatePaymentRequest payload)
    {
        var data = string.Join("&",
            $"amount={payload.Amount}",
            $"cancelUrl={payload.CancelUrl}",
            $"description={payload.Description}",
            $"orderCode={payload.OrderCode}",
            $"returnUrl={payload.ReturnUrl}");

        return computeSignature(data);
    }

    private string computeSignature(string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ChecksumKey));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void validateOptions()
    {
        if (!isConfigured() ||
            string.IsNullOrWhiteSpace(_options.SuccessUrl) ||
            string.IsNullOrWhiteSpace(_options.CancelUrl))
        {
            throw new InvalidOperationException(
                "Vui long cau hinh day du PayOS: ClientId, ApiKey, ChecksumKey, SuccessUrl, CancelUrl.");
        }
    }

    private PayOsPaymentInstruction mapInstruction(PayOsPaymentResponseData data, DateTime expiresAtUtc)
    {
        var qrCodeUrl = buildCheckoutQrUrl(data.CheckoutUrl);

        return new PayOsPaymentInstruction
        {
            OrderCode = data.OrderCode,
            Amount = data.Amount,
            TransferContent = data.Description ?? "",
            QrCodeUrl = qrCodeUrl,
            CheckoutUrl = data.CheckoutUrl ?? "",
            BankId = data.Bin ?? "",
            AccountNumber = data.AccountNumber ?? "",
            AccountName = data.AccountName ?? "",
            PaymentLinkId = data.PaymentLinkId ?? "",
            Status = data.Status ?? "",
            ExpiresAt = expiresAtUtc
        };
    }

    private static string buildTransferDescription(int orderCode) => $"NM{orderCode}";

    private static string buildReturnUrl(string baseUrl, Guid transactionId, bool cancelled)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";

        if (baseUrl.Contains("{transactionId}", StringComparison.OrdinalIgnoreCase))
            return baseUrl.Replace("{transactionId}", transactionId.ToString(), StringComparison.OrdinalIgnoreCase);

        var separator = baseUrl.Contains('?') ? '&' : '?';
        var url = $"{baseUrl}{separator}transactionId={transactionId}";
        if (cancelled && !baseUrl.Contains("cancelled=", StringComparison.OrdinalIgnoreCase))
            url += "&cancelled=true";

        return url;
    }

    private static async Task<T?> deserializeResponse<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static string normalizeValue(decimal value) =>
        decimal.Round(value, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    private sealed class PayOsCreatePaymentRequest
    {
        public int OrderCode { get; set; }
        public int Amount { get; set; }
        public string Description { get; set; } = "";
        public string CancelUrl { get; set; } = "";
        public string ReturnUrl { get; set; } = "";
        public long ExpiredAt { get; set; }
        public List<PayOsPaymentItem> Items { get; set; } = [];
        public string Signature { get; set; } = "";
    }

    private sealed class PayOsPaymentItem
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public int Price { get; set; }
        public string Unit { get; set; } = "";
    }

    private class PayOsApiResponse<T>
    {
        public string Code { get; set; } = "";
        public string Desc { get; set; } = "";
        public T? Data { get; set; }
        public string Signature { get; set; } = "";
    }

    private class PayOsPaymentResponse : PayOsApiResponse<PayOsPaymentResponseData>;

    private sealed class PayOsPaymentResponseData
    {
        public string? Bin { get; set; }
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public int OrderCode { get; set; }
        public string? Currency { get; set; }
        public string? PaymentLinkId { get; set; }
        public string? Status { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? QrCode { get; set; }
    }

    private class PayOsPaymentLookupResponse : PayOsApiResponse<PayOsPaymentLookupData>;

    private sealed class PayOsPaymentLookupData
    {
        public string? Id { get; set; }
        public int OrderCode { get; set; }
        public decimal Amount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal AmountRemaining { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? CheckoutUrl { get; set; }
    }
}

public class PayOsPaymentInstruction
{
    public int OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = "";
    public string QrCodeUrl { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
    public string BankId { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string PaymentLinkId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}

public class PayOsPaymentStatusResult
{
    public int OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string PaymentLinkId { get; set; } = "";
    public bool IsPaid => string.Equals(Status, "PAID", StringComparison.OrdinalIgnoreCase);
    public bool IsCancelled => string.Equals(Status, "CANCELLED", StringComparison.OrdinalIgnoreCase);
}
