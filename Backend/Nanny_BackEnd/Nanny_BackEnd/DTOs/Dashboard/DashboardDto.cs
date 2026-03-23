namespace Nanny_BackEnd.DTOs.Dashboard;

public class DashboardStatsDto
{
    public UserStatsDto UserStats { get; set; } = null!;
    public RevenueStatsDto RevenueStats { get; set; } = null!;
    public SubscriptionStatsDto SubscriptionStats { get; set; } = null!;
    public PlatformHealthStatsDto PlatformHealth { get; set; } = null!;
}

// ── Platform Health Stats ───────────────────────────────────────────────────

public class PlatformHealthStatsDto
{
    public int PendingVerifications { get; set; }
    public int ActiveJobPostings { get; set; }
    public int TotalContracts { get; set; }
}

// ── User Stats ──────────────────────────────────────────────────────────────

public class UserStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalParents { get; set; }
    public int TotalNannies { get; set; }
    public int TotalModerators { get; set; }
}

// ── Revenue Stats ───────────────────────────────────────────────────────────

public class RevenueStatsDto
{
    public decimal TotalRevenue { get; set; }
    public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class MonthlyRevenueDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}

public class RecentTransactionDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public int Status { get; set; }
    public int Type { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
}

// ── Subscription Stats ──────────────────────────────────────────────────────

public class SubscriptionStatsDto
{
    public int TotalSubscriptions { get; set; }
    public int ActiveSubs { get; set; }
    public int ExpiredSubs { get; set; }
    public List<MonthlySubscriptionDto> MonthlySubs { get; set; } = new();
}

public class MonthlySubscriptionDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
}
