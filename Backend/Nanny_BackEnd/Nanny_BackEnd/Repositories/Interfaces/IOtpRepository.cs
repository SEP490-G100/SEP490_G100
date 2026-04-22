using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories.Interfaces;

public interface IOtpRepository
{
    void Add(OtpCode otp);
    Task<OtpCode?> FindActiveAsync(string email, OtpPurpose purpose);
    Task MarkPreviousAsUsedAsync(string email, OtpPurpose purpose);
    Task CleanupExpiredAsync();
    Task SaveChangesAsync();
}
