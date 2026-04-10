using System.Globalization;
using Microsoft.Extensions.Options;

namespace Nanny_BackEnd.Services;

public class VietQrService
{
    private readonly VietQrOptions _options;

    public VietQrService(IOptions<VietQrOptions> options)
    {
        _options = options.Value;
    }

    public VietQrPaymentInstruction createPaymentInstruction(int orderCode, decimal amount, string transferContent)
    {
        validateOptions();

        var roundedAmount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(1, _options.ExpiresAfterMinutes));
        var qrCodeUrl = buildQrCodeUrl(roundedAmount, transferContent);

        return new VietQrPaymentInstruction
        {
            OrderCode = orderCode,
            Amount = roundedAmount,
            TransferContent = transferContent,
            QrCodeUrl = qrCodeUrl,
            CheckoutUrl = qrCodeUrl,
            BankId = _options.BankId,
            AccountNumber = _options.AccountNumber,
            AccountName = _options.AccountName,
            ExpiresAt = expiresAt
        };
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
        if (string.IsNullOrWhiteSpace(_options.BankId) ||
            string.IsNullOrWhiteSpace(_options.AccountNumber) ||
            string.IsNullOrWhiteSpace(_options.AccountName))
        {
            throw new InvalidOperationException(
                "Vui long cau hinh day du VietQR: BankId, AccountNumber, AccountName.");
        }
    }

    private string buildQrCodeUrl(decimal amount, string transferContent)
    {
        var normalizedBaseUrl = _options.ImageBaseUrl.TrimEnd('/');
        var template = string.IsNullOrWhiteSpace(_options.Template) ? "compact2" : _options.Template.Trim();
        var encodedAmount = Uri.EscapeDataString(amount.ToString("0", CultureInfo.InvariantCulture));
        var encodedContent = Uri.EscapeDataString(transferContent);
        var encodedAccountName = Uri.EscapeDataString(_options.AccountName);

        return
            $"{normalizedBaseUrl}/{_options.BankId}-{_options.AccountNumber}-{template}.png?amount={encodedAmount}&addInfo={encodedContent}&accountName={encodedAccountName}";
    }

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
}

public class VietQrPaymentInstruction
{
    public int OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string TransferContent { get; set; } = "";
    public string QrCodeUrl { get; set; } = "";
    public string CheckoutUrl { get; set; } = "";
    public string BankId { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
