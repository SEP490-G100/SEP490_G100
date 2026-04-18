using WebSite.Models.Account;

namespace WebSite.Models.Moderator;

public class ModeratorDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalParents { get; set; }
    public int TotalNannies { get; set; }
    public int TotalModerators { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int PendingUsers { get; set; }
    public int BannedUsers { get; set; }
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public int ActiveJobPostings { get; set; }
    public int TotalContracts { get; set; }
    public ModeratorQueueRangeDto Queue7Days { get; set; } = new();
    public ModeratorQueueRangeDto Queue30Days { get; set; } = new();
    public ModeratorQueueRangeDto Queue12Months { get; set; } = new();
    public List<ModeratorModerationResultPointDto> ModerationResults7Days { get; set; } = new();
    public List<ModeratorModerationResultPointDto> ModerationResults30Days { get; set; } = new();
    public List<ModeratorModerationResultPointDto> ModerationResults12Months { get; set; } = new();
    public List<ModeratorUserGrowthPointDto> NewUsers7Days { get; set; } = new();
    public List<ModeratorUserGrowthPointDto> NewUsers30Days { get; set; } = new();
    public List<ModeratorUserGrowthPointDto> NewUsers12Months { get; set; } = new();
    public List<AccountDto> RecentAccounts { get; set; } = new();
}

public class ApiModeratorDashboardStatsDto
{
    public ApiModeratorUserStatsDto? UserStats { get; set; }
    public ApiModeratorPlatformHealthStatsDto? PlatformHealth { get; set; }
    public ApiModeratorModerationQueueStatsDto? ModerationQueue { get; set; }
    public ApiModeratorModerationResultsStatsDto? ModerationResults { get; set; }
    public ApiModeratorUserGrowthStatsDto? UserGrowth { get; set; }

    public ModeratorDashboardDto ToViewModel(List<AccountDto>? recentAccounts = null) => new()
    {
        TotalUsers = UserStats?.TotalUsers ?? 0,
        TotalParents = UserStats?.TotalParents ?? 0,
        TotalNannies = UserStats?.TotalNannies ?? 0,
        TotalModerators = UserStats?.TotalModerators ?? 0,
        ActiveUsers = UserStats?.ActiveUsers ?? 0,
        InactiveUsers = UserStats?.InactiveUsers ?? 0,
        PendingUsers = UserStats?.PendingUsers ?? 0,
        BannedUsers = UserStats?.BannedUsers ?? 0,
        PendingVerifications = PlatformHealth?.PendingVerifications ?? 0,
        PendingJobPostings = ModerationQueue?.PendingJobPostings ?? 0,
        PendingReports = ModerationQueue?.PendingReports ?? 0,
        ActiveJobPostings = PlatformHealth?.ActiveJobPostings ?? 0,
        TotalContracts = PlatformHealth?.TotalContracts ?? 0,
        Queue7Days = ModerationQueue?.Last7Days?.ToViewModel() ?? new(),
        Queue30Days = ModerationQueue?.Last30Days?.ToViewModel() ?? new(),
        Queue12Months = ModerationQueue?.Last12Months?.ToViewModel() ?? new(),
        ModerationResults7Days = ModerationResults?.Last7Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        ModerationResults30Days = ModerationResults?.Last30Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        ModerationResults12Months = ModerationResults?.Last12Months?.Select(item => item.ToViewModel()).ToList() ?? new(),
        NewUsers7Days = UserGrowth?.Last7Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        NewUsers30Days = UserGrowth?.Last30Days?.Select(item => item.ToViewModel()).ToList() ?? new(),
        NewUsers12Months = UserGrowth?.Last12Months?.Select(item => item.ToViewModel()).ToList() ?? new(),
        RecentAccounts = recentAccounts ?? new()
    };
}

public class ApiModeratorUserStatsDto
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

public class ApiModeratorPlatformHealthStatsDto
{
    public int PendingVerifications { get; set; }
    public int ActiveJobPostings { get; set; }
    public int TotalContracts { get; set; }
}

public class ApiModeratorModerationQueueStatsDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public ApiModeratorQueueRangeDto? Last7Days { get; set; }
    public ApiModeratorQueueRangeDto? Last30Days { get; set; }
    public ApiModeratorQueueRangeDto? Last12Months { get; set; }
}

public class ApiModeratorQueueRangeDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public List<ApiModeratorQueueTimelinePointDto> Timeline { get; set; } = new();

    public ModeratorQueueRangeDto ToViewModel() => new()
    {
        PendingVerifications = PendingVerifications,
        PendingJobPostings = PendingJobPostings,
        PendingReports = PendingReports,
        Timeline = Timeline.Select(item => item.ToViewModel()).ToList()
    };
}

public class ModeratorQueueRangeDto
{
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
    public List<ModeratorQueueTimelinePointDto> Timeline { get; set; } = new();
}

public class ApiModeratorQueueTimelinePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }

    public ModeratorQueueTimelinePointDto ToViewModel() => new()
    {
        Date = Date,
        Label = Label,
        PendingVerifications = PendingVerifications,
        PendingJobPostings = PendingJobPostings,
        PendingReports = PendingReports
    };
}

public class ModeratorQueueTimelinePointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int PendingVerifications { get; set; }
    public int PendingJobPostings { get; set; }
    public int PendingReports { get; set; }
}

public class ApiModeratorModerationResultsStatsDto
{
    public List<ApiModeratorModerationResultPointDto> Last7Days { get; set; } = new();
    public List<ApiModeratorModerationResultPointDto> Last30Days { get; set; } = new();
    public List<ApiModeratorModerationResultPointDto> Last12Months { get; set; } = new();
}

public class ApiModeratorModerationResultPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int VerificationApproved { get; set; }
    public int VerificationRejected { get; set; }
    public int JobApproved { get; set; }
    public int JobRejected { get; set; }

    public ModeratorModerationResultPointDto ToViewModel() => new()
    {
        Date = Date,
        Label = Label,
        VerificationApproved = VerificationApproved,
        VerificationRejected = VerificationRejected,
        JobApproved = JobApproved,
        JobRejected = JobRejected
    };
}

public class ModeratorModerationResultPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int VerificationApproved { get; set; }
    public int VerificationRejected { get; set; }
    public int JobApproved { get; set; }
    public int JobRejected { get; set; }
}

public class ApiModeratorUserGrowthStatsDto
{
    public List<ApiModeratorUserGrowthPointDto> Last7Days { get; set; } = new();
    public List<ApiModeratorUserGrowthPointDto> Last30Days { get; set; } = new();
    public List<ApiModeratorUserGrowthPointDto> Last12Months { get; set; } = new();
}

public class ApiModeratorUserGrowthPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int NewUsers { get; set; }

    public ModeratorUserGrowthPointDto ToViewModel() => new()
    {
        Date = Date,
        Label = Label,
        NewUsers = NewUsers
    };
}

public class ModeratorUserGrowthPointDto
{
    public DateTime Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int NewUsers { get; set; }
}
