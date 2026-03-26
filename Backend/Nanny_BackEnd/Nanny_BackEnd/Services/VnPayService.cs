using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Nanny_BackEnd.Services;

public class VnPayService
{
    private readonly VnPayOptions _options;

    public VnPayService(IOptions<VnPayOptions> options)
    {
        _options = options.Value;
    }

    public string createPaymentUrl(int orderCode, decimal amount, string orderInfo, string? clientIp)
    {
        validateOptions();

        var nowUtc = DateTime.UtcNow;
        var payload = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = _options.Command,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_Amount"] = ((long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = nowUtc.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = _options.CurrencyCode,
            ["vnp_IpAddr"] = normalizeIp(clientIp),
            ["vnp_Locale"] = _options.Locale,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = _options.OrderType,
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TxnRef"] = orderCode.ToString(CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = nowUtc.AddMinutes(15).ToString("yyyyMMddHHmmss")
        };

        var hashData = buildQueryString(payload);
        var secureHash = computeSignature(hashData);
        var query = $"{hashData}&vnp_SecureHash={urlEncode(secureHash)}";
        return $"{_options.PaymentUrl}?{query}";
    }

    public VnPayCallbackValidationResult validateCallback(IQueryCollection query)
    {
        var secureHash = query["vnp_SecureHash"].ToString();
        if (string.IsNullOrWhiteSpace(secureHash))
            return VnPayCallbackValidationResult.Invalid("Thieu chu ky VNPay.");

        var payload = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query)
        {
            if (!pair.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(pair.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pair.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = pair.Value.ToString();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            payload[pair.Key] = value;
        }

        if (payload.Count == 0)
            return VnPayCallbackValidationResult.Invalid("Khong co du lieu callback VNPay.");

        var hashData = buildQueryString(payload);
        var expectedHash = computeSignature(hashData);
        var valid = string.Equals(expectedHash, secureHash, StringComparison.OrdinalIgnoreCase);

        var amount = 0m;
        if (decimal.TryParse(query["vnp_Amount"], NumberStyles.Number, CultureInfo.InvariantCulture, out var rawAmount))
            amount = rawAmount / 100m;

        return new VnPayCallbackValidationResult
        {
            IsValidSignature = valid,
            Message = valid ? "" : "Chu ky callback VNPay khong hop le.",
            OrderCode = query["vnp_TxnRef"].ToString(),
            Amount = amount,
            ResponseCode = query["vnp_ResponseCode"].ToString(),
            TransactionStatus = query["vnp_TransactionStatus"].ToString(),
            TransactionNo = query["vnp_TransactionNo"].ToString(),
            BankTranNo = query["vnp_BankTranNo"].ToString()
        };
    }

    public string buildSuccessUrl(Guid transactionId) => buildReturnUrl(_options.SuccessUrl, transactionId, false);

    public string buildCancelUrl(Guid transactionId) => buildReturnUrl(_options.CancelUrl, transactionId, true);

    private void validateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TmnCode) ||
            string.IsNullOrWhiteSpace(_options.HashSecret) ||
            string.IsNullOrWhiteSpace(_options.PaymentUrl) ||
            string.IsNullOrWhiteSpace(_options.ReturnUrl) ||
            string.IsNullOrWhiteSpace(_options.SuccessUrl) ||
            string.IsNullOrWhiteSpace(_options.CancelUrl))
        {
            throw new InvalidOperationException(
                "Vui long cau hinh day du VNPay: TmnCode, HashSecret, PaymentUrl, ReturnUrl, SuccessUrl, CancelUrl.");
        }
    }

    private string computeSignature(string payload)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.HashSecret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static string buildQueryString(SortedDictionary<string, string> payload) =>
        string.Join("&", payload.Select(pair => $"{pair.Key}={urlEncode(pair.Value)}"));

    private static string urlEncode(string value) => WebUtility.UrlEncode(value) ?? "";

    private static string normalizeIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return "127.0.0.1";

        if (ip == "::1")
            return "127.0.0.1";

        return ip;
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
}

public class VnPayCallbackValidationResult
{
    public bool IsValidSignature { get; set; }
    public string Message { get; set; } = "";
    public string OrderCode { get; set; } = "";
    public decimal Amount { get; set; }
    public string ResponseCode { get; set; } = "";
    public string TransactionStatus { get; set; } = "";
    public string TransactionNo { get; set; } = "";
    public string BankTranNo { get; set; } = "";

    public bool IsSuccess =>
        string.Equals(ResponseCode, "00", StringComparison.OrdinalIgnoreCase) &&
        (string.IsNullOrWhiteSpace(TransactionStatus) ||
         string.Equals(TransactionStatus, "00", StringComparison.OrdinalIgnoreCase));

    public static VnPayCallbackValidationResult Invalid(string message) => new()
    {
        IsValidSignature = false,
        Message = message
    };
}
