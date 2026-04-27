namespace WebSite.Models.Account;

public class AccountDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Gender { get; set; }          // 0=Other, 1=Male, 2=Female
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? Ward { get; set; }
    public int Status { get; set; }           // 1 = Active, 2 = Inactive, 3 = Pending, 4 = Banned
    public bool EmailConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = new();

    // Convenience helpers
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string StatusLabel => Status == 1 ? "Active" : "Inactive";
    public string GenderLabel => Gender switch { 1 => "Male", 2 => "Female", _ => "Other / Not specified" };
    public string PrimaryRole => Roles.FirstOrDefault() ?? "User";
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

public class ViewAccountDetailRequest
{
    public int Status { get; set; }
    public string? PhoneNumber { get; set; }
}
