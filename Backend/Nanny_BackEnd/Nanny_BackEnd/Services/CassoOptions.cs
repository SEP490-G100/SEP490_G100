namespace Nanny_BackEnd.Services;

public class CassoOptions
{
    public string BaseUrl { get; set; } = "https://oauth.casso.vn/v2/";
    public string ApiKey { get; set; } = "";
    public string WebhookSecureToken { get; set; } = "";
    public string BankAccountId { get; set; } = "";
}
