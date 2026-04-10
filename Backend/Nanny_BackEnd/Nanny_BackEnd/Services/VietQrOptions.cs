namespace Nanny_BackEnd.Services;

public class VietQrOptions
{
    public string ImageBaseUrl { get; set; } = "https://img.vietqr.io/image/";
    public string BankId { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Template { get; set; } = "compact2";
    public string SuccessUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public string WebhookToken { get; set; } = "";
    public int ExpiresAfterMinutes { get; set; } = 15;
}
