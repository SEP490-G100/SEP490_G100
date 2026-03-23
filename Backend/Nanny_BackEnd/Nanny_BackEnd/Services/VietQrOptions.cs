namespace Nanny_BackEnd.Services;

public class VietQrOptions
{
    public string BaseUrl { get; set; } = "https://api.vietqr.io/v2/";
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Template { get; set; } = "compact";
    public string SuccessUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public string WebhookToken { get; set; } = "";
}
