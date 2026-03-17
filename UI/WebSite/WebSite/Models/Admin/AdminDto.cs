namespace WebSite.Models.Admin;


public class AdminDashboardDto
{
    // User stats
    public int     TotalUsers        { get; set; }
    public int     TotalParents      { get; set; }
    public int     TotalNannies      { get; set; }
    public int     TotalModerators   { get; set; }

    // Revenue stats
    public decimal TotalRevenue      { get; set; }
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();

    // Subscription stats
    public int     TotalSubscriptions { get; set; }
    public int     ActiveSubs         { get; set; }
    public int     ExpiredSubs        { get; set; }
}

public class RecentTransactionDto
{
    public Guid     Id          { get; set; }
    public decimal  Amount      { get; set; }
    public int      Status      { get; set; }
    public int      Type        { get; set; }
    public string?  Description { get; set; }
    public DateTime CreatedAt   { get; set; }
    public string?  UserName    { get; set; }
    public string?  UserEmail   { get; set; }

    public string StatusLabel => Status switch { 1 => "Completed", 0 => "Pending", _ => "Failed" };
    public string TypeLabel   => Type   switch { 1 => "Subscription", 2 => "Refund", _ => "Payment" };
}

// ── Nested API response DTOs (match backend DashboardStatsDto) ────────────

public class ApiDashboardStatsDto
{
    public ApiUserStatsDto?         UserStats         { get; set; }
    public ApiRevenueStatsDto?      RevenueStats      { get; set; }
    public ApiSubscriptionStatsDto? SubscriptionStats { get; set; }

    /// <summary>Flatten into the view model.</summary>
    public AdminDashboardDto ToViewModel() => new()
    {
        TotalUsers         = UserStats?.TotalUsers        ?? 0,
        TotalParents       = UserStats?.TotalParents      ?? 0,
        TotalNannies       = UserStats?.TotalNannies      ?? 0,
        TotalModerators    = UserStats?.TotalModerators   ?? 0,
        TotalRevenue       = RevenueStats?.TotalRevenue   ?? 0,
        RecentTransactions = RevenueStats?.RecentTransactions ?? new(),
        TotalSubscriptions = SubscriptionStats?.TotalSubscriptions ?? 0,
        ActiveSubs         = SubscriptionStats?.ActiveSubs        ?? 0,
        ExpiredSubs        = SubscriptionStats?.ExpiredSubs        ?? 0,
    };
}

public class ApiUserStatsDto
{
    public int TotalUsers      { get; set; }
    public int TotalParents    { get; set; }
    public int TotalNannies    { get; set; }
    public int TotalModerators { get; set; }
}

public class ApiRevenueStatsDto
{
    public decimal TotalRevenue { get; set; }
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class ApiSubscriptionStatsDto
{
    public int TotalSubscriptions { get; set; }
    public int ActiveSubs         { get; set; }
    public int ExpiredSubs        { get; set; }
}

// ── Moderator management DTOs ─────────────────────────────────────────────

public class CreateModeratorRequest
{
    public string  Email       { get; set; } = "";
    public string  Password    { get; set; } = "";
    public string  FirstName   { get; set; } = "";
    public string  LastName    { get; set; } = "";
    public string? PhoneNumber { get; set; }
}

public class UpdateModeratorRequest
{
    public string  FirstName   { get; set; } = "";
    public string  LastName    { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public int     Status      { get; set; }
}
