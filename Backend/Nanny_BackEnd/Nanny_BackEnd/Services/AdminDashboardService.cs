using Nanny_BackEnd.DTOs.Dashboard;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Helpers;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class AdminDashboardService
{
    private readonly AdminDashboardRepository _adminDashboardRepo;

    public AdminDashboardService(AdminDashboardRepository adminDashboardRepo)
    {
        _adminDashboardRepo = adminDashboardRepo;
    }

    public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync()
    {
        var userStats = await GetUserStatsAsync();
        var revenueStats = await GetRevenueStatsAsync();
        var subscriptionStats = await GetSubscriptionStatsAsync();
        var (pendingVerifications, pendingJobPostings, pendingReports, activeJobs, totalContracts) =
            await GetCurrentModerationHealthCountersAsync();

        return new AdminDashboardStatsDto
        {
            UserStats = userStats,
            RevenueStats = revenueStats,
            SubscriptionStats = subscriptionStats,
            PlatformHealth = new PlatformHealthStatsDto
            {
                PendingVerifications = pendingVerifications,
                ActiveJobPostings = activeJobs,
                TotalContracts = totalContracts
            },
            ModerationQueue = await GetModerationQueueStatsAsync(pendingVerifications, pendingJobPostings, pendingReports),
            ModerationResults = await GetModerationResultsAsync(),
            UserGrowth = await GetUserGrowthStatsAsync()
        };
    }

    private async Task<(int PendingVerifications, int PendingJobPostings, int PendingReports, int ActiveJobs, int TotalContracts)> GetCurrentModerationHealthCountersAsync()
    {
        var pendingVerifications = await _adminDashboardRepo.GetPendingVerificationCountAsync();
        var activeJobs = await _adminDashboardRepo.GetActiveJobPostingCountAsync();
        var totalContracts = await _adminDashboardRepo.GetTotalContractsCountAsync();
        var pendingJobPostings = await _adminDashboardRepo.GetPendingJobPostingCountAsync();
        var pendingReports = await _adminDashboardRepo.GetPendingReportsCountAsync();

        return (pendingVerifications, pendingJobPostings, pendingReports, activeJobs, totalContracts);
    }

    private async Task<UserStatsDto> GetUserStatsAsync()
    {
        var totalUsers = await _adminDashboardRepo.GetTotalUsersCountAsync();
        var totalParents = await _adminDashboardRepo.GetUserCountByRoleAsync("Parent");
        var totalNannies = await _adminDashboardRepo.GetUserCountByRoleAsync("Nanny");
        var totalModerators = await _adminDashboardRepo.GetUserCountByRoleAsync("Moderator");
        var activeUsers = await _adminDashboardRepo.GetUserCountByStatusAsync((int)UserStatus.Active);
        var inactiveUsers = await _adminDashboardRepo.GetUserCountByStatusAsync((int)UserStatus.Inactive);
        var pendingUsers = await _adminDashboardRepo.GetUserCountByStatusAsync((int)UserStatus.Pending);
        var bannedUsers = await _adminDashboardRepo.GetUserCountByStatusAsync((int)UserStatus.Banned);

        return new UserStatsDto
        {
            TotalUsers = totalUsers,
            TotalParents = totalParents,
            TotalNannies = totalNannies,
            TotalModerators = totalModerators,
            ActiveUsers = activeUsers,
            InactiveUsers = inactiveUsers,
            PendingUsers = pendingUsers,
            BannedUsers = bannedUsers
        };
    }

    private async Task<RevenueStatsDto> GetRevenueStatsAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var start7Days = todayUtc.AddDays(-6);
        var start30Days = todayUtc.AddDays(-29);
        var start12Months = new DateTime(todayUtc.Year, todayUtc.Month, 1).AddMonths(-11);
        var endMonth = new DateTime(todayUtc.Year, todayUtc.Month, 1);

        var totalRevenue = await _adminDashboardRepo.GetTotalRevenueAsync();
        var recentTransactions = await _adminDashboardRepo.GetRecentTransactionsAsync(5);
        var monthlyRevenue = await _adminDashboardRepo.GetMonthlyRevenueAsync(12);
        var completedTransactions = await _adminDashboardRepo.GetCompletedTransactionEventsSinceAsync(start12Months);

        return new RevenueStatsDto
        {
            TotalRevenue = totalRevenue,
            MonthlyRevenue = monthlyRevenue.Select(m => new MonthlyRevenueDto
            {
                Year = m.Year,
                Month = m.Month,
                Revenue = m.Revenue
            }).ToList(),
            Last7Days = BuildRevenueDailySeries(start7Days, todayUtc, completedTransactions),
            Last30Days = BuildRevenueDailySeries(start30Days, todayUtc, completedTransactions),
            Last12Months = BuildRevenueMonthlySeries(start12Months, endMonth, completedTransactions),
            RecentTransactions = recentTransactions.Select(t => new RecentTransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                Status = t.Status,
                Type = t.Type,
                Description = t.Description,
                CreatedAt = t.CreatedAt,
                UserName = t.UserName?.Trim(),
                UserEmail = t.UserEmail
            }).ToList()
        };
    }

    private async Task<SubscriptionStatsDto> GetSubscriptionStatsAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var start7Days = todayUtc.AddDays(-6);
        var start30Days = todayUtc.AddDays(-29);
        var start12Months = new DateTime(todayUtc.Year, todayUtc.Month, 1).AddMonths(-11);
        var endMonth = new DateTime(todayUtc.Year, todayUtc.Month, 1);

        var total = await _adminDashboardRepo.GetTotalSubscriptionsAsync();
        var active = await _adminDashboardRepo.GetActiveSubscriptionsAsync();
        var expired = await _adminDashboardRepo.GetExpiredSubscriptionsAsync();
        var monthlySubscriptions = await _adminDashboardRepo.GetMonthlySubscriptionsAsync(12);
        var createdSubscriptions = await _adminDashboardRepo.GetCreatedSubscriptionDatesSinceAsync(start12Months);

        return new SubscriptionStatsDto
        {
            TotalSubscriptions = total,
            ActiveSubs = active,
            ExpiredSubs = expired,
            MonthlySubs = monthlySubscriptions.Select(m => new MonthlySubscriptionDto
            {
                Year = m.Year,
                Month = m.Month,
                Count = m.Count
            }).ToList(),
            Last7Days = BuildSubscriptionDailySeries(start7Days, todayUtc, createdSubscriptions),
            Last30Days = BuildSubscriptionDailySeries(start30Days, todayUtc, createdSubscriptions),
            Last12Months = BuildSubscriptionMonthlySeries(start12Months, endMonth, createdSubscriptions)
        };
    }

    private async Task<ModerationQueueStatsDto> GetModerationQueueStatsAsync(
        int currentPendingVerifications,
        int currentPendingJobPostings,
        int currentPendingReports)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var start7Days = todayUtc.AddDays(-6);
        var start30Days = todayUtc.AddDays(-29);
        var start12Months = new DateTime(todayUtc.Year, todayUtc.Month, 1).AddMonths(-11);

        return new ModerationQueueStatsDto
        {
            PendingVerifications = currentPendingVerifications,
            PendingJobPostings = currentPendingJobPostings,
            PendingReports = currentPendingReports,
            Last7Days = await GetQueueRangeStatsAsync(start7Days),
            Last30Days = await GetQueueRangeStatsAsync(start30Days),
            Last12Months = await GetQueueRangeStatsAsync(start12Months)
        };
    }

    private async Task<QueueRangeStatsDto> GetQueueRangeStatsAsync(DateTime startDateUtc)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var startDate = startDateUtc.Date;
        var useMonthlyBuckets = startDate.Day == 1 && startDate.Month != todayUtc.Month || (todayUtc - startDate).TotalDays > 31;

        var pendingVerificationDates = await _adminDashboardRepo.GetPendingVerificationDatesSinceAsync(startDateUtc);
        var pendingJobPostingDates = await _adminDashboardRepo.GetPendingJobPostingDatesSinceAsync(startDateUtc);
        var pendingReportDates = await _adminDashboardRepo.GetPendingReportDatesSinceAsync(startDateUtc);

        return new QueueRangeStatsDto
        {
            PendingVerifications = pendingVerificationDates.Count,
            PendingJobPostings = pendingJobPostingDates.Count,
            PendingReports = pendingReportDates.Count,
            Timeline = useMonthlyBuckets
                ? BuildQueueMonthlySeries(
                    new DateTime(startDate.Year, startDate.Month, 1),
                    new DateTime(todayUtc.Year, todayUtc.Month, 1),
                    pendingVerificationDates,
                    pendingJobPostingDates,
                    pendingReportDates)
                : BuildQueueDailySeries(
                    startDate,
                    todayUtc,
                    pendingVerificationDates,
                    pendingJobPostingDates,
                    pendingReportDates)
        };
    }

    private async Task<ModerationResultsStatsDto> GetModerationResultsAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var start7Days = todayUtc.AddDays(-6);
        var start30Days = todayUtc.AddDays(-29);
        var start12Months = new DateTime(todayUtc.Year, todayUtc.Month, 1).AddMonths(-11);
        var endMonth = new DateTime(todayUtc.Year, todayUtc.Month, 1);

        var verificationItems = await _adminDashboardRepo.GetVerificationModerationEventsSinceAsync(start12Months);
        var jobItems = await _adminDashboardRepo.GetJobPostingModerationEventsSinceAsync(start12Months);

        return new ModerationResultsStatsDto
        {
            Last7Days = BuildModerationDailySeries(start7Days, todayUtc, verificationItems, jobItems),
            Last30Days = BuildModerationDailySeries(start30Days, todayUtc, verificationItems, jobItems),
            Last12Months = BuildModerationMonthlySeries(start12Months, endMonth, verificationItems, jobItems)
        };
    }

    private async Task<UserGrowthStatsDto> GetUserGrowthStatsAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var start7Days = todayUtc.AddDays(-6);
        var start30Days = todayUtc.AddDays(-29);
        var start12Months = new DateTime(todayUtc.Year, todayUtc.Month, 1).AddMonths(-11);
        var endMonth = new DateTime(todayUtc.Year, todayUtc.Month, 1);

        var createdAtList = await _adminDashboardRepo.GetUserCreatedDatesSinceAsync(start12Months);

        return new UserGrowthStatsDto
        {
            Last7Days = BuildUserGrowthDailySeries(start7Days, todayUtc, createdAtList),
            Last30Days = BuildUserGrowthDailySeries(start30Days, todayUtc, createdAtList),
            Last12Months = BuildUserGrowthMonthlySeries(start12Months, endMonth, createdAtList)
        };
    }

    private static List<DailyModerationResultDto> BuildModerationDailySeries(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<DashboardModerationEventQueryDto> verificationItems,
        IEnumerable<DashboardModerationEventQueryDto> jobItems)
    {
        var verificationApproved = verificationItems
            .Where(v => v.Status == (int)NannyVerificationRequestStatus.Approved)
            .GroupBy(v => v.Date.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        var verificationRejected = verificationItems
            .Where(v => v.Status == (int)NannyVerificationRequestStatus.Rejected)
            .GroupBy(v => v.Date.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        var jobApproved = jobItems
            .Where(j => j.Status == (int)JobPostingModerationStatus.Approved)
            .GroupBy(j => j.Date.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        var jobRejected = jobItems
            .Where(j => j.Status == (int)JobPostingModerationStatus.Rejected)
            .GroupBy(j => j.Date.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<DailyModerationResultDto>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            verificationApproved.TryGetValue(date, out var verificationApprovedCount);
            verificationRejected.TryGetValue(date, out var verificationRejectedCount);
            jobApproved.TryGetValue(date, out var jobApprovedCount);
            jobRejected.TryGetValue(date, out var jobRejectedCount);

            items.Add(new DailyModerationResultDto
            {
                Date = date,
                Label = date.ToString("dd/MM"),
                VerificationApproved = verificationApprovedCount,
                VerificationRejected = verificationRejectedCount,
                JobApproved = jobApprovedCount,
                JobRejected = jobRejectedCount
            });
        }

        return items;
    }

    private static List<DailyModerationResultDto> BuildModerationMonthlySeries(
        DateTime startMonth,
        DateTime endMonth,
        IEnumerable<DashboardModerationEventQueryDto> verificationItems,
        IEnumerable<DashboardModerationEventQueryDto> jobItems)
    {
        var verificationApproved = verificationItems
            .Where(v => v.Status == (int)NannyVerificationRequestStatus.Approved)
            .GroupBy(v => new DateTime(v.Date.Year, v.Date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());
        var verificationRejected = verificationItems
            .Where(v => v.Status == (int)NannyVerificationRequestStatus.Rejected)
            .GroupBy(v => new DateTime(v.Date.Year, v.Date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());
        var jobApproved = jobItems
            .Where(j => j.Status == (int)JobPostingModerationStatus.Approved)
            .GroupBy(j => new DateTime(j.Date.Year, j.Date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());
        var jobRejected = jobItems
            .Where(j => j.Status == (int)JobPostingModerationStatus.Rejected)
            .GroupBy(j => new DateTime(j.Date.Year, j.Date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<DailyModerationResultDto>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            verificationApproved.TryGetValue(month, out var verificationApprovedCount);
            verificationRejected.TryGetValue(month, out var verificationRejectedCount);
            jobApproved.TryGetValue(month, out var jobApprovedCount);
            jobRejected.TryGetValue(month, out var jobRejectedCount);

            items.Add(new DailyModerationResultDto
            {
                Date = month,
                Label = month.ToString("MM/yyyy"),
                VerificationApproved = verificationApprovedCount,
                VerificationRejected = verificationRejectedCount,
                JobApproved = jobApprovedCount,
                JobRejected = jobRejectedCount
            });
        }

        return items;
    }

    private static List<DailyUserGrowthDto> BuildUserGrowthDailySeries(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<DateTime> createdAtList)
    {
        var countsByDate = createdAtList
            .GroupBy(createdAt => createdAt.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<DailyUserGrowthDto>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            countsByDate.TryGetValue(date, out var count);
            items.Add(new DailyUserGrowthDto
            {
                Date = date,
                Label = date.ToString("dd/MM"),
                NewUsers = count
            });
        }

        return items;
    }

    private static List<DailyUserGrowthDto> BuildUserGrowthMonthlySeries(
        DateTime startMonth,
        DateTime endMonth,
        IEnumerable<DateTime> createdAtList)
    {
        var countsByMonth = createdAtList
            .GroupBy(createdAt => new DateTime(createdAt.Year, createdAt.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<DailyUserGrowthDto>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            countsByMonth.TryGetValue(month, out var count);
            items.Add(new DailyUserGrowthDto
            {
                Date = month,
                Label = month.ToString("MM/yyyy"),
                NewUsers = count
            });
        }

        return items;
    }

    private static List<QueueTimelinePointDto> BuildQueueDailySeries(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<DateTime> verificationDates,
        IEnumerable<DateTime> jobPostingDates,
        IEnumerable<DateTime> reportDates)
    {
        var verificationsByDate = verificationDates
            .GroupBy(date => date.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        var jobsByDate = jobPostingDates
            .GroupBy(date => date.Date)
            .ToDictionary(group => group.Key, group => group.Count());
        var reportsByDate = reportDates
            .GroupBy(date => date.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<QueueTimelinePointDto>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            verificationsByDate.TryGetValue(date, out var verificationCount);
            jobsByDate.TryGetValue(date, out var jobCount);
            reportsByDate.TryGetValue(date, out var reportCount);

            items.Add(new QueueTimelinePointDto
            {
                Date = date,
                Label = date.ToString("dd/MM"),
                PendingVerifications = verificationCount,
                PendingJobPostings = jobCount,
                PendingReports = reportCount
            });
        }

        return items;
    }

    private static List<QueueTimelinePointDto> BuildQueueMonthlySeries(
        DateTime startMonth,
        DateTime endMonth,
        IEnumerable<DateTime> verificationDates,
        IEnumerable<DateTime> jobPostingDates,
        IEnumerable<DateTime> reportDates)
    {
        var verificationsByMonth = verificationDates
            .GroupBy(date => new DateTime(date.Year, date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());
        var jobsByMonth = jobPostingDates
            .GroupBy(date => new DateTime(date.Year, date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());
        var reportsByMonth = reportDates
            .GroupBy(date => new DateTime(date.Year, date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<QueueTimelinePointDto>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            verificationsByMonth.TryGetValue(month, out var verificationCount);
            jobsByMonth.TryGetValue(month, out var jobCount);
            reportsByMonth.TryGetValue(month, out var reportCount);

            items.Add(new QueueTimelinePointDto
            {
                Date = month,
                Label = month.ToString("MM/yyyy"),
                PendingVerifications = verificationCount,
                PendingJobPostings = jobCount,
                PendingReports = reportCount
            });
        }

        return items;
    }

    private static List<RevenuePointDto> BuildRevenueDailySeries(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<DashboardRevenueEventQueryDto> events)
    {
        var revenueByDate = events
            .GroupBy(item => item.Date.Date)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Revenue));

        var items = new List<RevenuePointDto>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            revenueByDate.TryGetValue(date, out var revenue);
            items.Add(new RevenuePointDto
            {
                Date = date,
                Label = date.ToString("dd/MM"),
                Revenue = revenue
            });
        }

        return items;
    }

    private static List<RevenuePointDto> BuildRevenueMonthlySeries(
        DateTime startMonth,
        DateTime endMonth,
        IEnumerable<DashboardRevenueEventQueryDto> events)
    {
        var revenueByMonth = events
            .GroupBy(item => new DateTime(item.Date.Year, item.Date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Revenue));

        var items = new List<RevenuePointDto>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            revenueByMonth.TryGetValue(month, out var revenue);
            items.Add(new RevenuePointDto
            {
                Date = month,
                Label = month.ToString("MM/yyyy"),
                Revenue = revenue
            });
        }

        return items;
    }

    private static List<SubscriptionPointDto> BuildSubscriptionDailySeries(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<DateTime> createdAtList)
    {
        var countsByDate = createdAtList
            .GroupBy(createdAt => createdAt.Date)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<SubscriptionPointDto>();
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            countsByDate.TryGetValue(date, out var count);
            items.Add(new SubscriptionPointDto
            {
                Date = date,
                Label = date.ToString("dd/MM"),
                Count = count
            });
        }

        return items;
    }

    private static List<SubscriptionPointDto> BuildSubscriptionMonthlySeries(
        DateTime startMonth,
        DateTime endMonth,
        IEnumerable<DateTime> createdAtList)
    {
        var countsByMonth = createdAtList
            .GroupBy(createdAt => new DateTime(createdAt.Year, createdAt.Month, 1))
            .ToDictionary(group => group.Key, group => group.Count());

        var items = new List<SubscriptionPointDto>();
        for (var month = startMonth; month <= endMonth; month = month.AddMonths(1))
        {
            countsByMonth.TryGetValue(month, out var count);
            items.Add(new SubscriptionPointDto
            {
                Date = month,
                Label = month.ToString("MM/yyyy"),
                Count = count
            });
        }

        return items;
    }
}
