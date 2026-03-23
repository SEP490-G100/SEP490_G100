using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Nanny_BackEnd.Services;

public class VietQrService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly VietQrOptions _options;

    public VietQrService(IHttpClientFactory httpFactory, IOptions<VietQrOptions> options)
    {
        _http = httpFactory.CreateClient("VietQr");
        _options = options.Value;
    }

    public async Task<VietQrCreatePaymentResponse> createPayment(
        int orderCode,
        decimal amount,
        string description,
        string buyerName,
        string buyerEmail,
        string successUrl,
        string cancelUrl)
    {
        validateOptions();

        using var request = new HttpRequestMessage(HttpMethod.Post, "paymentRequests");
        request.Headers.Add("x-client-id", _options.ClientId);
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new VietQrCreatePaymentRequest
        {
            OrderCode = orderCode,
            Amount = (int)Math.Round(amount, MidpointRounding.AwayFromZero),
            Description = description,
            BuyerName = buyerName,
            BuyerEmail = buyerEmail,
            Template = _options.Template,
            CancelUrl = cancelUrl,
            SuccessUrl = successUrl
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<VietQrCreatePaymentResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Khong doc duoc phan hoi tu VietQR.");

        if (!response.IsSuccessStatusCode || !string.Equals(result.Code, "00", StringComparison.OrdinalIgnoreCase) || result.Data == null)
            throw new InvalidOperationException(result.Desc ?? "Khong the tao lien ket thanh toan VietQR.");

        return result;
    }

    public bool isWebhookAuthorized(string? secureToken)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookToken))
            return true;

        return string.Equals(_options.WebhookToken, secureToken, StringComparison.Ordinal);
    }

    public string buildSuccessUrl(Guid transactionId) => buildReturnUrl(_options.SuccessUrl, transactionId, false);

    public string buildCancelUrl(Guid transactionId) => buildReturnUrl(_options.CancelUrl, transactionId, true);

    private void validateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.SuccessUrl) ||
            string.IsNullOrWhiteSpace(_options.CancelUrl))
        {
            throw new InvalidOperationException("Vui long cau hinh day du VietQR: ClientId, ApiKey, SuccessUrl, CancelUrl.");
        }
    }

    private static string buildReturnUrl(string baseUrl, Guid transactionId, bool cancelled)
    {
        if (baseUrl.Contains("{transactionId}", StringComparison.OrdinalIgnoreCase))
            return baseUrl.Replace("{transactionId}", transactionId.ToString(), StringComparison.OrdinalIgnoreCase);

        var separator = baseUrl.Contains('?') ? '&' : '?';
        var url = $"{baseUrl}{separator}transactionId={transactionId}";
        if (cancelled && !baseUrl.Contains("cancelled=", StringComparison.OrdinalIgnoreCase))
            url += "&cancelled=true";

        return url;
    }

    private sealed class VietQrCreatePaymentRequest
    {
        public int OrderCode { get; set; }
        public int Amount { get; set; }
        public string Description { get; set; } = "";
        public string BuyerName { get; set; } = "";
        public string BuyerEmail { get; set; } = "";
        public string Template { get; set; } = "";
        public string CancelUrl { get; set; } = "";
        public string SuccessUrl { get; set; } = "";
    }
}

public class VietQrCreatePaymentResponse
{
    public string? Code { get; set; }
    public string? Desc { get; set; }
    public VietQrCreatePaymentData? Data { get; set; }
}

public class VietQrCreatePaymentData
{
    public string Id { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public int OrderCode { get; set; }
    public string Status { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
}
