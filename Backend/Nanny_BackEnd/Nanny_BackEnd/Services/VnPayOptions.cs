namespace Nanny_BackEnd.Services;

public class VnPayOptions
{
    public string TmnCode { get; set; } = "";
    public string HashSecret { get; set; } = "";
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string ReturnUrl { get; set; } = "";
    public string IpnUrl { get; set; } = "";
    public string SuccessUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
    public string Locale { get; set; } = "vn";
    public string CurrencyCode { get; set; } = "VND";
    public string OrderType { get; set; } = "other";
    public string Command { get; set; } = "pay";
}
