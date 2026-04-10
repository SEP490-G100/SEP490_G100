namespace Nanny_BackEnd.Services;

public class PayOsOptions
{
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn/";
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string SuccessUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public int ExpiresAfterMinutes { get; set; } = 15;
}
