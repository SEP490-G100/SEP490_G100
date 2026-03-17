using System.Security.Cryptography;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class OtpService
{
    private readonly OtpRepository _otpRepo;

    public OtpService(OtpRepository otpRepo) => _otpRepo = otpRepo;

    public async Task<string> GenerateAsync(string email, OtpPurpose purpose, Guid? userId = null)
    {
        await _otpRepo.MarkPreviousAsUsedAsync(email, purpose);

        var code = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var otp = new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            Code = code,
            Purpose = (int)purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(AuthConstants.OtpExpirationMinutes),
            CreatedAt = DateTime.UtcNow
        };

        _otpRepo.Add(otp);
        await _otpRepo.SaveChangesAsync();
        return code;
    }

    public async Task<OtpCode?> ValidateAsync(string email, string code, OtpPurpose purpose)
    {
        var otp = await _otpRepo.FindActiveAsync(email, purpose);
        if (otp == null) return null;

        otp.AttemptCount++;

        if (otp.Code != code)
        {
            await _otpRepo.SaveChangesAsync();
            return null;
        }

        otp.IsUsed = true;
        otp.UsedAt = DateTime.UtcNow;
        await _otpRepo.SaveChangesAsync();

        return otp;
    }
}
