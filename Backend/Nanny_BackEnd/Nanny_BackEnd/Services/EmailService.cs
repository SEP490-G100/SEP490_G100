using MailKit.Net.Smtp;
using MimeKit;

namespace Nanny_BackEnd.Services;

public class EmailService
{
    private readonly string _fromName;
    private readonly string _fromEmail;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUser;
    private readonly string _smtpPassword;

    public EmailService(IConfiguration config)
    {
        var s = config.GetSection("EmailSettings");
        _fromName     = s["FromName"] ?? "NannyMatch";
        _fromEmail    = s["FromEmail"]!;
        _smtpHost     = s["SmtpHost"]!;
        _smtpPort     = s.GetValue<int>("SmtpPort", 587);
        _smtpUser     = s["SmtpUser"]!;
        _smtpPassword = s["SmtpPassword"]!;
    }

    public async Task sendOtpEmail(string toEmail, string otpCode, string purpose)
    {
        var subject = purpose switch
        {
            "VerifyEmail"    => "Xác thực email - NannyMatch",
            "ForgotPassword" => "Đặt lại mật khẩu - NannyMatch",
            _                => "Mã OTP - NannyMatch"
        };

        var body = BuildOtpBody(subject, otpCode);
        await send(toEmail, subject, body);
    }

    private static string BuildOtpBody(string subject, string otpCode) => $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px">
            <h2 style="color:#f97316">{subject}</h2>
            <p>Mã xác thực của bạn là:</p>
            <div style="background:#f1f5f9;border-radius:8px;padding:16px;text-align:center;margin:16px 0">
                <span style="font-size:32px;font-weight:bold;letter-spacing:8px;color:#1e293b">{otpCode}</span>
            </div>
            <p style="color:#64748b;font-size:14px">Mã có hiệu lực trong 10 phút. Không chia sẻ mã này với bất kỳ ai.</p>
        </div>
        """;

    private async Task send(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_smtpHost, _smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(_smtpUser, _smtpPassword);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }
}
