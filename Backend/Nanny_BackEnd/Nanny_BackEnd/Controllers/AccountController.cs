using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Account;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize(Roles = "Moderator,Admin")]
public class AccountController : ControllerBase
{
    private readonly Sep490NannyDbContext _db;

    public AccountController(Sep490NannyDbContext db) => _db = db;

    /// <summary>
    /// GET /api/accounts?role=Nanny&status=0&search=lan&page=1&pageSize=10
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? role = null,
        [FromQuery] int? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        // Các role bị loại trừ khỏi danh sách quản lý (Moderator chỉ quản lý Nanny và Parent)
        var excludedRoles = new[] { "Moderator", "Admin" };

        var query = _db.Users
            .Where(u => !u.IsDeleted)
            .Where(u => u.UserRoles.Any(ur =>
                !ur.IsDeleted &&
                !excludedRoles.Contains(ur.Role.Name)))
            .AsQueryable();

        // Filter by status
        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

        // Filter by search (name or email)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.Email.ToLower().Contains(s) ||
                u.FirstName.ToLower().Contains(s) ||
                u.LastName.ToLower().Contains(s));
        }

        // Filter by role — chỉ cho phép Parent hoặc Nanny
        var allowedRoles = new[] { "Parent", "Nanny" };
        if (!string.IsNullOrWhiteSpace(role) && allowedRoles.Contains(role))
        {
            query = query.Where(u =>
                u.UserRoles.Any(ur =>
                    !ur.IsDeleted &&
                    ur.Role.Name == role));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AccountDto
            {
                Id            = u.Id,
                FirstName     = u.FirstName,
                LastName      = u.LastName,
                Email         = u.Email,
                PhoneNumber   = u.PhoneNumber,
                AvatarUrl     = u.AvatarUrl,
                City          = u.City,
                Status        = u.Status,
                EmailConfirmed = u.EmailConfirmed,
                CreatedAt     = u.CreatedAt,
                LastLoginAt   = u.LastLoginAt,
                Roles         = u.UserRoles
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
                Items      = users,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            }
        });
    }

    /// <summary>
    /// GET /api/accounts/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var user = await _db.Users
            .Where(u => u.Id == id && !u.IsDeleted)
            .Select(u => new AccountDto
            {
                Id            = u.Id,
                FirstName     = u.FirstName,
                LastName      = u.LastName,
                Email         = u.Email,
                PhoneNumber   = u.PhoneNumber,
                AvatarUrl     = u.AvatarUrl,
                City          = u.City,
                Status        = u.Status,
                EmailConfirmed = u.EmailConfirmed,
                CreatedAt     = u.CreatedAt,
                LastLoginAt   = u.LastLoginAt,
                Roles         = u.UserRoles
                    .Where(ur => !ur.IsDeleted)
                    .Select(ur => ur.Role.Name)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

        return Ok(new { success = true, data = user });
    }

    /// <summary>
    /// PATCH /api/accounts/{id}/status
    /// Body: { "status": 0 } = Active, { "status": 1 } = Inactive
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAccountStatusRequest request)
    {
        if (request.Status != 0 && request.Status != 1)
            return BadRequest(new { success = false, message = "Status không hợp lệ. Chỉ chấp nhận 0 (Active) hoặc 1 (Inactive)." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        if (user == null)
            return NotFound(new { success = false, message = "Không tìm thấy tài khoản." });

        user.Status    = request.Status;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = request.Status == 0 ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản.",
            data    = new { user.Id, user.Status }
        });
    }
}
