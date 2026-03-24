using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Data;
using Nanny_BackEnd.DTOs.Dashboard;
using Nanny_BackEnd.Enums;
using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;

public class DashboardService
{
    private readonly UserRepository _userRepo;
    private readonly TransactionRepository _transactionRepo;
    private readonly UserSubscriptionRepository _subscriptionRepo;
    private readonly VerificationRequestRepository _verificationRepo;
    private readonly JobRepository _jobRepo;
    private readonly ContractRepository _contractRepo;
    private readonly Sep490NannyDbContext _db;

    public DashboardService(
        UserRepository userRepo,
        TransactionRepository transactionRepo,
        UserSubscriptionRepository subscriptionRepo,
        VerificationRequestRepository verificationRepo,
        JobRepository jobRepo,
        ContractRepository contractRepo,
        Sep490NannyDbContext db)
    {
        _userRepo = userRepo;
        _transactionRepo = transactionRepo;
        _subscriptionRepo = subscriptionRepo;
        _verificationRepo = verificationRepo;
        _jobRepo = jobRepo;
        _contractRepo = contractRepo;
        _db = db;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var userStats = await GetUserStatsAsync();
        var revenueStats = await GetRevenueStatsAsync();
        var subscriptionStats = await GetSubscriptionStatsAsync();

        var pendingVerifications = await _verificationRepo.GetQuery()
            .CountAsync(v => !v.IsDeleted && v.Status == (int)NannyVerificationRequestStatus.Pending);
        var activeJobs = await _jobRepo.GetQuery()
            .CountAsync(j => !j.IsDeleted && j.Status == 1);
        var totalContracts = await _contractRepo.GetQuery()
            .CountAsync(c => !c.IsDeleted);
        var pendingJobPostings = await _jobRepo.GetQuery()
            .CountAsync(j => !j.IsDeleted && j.ModerationStatus == (int)JobPostingModerationStatus.Pending);
        var pendingReports = await _db.Reports
            .AsNoTracking()
            .CountAsync(r => !r.IsDeleted && r.HandledAt == null);

        return new DashboardStatsDto
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

    private async Task<UserStatsDto> GetUserStatsAsync()
    {
        var totalUsers = await _userRepo.GetTotalUsersCountAsync();
        var totalParents = await _userRepo.GetUserCountByRoleAsync("Parent");
        var totalNannies = await _userRepo.GetUserCountByRoleAsync("Nanny");
        var totalModerators = await _userRepo.GetUserCountByRoleAsync("Moderator");
        var activeUsers = await _userRepo.GetUserCountByStatusAsync(1);
        var inactiveUsers = await _userRepo.GetUserCountByStatusAsync(0);

        return new UserStatsDto
        {
            TotalUsers = totalUsers,
            TotalParents = totalParents,
            TotalNannies = totalNannies,
            TotalModerators = totalModerators,
            ActiveUsers = activeUsers,
            InactiveUsers = inactiveUsers,
            PendingUsers = 0,
            BannedUsers = 0
        };
    }

    private async Task<RevenueStatsDto> GetRevenueStatsAsync()
    {
        var todayUtc = DateTime.UtcNow.Date;
        var start7Days = todayUtc.AddDays(-6);
        var start30Days = todayUtc.AddDays(-29);
        var start12Months = new DateTime(todayUtc.Year, todayUtc.Month, 1).AddMonths(-11);
        var endMonth = new DateTime(todayUtc.Year, todayUtc.Month, 1);

        var totalRevenue = await _transactionRepo.GetTotalRevenueAsync();
        var recentTransactions = await _transactionRepo.GetRecentTransactionsAsync(5);
        var monthlyRevenue = await _transactionRepo.GetMonthlyRevenueAsync(12);
        var completedTransactions = await _db.Transactions
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.Status == 1 && t.CreatedAt >= start12Months)
            .Select(t => new RevenueEventPoint
            {
                Date = t.CreatedAt,
                Revenue = t.Amount
            })
            .ToListAsync();

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
                UserName = t.User?.FirstName + " " + t.User?.LastName,
                UserEmail = t.User?.Email
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

        var total = await _subscriptionRepo.GetTotalSubscriptionsAsync();
        var active = await _subscriptionRepo.GetActiveSubscriptionsAsync();
        var expired = await _subscriptionRepo.GetExpiredSubscriptionsAsync();
        var monthlySubscriptions = await _subscriptionRepo.GetMonthlySubscriptionsAsync(12);
        var createdSubscriptions = await _db.UserSubscriptions
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.CreatedAt >= start12Months)
            .Select(s => s.CreatedAt)
            .ToListAsync();

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

        var pendingVerificationDates = await _verificationRepo.GetQuery()
            .Where(v => !v.IsDeleted
                        && v.Status == (int)NannyVerificationRequestStatus.Pending
                        && v.CreatedAt >= startDateUtc)
            .Select(v => v.CreatedAt)
            .ToListAsync();
        var pendingJobPostingDates = await _jobRepo.GetQuery()
            .Where(j => !j.IsDeleted
                        && j.ModerationStatus == (int)JobPostingModerationStatus.Pending
                        && j.CreatedAt >= startDateUtc)
            .Select(j => j.CreatedAt)
            .ToListAsync();
        var pendingReportDates = await _db.Reports
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.HandledAt == null && r.CreatedAt >= startDateUtc)
            .Select(r => r.CreatedAt)
            .ToListAsync();

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

        var verificationItems = await _db.VerificationRequests
            .AsNoTracking()
            .Where(v => !v.IsDeleted
                        && v.ReviewedAt.HasValue
                        && v.ReviewedAt.Value >= start12Months
                        && (v.Status == (int)NannyVerificationRequestStatus.Approved
                            || v.Status == (int)NannyVerificationRequestStatus.Rejected))
            .Select(v => new ModerationEventPoint
            {
                Date = v.ReviewedAt!.Value,
                Status = v.Status
            })
            .ToListAsync();

        var jobItems = await _db.JobPostings
            .AsNoTracking()
            .Where(j => !j.IsDeleted
                        && j.ModeratedAt.HasValue
                        && j.ModeratedAt.Value >= start12Months
                        && (j.ModerationStatus == (int)JobPostingModerationStatus.Approved
                            || j.ModerationStatus == (int)JobPostingModerationStatus.Rejected))
            .Select(j => new ModerationEventPoint
            {
                Date = j.ModeratedAt!.Value,
                Status = j.ModerationStatus
            })
            .ToListAsync();

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

        var createdAtList = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.CreatedAt >= start12Months)
            .Select(u => u.CreatedAt)
            .ToListAsync();

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
        IEnumerable<ModerationEventPoint> verificationItems,
        IEnumerable<ModerationEventPoint> jobItems)
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
        IEnumerable<ModerationEventPoint> verificationItems,
        IEnumerable<ModerationEventPoint> jobItems)
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
        IEnumerable<RevenueEventPoint> events)
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
        IEnumerable<RevenueEventPoint> events)
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

    private sealed class ModerationEventPoint
    {
        public DateTime Date { get; set; }
        public int Status { get; set; }
    }

    private sealed class RevenueEventPoint
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
    }
}
