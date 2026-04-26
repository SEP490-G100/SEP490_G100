using System.ComponentModel.DataAnnotations;

namespace WebSite.Models.Admin;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalParents { get; set; }
    public int TotalNannies { get; set; }
    public int TotalModerators { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }

    public decimal TotalRevenue { get; set; }
    public List<AdminRevenuePointDto> Revenue7Days { get; set; } = new();
    public List<AdminRevenuePointDto> Revenue30Days { get; set; } = new();
    public List<AdminRevenuePointDto> Revenue12Months { get; set; } = new();
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();

    public int TotalSubscriptions { get; set; }
    public int ActiveSubs { get; set; }
    public int ExpiredSubs { get; set; }
    public List<AdminSubscriptionPointDto> Subscription7Days { get; set; } = new();
    public List<AdminSubscriptionPointDto> Subscription30Days { get; set; } = new();
    public List<AdminSubscriptionPointDto> Subscription12Months { get; set; } = new();

    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public int ActiveJobPostings { get; set; }
    public int TotalContracts { get; set; }
    public AdminQueueRangeDto Queue7Days { get; set; } = new();
    public AdminQueueRangeDto Queue30Days { get; set; } = new();
    public AdminQueueRangeDto Queue12Months { get; set; } = new();

    public List<AdminUserGrowthPointDto> NewUsers7Days { get; set; } = new();
    public List<AdminUserGrowthPointDto> NewUsers30Days { get; set; } = new();
    public List<AdminUserGrowthPointDto> NewUsers12Months { get; set; } = new();
}

public class AdminRevenuePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class AdminSubscriptionPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AdminUserGrowthPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int NewUsers { get; set; }
}

public class AdminQueueRangeDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public List<AdminQueueTimelinePointDto> Timeline { get; set; } = new();
}

public class AdminQueueTimelinePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
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

    public string StatusLabel => Status switch { 1 => "Hoàn thành", 0 => "Chờ xử lý", _ => "Thất bại" };
    public string TypeLabel => Type switch { 1 => "Gói đăng ký", 2 => "Hoàn tiền", _ => "Thanh toán" };
}

public class ApiDashboardStatsDto
{
    public ApiUserStatsDto? UserStats { get; set; }
    public ApiRevenueStatsDto? RevenueStats { get; set; }
    public ApiSubscriptionStatsDto? SubscriptionStats { get; set; }
    public ApiPlatformHealthStatsDto? PlatformHealth { get; set; }
    public ApiModerationQueueStatsDto? ModerationQueue { get; set; }
    public ApiUserGrowthStatsDto? UserGrowth { get; set; }

    public AdminDashboardDto ToViewModel() => new()
    {
        TotalUsers = UserStats?.TotalUsers ?? 0,
        TotalParents = UserStats?.TotalParents ?? 0,
        TotalNannies = UserStats?.TotalNannies ?? 0,
        TotalModerators = UserStats?.TotalModerators ?? 0,
        ActiveUsers = UserStats?.ActiveUsers ?? 0,
        InactiveUsers = UserStats?.InactiveUsers ?? 0,
        TotalRevenue = RevenueStats?.TotalRevenue ?? 0,
        Revenue7Days = RevenueStats?.Last7Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        Revenue30Days = RevenueStats?.Last30Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        Revenue12Months = RevenueStats?.Last12Months?.Select(item => item.ToViewModel()).ToList() ?? new(),
        RecentTransactions = RevenueStats?.RecentTransactions ?? new(),
        TotalSubscriptions = SubscriptionStats?.TotalSubscriptions ?? 0,
        ActiveSubs = SubscriptionStats?.ActiveSubs ?? 0,
        ExpiredSubs = SubscriptionStats?.ExpiredSubs ?? 0,
        Subscription7Days = SubscriptionStats?.Last7Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        Subscription30Days = SubscriptionStats?.Last30Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        Subscription12Months = SubscriptionStats?.Last12Months?.Select(item => item.ToViewModel()).ToList() ?? new(),
        PendingVerifications = PlatformHealth?.PendingVerifications ?? 0,
        PendingJobPostings = ModerationQueue?.PendingJobPostings ?? 0,
        PendingReports = ModerationQueue?.PendingReports ?? 0,
        ActiveJobPostings = PlatformHealth?.ActiveJobPostings ?? 0,
        TotalContracts = PlatformHealth?.TotalContracts ?? 0,
        Queue7Days = ModerationQueue?.Last7Days?.ToViewModel() ?? new(),
        Queue30Days = ModerationQueue?.Last30Days?.ToViewModel() ?? new(),
        Queue12Months = ModerationQueue?.Last12Months?.ToViewModel() ?? new(),
        NewUsers7Days = UserGrowth?.Last7Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        NewUsers30Days = UserGrowth?.Last30Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        NewUsers12Months = UserGrowth?.Last12Months?.Select(item => item.ToViewModel()).ToList() ?? new()
    };
}

public class ApiUserStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalParents { get; set; }
    public int TotalNannies { get; set; }
    public int TotalModerators { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
}

public class ApiRevenueStatsDto
{
    public decimal TotalRevenue { get; set; }
    public List<ApiRevenuePointDto> Last7Days { get; set; } = new();
    public List<ApiRevenuePointDto> Last30Days { get; set; } = new();
    public List<ApiRevenuePointDto> Last12Months { get; set; } = new();
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class ApiRevenuePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Revenue { get; set; }

    public AdminRevenuePointDto ToViewModel() => new()
    {
        Date = Date,
        Label = Label,
        Revenue = Revenue
    };
}

public class ApiSubscriptionStatsDto
{
    public int TotalSubscriptions { get; set; }
    public int ActiveSubs { get; set; }
    public int ExpiredSubs { get; set; }
    public List<ApiSubscriptionPointDto> Last7Days { get; set; } = new();
    public List<ApiSubscriptionPointDto> Last30Days { get; set; } = new();
    public List<ApiSubscriptionPointDto> Last12Months { get; set; } = new();
}

public class ApiSubscriptionPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }

    public AdminSubscriptionPointDto ToViewModel() => new()
    {
        Date = Date,
        Label = Label,
        Count = Count
    };
}

public class ApiPlatformHealthStatsDto
{
    public int PendingVerifications { get; set; }
    public int ActiveJobPostings { get; set; }
    public int TotalContracts { get; set; }
}

public class ApiModerationQueueStatsDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public ApiQueueRangeDto? Last7Days { get; set; }
    public ApiQueueRangeDto? Last30Days { get; set; }
    public ApiQueueRangeDto? Last12Months { get; set; }
}

public class ApiQueueRangeDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public List<ApiQueueTimelinePointDto> Timeline { get; set; } = new();

    public AdminQueueRangeDto ToViewModel() => new()
    {
        PendingVerifications = PendingVerifications,
        PendingJobPostings = PendingJobPostings,
        PendingReports = PendingReports,
        Timeline = Timeline.Select(item => item.ToViewModel()).ToList()
    };
}

public class ApiQueueTimelinePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }

    public AdminQueueTimelinePointDto ToViewModel() => new()
    {
        Date = Date,
        Label = Label,
        PendingVerifications = PendingVerifications,
        PendingJobPostings = PendingJobPostings,
        PendingReports = PendingReports
    };
}

public class ApiUserGrowthStatsDto
{
    public List<ApiUserGrowthPointDto> Last7Days { get; set; } = new();
    public List<ApiUserGrowthPointDto> Last30Days { get; set; } = new();
    public List<ApiUserGrowthPointDto> Last12Months { get; set; } = new();
}

public class ApiUserGrowthPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int NewUsers { get; set; }

    public AdminUserGrowthPointDto ToViewModel() => new()
    {
        Date = Date,
        Label = Label,
        NewUsers = NewUsers
    };
}

public class CreateModeratorRequest
{
    [Required(ErrorMessage = "Email không được để trống, phải là 1 email hợp lệ")]
    [EmailAddress(ErrorMessage = "Email không được để trống, phải là 1 email hợp lệ")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$", ErrorMessage = "Mật khẩu phải có ít nhất 8 kí tự, phải chứa ít nhất 1 kí tự đặc biệt, 1 kí tự in hoa")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Họ không được để trống")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Tên không được để trống")]
    public string LastName { get; set; } = "";

    public string? PhoneNumber { get; set; }
}

public class UpdateModeratorRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public int Status { get; set; }
}
