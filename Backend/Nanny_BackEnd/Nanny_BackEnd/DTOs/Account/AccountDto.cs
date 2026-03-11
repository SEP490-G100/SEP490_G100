namespace Nanny_BackEnd.DTOs.Account;

public class AccountDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? City { get; set; }
    public int Status { get; set; }          // 0 = Active, 1 = Inactive
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
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
    public int Status { get; set; }   // 0 = Active, 1 = Inactive
}
