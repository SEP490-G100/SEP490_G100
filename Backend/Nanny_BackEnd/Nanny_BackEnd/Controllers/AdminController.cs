using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly Sep490NannyDbContext _db;
    private static readonly string ModeratorRole = "Moderator";

    public AdminController(Sep490NannyDbContext db) => _db = db;

    // ────────────────────────────────────────────────
    // GET /api/admin/dashboard
    // ────────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var totalUsers      = await _db.Users.CountAsync(u => !u.IsDeleted);
        var totalParents    = await _db.Users.CountAsync(u => !u.IsDeleted &&
            u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == "Parent"));
        var totalNannies    = await _db.Users.CountAsync(u => !u.IsDeleted &&
            u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == "Nanny"));
        var totalModerators = await _db.Users.CountAsync(u => !u.IsDeleted &&
            u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == ModeratorRole));

        var totalRevenue      = await _db.Transactions
            .Where(t => !t.IsDeleted && t.Status == 1) // 1 = completed
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
        var totalTransactions = await _db.Transactions.CountAsync(t => !t.IsDeleted);

        var recentTransactions = await _db.Transactions
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .Select(t => new
            {
                t.Id,
                t.Amount,
                t.Status,
                t.Type,
                t.Description,
                t.CreatedAt,
                UserName = t.User.FirstName + " " + t.User.LastName,
                UserEmail = t.User.Email
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = new
            {
                totalUsers,
                totalParents,
                totalNannies,
                totalModerators,
                totalRevenue,
                totalTransactions,
                recentTransactions
            }
        });
    }

    // ────────────────────────────────────────────────
    // GET /api/admin/moderators?search=&page=1&pageSize=10
    // ────────────────────────────────────────────────
    [HttpGet("moderators")]
    public async Task<IActionResult> GetModerators(
        [FromQuery] string? search   = null,
        [FromQuery] int?    status   = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var query = _db.Users
            .Where(u => !u.IsDeleted &&
                u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == ModeratorRole))
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(s) ||
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AccountDto
            {
                Id             = u.Id,
                FirstName      = u.FirstName,
                LastName       = u.LastName,
                Email          = u.Email,
                PhoneNumber    = u.PhoneNumber,
                AvatarUrl      = u.AvatarUrl,
                Status         = u.Status,
                EmailConfirmed = u.EmailConfirmed,
                CreatedAt      = u.CreatedAt,
                LastLoginAt    = u.LastLoginAt,
                Roles          = u.UserRoles
                    .Where(ur => !ur.IsDeleted)
                    .Select(ur => ur.Role.Name)
                    .ToList()
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            data = new AccountListResponse
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            }
        });
    }

    // ────────────────────────────────────────────────
    // POST /api/admin/moderators  — Create moderator
    // ────────────────────────────────────────────────
    [HttpPost("moderators")]
    public async Task<IActionResult> CreateModerator([FromBody] CreateModeratorRequest request)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new { success = false, message = "Email không hợp lệ." });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { success = false, message = "Mật khẩu phải có ít nhất 8 ký tự." });
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            !System.Text.RegularExpressions.Regex.IsMatch(request.PhoneNumber.Trim(), @"^\d{10,11}$"))
            return BadRequest(new { success = false, message = "Số điện thoại phải là 10-11 chữ số." });

        // Check duplicate email
        if (await _db.Users.AnyAsync(u => u.Email == request.Email && !u.IsDeleted))
            return Conflict(new { success = false, message = "Email này đã được sử dụng." });

        // Find Moderator role
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == ModeratorRole);
        if (role == null)
            return StatusCode(500, new { success = false, message = "Role Moderator không tồn tại trong hệ thống." });

        // Hash password (BCrypt-style simple hash — use your project's actual hasher)
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var newUser = new User
        {
            Id             = Guid.NewGuid(),
            Email          = request.Email.Trim().ToLower(),
            PasswordHash   = passwordHash,
            FirstName      = request.FirstName.Trim(),
            LastName       = request.LastName.Trim(),
            PhoneNumber    = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Status         = 1, // Active
            EmailConfirmed = true,
            AuthProvider   = 0,
            CreatedAt      = DateTime.UtcNow,
            IsDeleted      = false
        };

        var userRole = new UserRole
        {
            UserId    = newUser.Id,
            RoleId    = role.Id,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.Users.Add(newUser);
        _db.UserRoles.Add(userRole);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Tạo tài khoản Moderator thành công.", data = new { newUser.Id } });
    }

    // ────────────────────────────────────────────────
    // PATCH /api/admin/moderators/{id}  — Edit moderator
    // ────────────────────────────────────────────────
    [HttpPatch("moderators/{id:guid}")]
    public async Task<IActionResult> UpdateModerator(Guid id, [FromBody] UpdateModeratorRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted &&
            u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == ModeratorRole));
        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy Moderator." });

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            !System.Text.RegularExpressions.Regex.IsMatch(request.PhoneNumber.Trim(), @"^\d{10,11}$"))
            return BadRequest(new { success = false, message = "Số điện thoại phải là 10-11 chữ số." });

        if (request.Status != 0 && request.Status != 1)
            return BadRequest(new { success = false, message = "Status không hợp lệ." });

        user.FirstName   = request.FirstName.Trim();
        user.LastName    = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.Status      = request.Status;
        user.UpdatedAt   = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new { success = true, message = "Cập nhật Moderator thành công." });
    }

    // ────────────────────────────────────────────────
    // DELETE /api/admin/moderators/{id}  — Soft delete
    // ────────────────────────────────────────────────
    [HttpDelete("moderators/{id:guid}")]
    public async Task<IActionResult> DeleteModerator(Guid id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted &&
            u.UserRoles.Any(ur => !ur.IsDeleted && ur.Role.Name == ModeratorRole));
        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy Moderator." });

        user.IsDeleted = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Đã xoá tài khoản Moderator." });
    }
}
