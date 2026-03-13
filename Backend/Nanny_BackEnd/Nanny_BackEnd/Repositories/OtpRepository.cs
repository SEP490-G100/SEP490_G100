using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class OtpRepository
{
    private readonly Sep490NannyDbContext _db;

    public OtpRepository(Sep490NannyDbContext db) => _db = db;

    public void Add(OtpCode otp) => _db.OtpCodes.Add(otp);

    public async Task<OtpCode?> FindActiveAsync(string email, OtpPurpose purpose) =>
        await _db.OtpCodes.FirstOrDefaultAsync(o =>
            o.Email == email &&
            o.Purpose == (int)purpose &&
            !o.IsUsed &&
            o.ExpiresAt > DateTime.UtcNow &&
            o.AttemptCount < AuthConstants.MaxOtpAttempts &&
            !o.IsDeleted);

    public async Task MarkPreviousAsUsedAsync(string email, OtpPurpose purpose)
    {
        var previous = await _db.OtpCodes
            .Where(o => o.Email == email && o.Purpose == (int)purpose && !o.IsUsed && !o.IsDeleted)
            .ToListAsync();

        foreach (var otp in previous)
        {
            otp.IsUsed = true;
            otp.UsedAt = DateTime.UtcNow;
        }
    }

    public async Task CleanupExpiredAsync()
    {
        var expired = await _db.OtpCodes
            .Where(o => o.ExpiresAt < DateTime.UtcNow && !o.IsDeleted)
            .ToListAsync();

        _db.OtpCodes.RemoveRange(expired);
    }

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
