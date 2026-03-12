using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Repositories;

public class RefreshTokenRepository
{
    private readonly Sep490NannyDbContext _db;

    public RefreshTokenRepository(Sep490NannyDbContext db) => _db = db;

    public void Add(RefreshToken token) => _db.RefreshTokens.Add(token);

    public async Task<RefreshToken?> findByToken(string token) =>
        await _db.RefreshTokens.FirstOrDefaultAsync(rt =>
            rt.Token == token && !rt.IsDeleted);

    public async Task revokeAllForUser(Guid userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && !rt.IsDeleted)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }
    }

    public async Task saveChanges() => await _db.SaveChangesAsync();
}
