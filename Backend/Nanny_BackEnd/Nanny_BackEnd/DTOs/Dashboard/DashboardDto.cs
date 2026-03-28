namespace Nanny_BackEnd.DTOs.Dashboard;

public class AdminDashboardStatsDto
{
    public UserStatsDto UserStats { get; set; } = null!;
    public RevenueStatsDto RevenueStats { get; set; } = null!;
    public SubscriptionStatsDto SubscriptionStats { get; set; } = null!;
    public PlatformHealthStatsDto PlatformHealth { get; set; } = null!;
    public ModerationQueueStatsDto ModerationQueue { get; set; } = null!;
    public ModerationResultsStatsDto ModerationResults { get; set; } = null!;
    public UserGrowthStatsDto UserGrowth { get; set; } = null!;
}


public class PlatformHealthStatsDto
{
    public int PendingVerifications { get; set; }
    public int ActiveJobPostings { get; set; }
    public int TotalContracts { get; set; }
}

public class ModerationQueueStatsDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public QueueRangeStatsDto Last7Days { get; set; } = new();
    public QueueRangeStatsDto Last30Days { get; set; } = new();
    public QueueRangeStatsDto Last12Months { get; set; } = new();
}

public class QueueRangeStatsDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public List<QueueTimelinePointDto> Timeline { get; set; } = new();
}

public class QueueTimelinePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
}

public class ModerationResultsStatsDto
{
    public List<DailyModerationResultDto> Last7Days { get; set; } = new();
    public List<DailyModerationResultDto> Last30Days { get; set; } = new();
    public List<DailyModerationResultDto> Last12Months { get; set; } = new();
}

public class DailyModerationResultDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int VerificationApproved { get; set; }
    public int VerificationRejected { get; set; }
    public int JobApproved { get; set; }
    public int JobRejected { get; set; }
}

public class UserGrowthStatsDto
{
    public List<DailyUserGrowthDto> Last7Days { get; set; } = new();
    public List<DailyUserGrowthDto> Last30Days { get; set; } = new();
    public List<DailyUserGrowthDto> Last12Months { get; set; } = new();
}

public class DailyUserGrowthDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int NewUsers { get; set; }
}


public class UserStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalParents { get; set; }
    public int TotalNannies { get; set; }
    public int TotalModerators { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int PendingUsers { get; set; }
    public int BannedUsers { get; set; }
}


public class RevenueStatsDto
{
    public decimal TotalRevenue { get; set; }
    public List<MonthlyRevenueDto> MonthlyRevenue { get; set; } = new();
    public List<RevenuePointDto> Last7Days { get; set; } = new();
    public List<RevenuePointDto> Last30Days { get; set; } = new();
    public List<RevenuePointDto> Last12Months { get; set; } = new();
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class MonthlyRevenueDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Revenue { get; set; }
}

public class RevenuePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
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


public class SubscriptionStatsDto
{
    public int TotalSubscriptions { get; set; }
    public int ActiveSubs { get; set; }
    public int ExpiredSubs { get; set; }
    public List<MonthlySubscriptionDto> MonthlySubs { get; set; } = new();
    public List<SubscriptionPointDto> Last7Days { get; set; } = new();
    public List<SubscriptionPointDto> Last30Days { get; set; } = new();
    public List<SubscriptionPointDto> Last12Months { get; set; } = new();
}

public class MonthlySubscriptionDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
}

public class SubscriptionPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}
