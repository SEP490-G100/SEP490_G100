namespace WebSite.Models.Account;

public class AccountDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? City { get; set; }
    public int Status { get; set; }           // 0 = Active, 1 = Inactive
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = new();

    // Convenience helpers
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string StatusLabel => Status == 0 ? "Hoạt động" : "Vô hiệu";
    public string PrimaryRole => Roles.FirstOrDefault() ?? "User";
    public string RoleLabel => PrimaryRole switch
    {
        "Parent"    => "Phụ huynh",
        "Nanny"     => "Bảo mẫu",
        "Moderator" => "Moderator",
        "Admin"     => "Admin",
        _           => PrimaryRole
    };
    public string Avatar => FullName.Length > 0 ? FullName[0].ToString().ToUpper() : "?";
}

public class AccountListResponse
{
    public List<AccountDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
