namespace Nanny_BackEnd.DTOs.Account;

public class AccountDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Gender { get; set; }          // 0=Other, 1=Male, 2=Female
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public int Status { get; set; }           // 0 = Inactive, 1 = Active
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class AccountListResponse
{
    public List<AccountDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class UpdateAccountStatusRequest
{
    public int Status { get; set; }   // 0 = Inactive, 1 = Active
}

/// <summary>Moderator dùng để cập nhật thông tin account</summary>
public class UpdateAccountRequest
{
    public int Status { get; set; }
    public string? PhoneNumber { get; set; }
}

/// <summary>Admin tạo tài khoản Moderator mới</summary>
public class CreateModeratorRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
}

/// <summary>Admin cập nhật thông tin Moderator</summary>
public class UpdateModeratorRequest
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public int Status { get; set; }
}
